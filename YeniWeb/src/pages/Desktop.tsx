import { useEffect, useRef, useState, useCallback } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  AlertCircle,
  ArrowLeft,
  Camera,
  Maximize,
  Minimize,
  Monitor,
  Play,
  RefreshCw,
  Settings,
  Video,
  Send,
  Square,
} from 'lucide-react';
import remoteDesktopService from '../services/remoteDesktop.service';
import { remoteOpsService } from '../services/remoteOps.service';
import { DEFAULT_REMOTE_RESOLUTION, getQualityPercent, REMOTE_QUALITY_MAP, REMOTE_KEY_COMBOS } from '../constants/remoteDesktop';
import type { RemoteShortcutId } from '../constants/remoteDesktop';
import { downloadCanvasImage } from '../utils/canvas';
import { downloadBlob } from '../utils/download';
import type { RemoteDesktopSession, QualityLevel, RemoteClipboardState } from './device-detail/types';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';

export default function Desktop() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const sessionRef = useRef<RemoteDesktopSession | null>(null);
  const lastMouseMove = useRef(0);
  const { t } = useTranslation();

  const [session, setSession] = useState<RemoteDesktopSession | null>(null);
  const [quality, setQuality] = useState<QualityLevel>('medium');
  const [fps, setFps] = useState(15);
  const [showSettings, setShowSettings] = useState(false);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isRecording, setIsRecording] = useState(false);
  const recorderRef = useRef<MediaRecorder | null>(null);
  const recordingChunksRef = useRef<Blob[]>([]);
  const [clipboard, setClipboard] = useState<RemoteClipboardState>({
    value: '',
    loading: false,
    syncing: false,
  });

  const deviceId = id ?? '';
  const isConnected = Boolean(session);

  useEffect(() => {
    sessionRef.current = session;
  }, [session]);

  useEffect(() => {
    return () => {
      const active = sessionRef.current;
      if (active && deviceId) {
        remoteDesktopService.stopSession(deviceId, active.sessionId).catch(() => undefined);
      }
      if (recorderRef.current) {
        recorderRef.current.stop();
      }
    };
  }, [deviceId]);

  const drawFrameToCanvas = useCallback(async (frameBase64: string, width: number, height: number) => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const img = new Image();
    const loadPromise = new Promise<void>((resolve, reject) => {
      img.onload = () => {
        canvas.width = Math.max(1, width);
        canvas.height = Math.max(1, height);
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        resolve();
      };
      img.onerror = (event) => reject(event);
    });
    img.src = `data:image/jpeg;base64,${frameBase64}`;
    await loadPromise;
  }, []);

  useEffect(() => {
    if (!deviceId || !session) return;
    let cancelled = false;
    let timer: number | undefined;

    const pumpFrames = async () => {
      if (!session || cancelled) return;
      try {
        const frame = await remoteDesktopService.captureFrame(deviceId, session.sessionId);
        if (frame?.frameBase64 && !cancelled) {
          const width = frame.width ?? session.width ?? DEFAULT_REMOTE_RESOLUTION.width;
          const height = frame.height ?? session.height ?? DEFAULT_REMOTE_RESOLUTION.height;
          await drawFrameToCanvas(frame.frameBase64, width, height);
        }
      } catch (err) {
        console.error('Frame request failed:', err);
        setError(toErrorMessage(err, t('desktop.error')));
      } finally {
        if (!cancelled) {
          timer = window.setTimeout(pumpFrames, Math.max(1, Math.round(1000 / fps)));
        }
      }
    };

    pumpFrames();
    return () => {
      cancelled = true;
      if (timer) {
        window.clearTimeout(timer);
      }
    };
  }, [deviceId, session, fps, drawFrameToCanvas, t]);

  const updateFullscreenState = useCallback(() => {
    setIsFullscreen(Boolean(document.fullscreenElement));
  }, []);

  useEffect(() => {
    document.addEventListener('fullscreenchange', updateFullscreenState);
    return () => document.removeEventListener('fullscreenchange', updateFullscreenState);
  }, [updateFullscreenState]);

  const startDesktop = async () => {
    if (!deviceId || busy) return;
    setBusy(true);
    setError(null);
    try {
      const started = await remoteDesktopService.startSession(deviceId, {
        quality: getQualityPercent(quality),
        fps,
      });
      const newSession: RemoteDesktopSession = {
        sessionId: started.sessionId,
        width: started.width || DEFAULT_REMOTE_RESOLUTION.width,
        height: started.height || DEFAULT_REMOTE_RESOLUTION.height,
        quality,
        startedAt: new Date().toISOString(),
      };
      setSession(newSession);
      setShowSettings(false);
      await handleClipboardRefresh();
    } catch (err) {
      setError(toErrorMessage(err, t('desktop.error')));
    } finally {
      setBusy(false);
    }
  };

  const stopDesktop = async () => {
    if (!session || busy || !deviceId) return;
    setBusy(true);
    setError(null);
    try {
      await remoteDesktopService.stopSession(deviceId, session.sessionId);
      if (document.fullscreenElement) {
        await document.exitFullscreen().catch(() => undefined);
      }
      setSession(null);
      if (recorderRef.current) {
        recorderRef.current.stop();
      }
      // setIsRecording(false);
      setShowSettings(false);
    } catch (err) {
      setError(toErrorMessage(err, t('desktop.errorStop')));
    } finally {
      setBusy(false);
    }
  };

  const toggleFullscreen = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    if (!document.fullscreenElement) {
      canvas.requestFullscreen().catch(() => undefined);
    } else {
      document.exitFullscreen().catch(() => undefined);
    }
  };

  const handleCapture = async () => {
    if (!canvasRef.current) {
      setError(t('desktop.captureNotImplemented'));
      return;
    }
    try {
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `remote-desktop-${id ?? 'device'}-${timestamp}.png`;
      await downloadCanvasImage(canvasRef.current, filename);
      setError(null);
    } catch (err) {
      setError(toErrorMessage(err, t('desktop.error')));
    }
  };

  const mapPointer = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session) return null;
    const rect = event.currentTarget.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return null;
    const scaleX = session.width / rect.width;
    const scaleY = session.height / rect.height;
    const x = Math.min(session.width - 1, Math.max(0, Math.round((event.clientX - rect.left) * scaleX)));
    const y = Math.min(session.height - 1, Math.max(0, Math.round((event.clientY - rect.top) * scaleY)));
    return { x, y };
  };

  const sendMouseMove = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session || !deviceId) return;
    const coords = mapPointer(event);
    if (!coords) return;
    const now = Date.now();
    if (now - lastMouseMove.current < 50) return;
    lastMouseMove.current = now;
    remoteDesktopService.sendMouseMove(deviceId, session.sessionId, coords.x, coords.y).catch(() => undefined);
  };

  const sendMouseButton = (event: React.MouseEvent<HTMLCanvasElement>, action: 'down' | 'up' | 'click') => {
    if (!session || !deviceId) return;
    event.preventDefault();
    remoteDesktopService.sendMouseButton(deviceId, session.sessionId, event.button, action).catch(() => undefined);
  };

  const sendKeyEvent = (event: React.KeyboardEvent<HTMLDivElement>, action: 'down' | 'up') => {
    if (!session || !deviceId) return;
    event.preventDefault();
    remoteDesktopService.sendKey(deviceId, session.sessionId, event.keyCode, action).catch(() => undefined);
  };

  const handleClipboardChange = (value: string) => {
    setClipboard((prev) => ({ ...prev, value, error: undefined }));
  };

  const handleClipboardRefresh = useCallback(async () => {
    if (!deviceId) return;
    setClipboard((prev) => ({ ...prev, loading: true, error: undefined }));
    try {
      const result = await remoteOpsService.runClipboardGet(deviceId);
      setClipboard({
        value: result.data ?? '',
        loading: false,
        syncing: false,
        error: result.error,
      });
    } catch (err) {
      setClipboard((prev) => ({
        ...prev,
        loading: false,
        error: toErrorMessage(err, t('deviceDetail.remoteDesktop.clipboardError')),
      }));
    }
  }, [deviceId, t]);

  const handleClipboardApply = useCallback(async () => {
    if (!deviceId) return;
    setClipboard((prev) => ({ ...prev, syncing: true, error: undefined }));
    try {
      await remoteOpsService.setClipboard(deviceId, clipboard.value);
      setClipboard((prev) => ({ ...prev, syncing: false }));
    } catch (err) {
      setClipboard((prev) => ({
        ...prev,
        syncing: false,
        error: toErrorMessage(err, t('deviceDetail.remoteDesktop.clipboardError')),
      }));
    }
  }, [deviceId, clipboard.value, t]);

  useEffect(() => {
    if (session && deviceId) {
      handleClipboardRefresh();
    }
  }, [session, deviceId, handleClipboardRefresh]);

  const handleShortcut = useCallback(
    async (shortcut: RemoteShortcutId) => {
      if (!deviceId || !session) return;
      try {
        const keys = REMOTE_KEY_COMBOS[shortcut];
        await remoteDesktopService.sendKeySequence(deviceId, session.sessionId, keys);
      } catch (err) {
        setError(toErrorMessage(err, t('deviceDetail.remoteDesktop.shortcutError')));
      }
    },
    [deviceId, session, t],
  );
  const handlePowerAction = useCallback(
    async (action: 'restart' | 'shutdown' | 'sleep') => {
      if (!deviceId) return;
      const label =
        action === 'restart'
          ? t('deviceDetail.remoteDesktop.powerRestart')
          : action === 'shutdown'
            ? t('deviceDetail.remoteDesktop.powerShutdown')
            : t('deviceDetail.remoteDesktop.powerSleep');
      const confirmed = window.confirm(
        t('deviceDetail.remoteDesktop.powerConfirm', { action: label }, 'Send power command?'),
      );
      if (!confirmed) return;
      try {
        await remoteOpsService.sendPowerAction(deviceId, action);
        setError(null);
      } catch (err) {
        setError(toErrorMessage(err, t('deviceDetail.remoteDesktop.powerError')));
      }
    },
    [deviceId, t],
  );

  const handleRecordingToggle = useCallback(async () => {
    if (isRecording) {
      recorderRef.current?.stop();
      return;
    }
    if (!canvasRef.current || !session) {
      setError(t('deviceDetail.remoteDesktop.recordingError'));
      return;
    }
    try {
      const stream = canvasRef.current.captureStream(Math.max(1, fps));
      const recorder = new MediaRecorder(stream, { mimeType: 'video/webm;codecs=vp9' });
      recordingChunksRef.current = [];
      recorder.ondataavailable = (event) => {
        if (event.data.size) recordingChunksRef.current.push(event.data);
      };
      recorder.onstop = () => {
        const blob = new Blob(recordingChunksRef.current, { type: 'video/webm' });
        recordingChunksRef.current = [];
        if (blob.size > 0) {
          const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
          const filename = `remote-desktop-${id ?? 'device'}-${timestamp}.webm`;
          downloadBlob(blob, filename);
        }
        recorderRef.current = null;
        setIsRecording(false);
      };
      recorder.start();
      recorderRef.current = recorder;
      setIsRecording(true);
    } catch (err) {
      recorderRef.current = null;
      recordingChunksRef.current = [];
      setIsRecording(false);
      setError(toErrorMessage(err, t('deviceDetail.remoteDesktop.recordingError')));
    }
  }, [isRecording, session, fps, id, t]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
      <div className="flex items-center gap-3">
          <button
            onClick={() => navigate('/devices')}
            className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <div>
            <h1 className="text-2xl font-semibold">{t('desktop.title')}</h1>
            <p className="text-sm text-muted-foreground">{t('desktop.description')}</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {isConnected ? (
            <>
              <button
                onClick={handleCapture}
                className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                title={t('desktop.capture')}
              >
                <Camera className="h-4 w-4" />
              </button>
              <button
                onClick={handleRecordingToggle}
                disabled={!isConnected || busy}
                className={`rounded-lg p-2 transition disabled:cursor-not-allowed disabled:opacity-60 ${
                  isRecording
                    ? 'bg-destructive text-white'
                    : 'border border-border bg-secondary/40 text-muted-foreground hover:bg-secondary/70'
                }`}
                title={isRecording ? t('desktop.recordingStop') : t('desktop.recordingStart')}
              >
                {isRecording ? <Square className="h-4 w-4" /> : <Video className="h-4 w-4" />}
              </button>
              <button
                onClick={toggleFullscreen}
                className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                title={isFullscreen ? t('desktop.fullscreenExit') : t('desktop.fullscreenEnter')}
              >
                {isFullscreen ? <Minimize className="h-4 w-4" /> : <Maximize className="h-4 w-4" />}
              </button>
              <button
                onClick={() => setShowSettings((prev) => !prev)}
                className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                title={t('desktop.settings')}
              >
                <Settings className="h-4 w-4" />
              </button>
              <button
                onClick={stopDesktop}
                disabled={busy}
                className="inline-flex items-center gap-2 rounded-lg bg-destructive px-4 py-2 text-sm font-medium text-white transition hover:bg-destructive/90 disabled:cursor-not-allowed disabled:opacity-70"
              >
                <RefreshCw className="h-4 w-4" />
                {t('desktop.disconnect')}
              </button>
            </>
          ) : (
            <button
              onClick={startDesktop}
              disabled={busy || !deviceId}
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-70"
            >
              <Play className="h-4 w-4" />
              {busy ? t('desktop.connecting') : t('desktop.connect')}
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{error}</span>
        </div>
      )}

      {showSettings && isConnected && (
        <div className="grid gap-4 rounded-xl border border-border bg-card p-4 shadow-sm md:grid-cols-3">
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('desktop.quality')}</label>
            <select
              value={quality}
              onChange={(event) => setQuality(event.target.value as QualityLevel)}
              className="w-full rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
              disabled={busy}
            >
              {Object.keys(REMOTE_QUALITY_MAP).map((value) => (
                <option key={value} value={value}>
                  {t(`desktop.qualityOptions.${value}` as const)}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('desktop.fps')}</label>
            <select
              value={fps}
              onChange={(event) => setFps(Number(event.target.value))}
              className="w-full rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
              disabled={busy}
            >
              {[10, 15, 24, 30].map((value) => (
                <option key={value} value={value}>
                  {value} FPS
                </option>
              ))}
            </select>
          </div>
          <div className="self-end text-xs text-muted-foreground">{t('desktop.reconnectHint')}</div>
        </div>
      )}

      <div
        className="rounded-xl border border-border bg-card"
        tabIndex={0}
        onKeyDown={(event) => sendKeyEvent(event, 'down')}
        onKeyUp={(event) => sendKeyEvent(event, 'up')}
      >
        {isConnected ? (
          <canvas
            ref={canvasRef}
            className="h-[600px] w-full bg-black"
            onMouseMove={sendMouseMove}
            onMouseDown={(event) => sendMouseButton(event, 'down')}
            onMouseUp={(event) => sendMouseButton(event, 'up')}
            onClick={(event) => sendMouseButton(event, 'click')}
          />
        ) : (
          <div className="flex h-[600px] flex-col items-center justify-center gap-4 text-muted-foreground">
            <Monitor className="h-20 w-20" />
            <div className="text-center">
              <p className="text-lg font-medium text-foreground">{t('desktop.waiting')}</p>
              <p className="text-sm">{t('desktop.waitingDescription')}</p>
            </div>
            <button
              onClick={startDesktop}
              disabled={busy || !deviceId}
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-70"
            >
              <Play className="h-4 w-4" />
              {busy ? t('desktop.connecting') : t('desktop.start')}
            </button>
          </div>
        )}
      </div>
      {isConnected && (
        <div className="rounded-xl border border-border bg-card/80 p-4 shadow-sm">
          <div className="mb-3 flex items-center justify-between">
            <div>
              <p className="text-sm font-semibold text-foreground">{t('deviceDetail.remoteDesktop.clipboardTitle')}</p>
              <p className="text-xs text-muted-foreground">{t('deviceDetail.remoteDesktop.clipboardDescription')}</p>
            </div>
            <div className="flex gap-2">
              <button
                onClick={handleClipboardRefresh}
                disabled={clipboard.loading}
                className="inline-flex items-center gap-1 rounded-lg border border-border bg-secondary/30 px-3 py-1.5 text-xs font-semibold text-muted-foreground transition hover:bg-secondary/60 disabled:opacity-60"
              >
                <RefreshCw className={`h-3.5 w-3.5 ${clipboard.loading ? 'animate-spin' : ''}`} />
                {t('deviceDetail.remoteDesktop.clipboardRefresh')}
              </button>
              <button
                onClick={handleClipboardApply}
                disabled={clipboard.syncing}
                className="inline-flex items-center gap-1 rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
              >
                <Send className={`h-3.5 w-3.5 ${clipboard.syncing ? 'animate-pulse' : ''}`} />
                {t('deviceDetail.remoteDesktop.clipboardApply')}
              </button>
            </div>
          </div>
          <textarea
            className="min-h-[120px] w-full rounded-lg border border-border bg-background px-3 py-2 text-sm font-mono text-foreground focus:outline-none focus:ring-2 focus:ring-primary/40"
            value={clipboard.value}
            onChange={(event) => handleClipboardChange(event.target.value)}
            placeholder={t('deviceDetail.remoteDesktop.clipboardPlaceholder')}
          />
          {clipboard.error && <p className="mt-2 text-xs text-destructive">{clipboard.error}</p>}
        </div>
      )}
      {isConnected && (
        <div className="rounded-xl border border-border bg-card/80 p-4 shadow-sm">
          <div className="mb-3">
            <p className="text-sm font-semibold text-foreground">{t('deviceDetail.remoteDesktop.shortcutsTitle')}</p>
            <p className="text-xs text-muted-foreground">{t('deviceDetail.remoteDesktop.shortcutsDescription')}</p>
          </div>
          <div className="grid gap-2 sm:grid-cols-3">
            <button
              onClick={() => handleShortcut('ctrlEsc')}
              className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
            >
              {t('deviceDetail.remoteDesktop.shortcutCtrlEsc')}
            </button>
            <button
              onClick={() => handleShortcut('ctrlShiftEsc')}
              className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
            >
              {t('deviceDetail.remoteDesktop.shortcutCtrlShiftEsc')}
            </button>
          </div>
        </div>
      )}
      {isConnected && (
        <div className="rounded-xl border border-border bg-card/80 p-4 shadow-sm">
          <div>
            <p className="text-sm font-semibold text-foreground">{t('deviceDetail.remoteDesktop.powerTitle')}</p>
            <p className="text-xs text-muted-foreground">{t('deviceDetail.remoteDesktop.powerDescription')}</p>
          </div>
          <div className="mt-3 grid gap-2 sm:grid-cols-3">
            <button
              onClick={() => handlePowerAction('restart')}
              className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
            >
              {t('deviceDetail.remoteDesktop.powerRestart')}
            </button>
            <button
              onClick={() => handlePowerAction('shutdown')}
              className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
            >
              {t('deviceDetail.remoteDesktop.powerShutdown')}
            </button>
            <button
              onClick={() => handlePowerAction('sleep')}
              className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
            >
              {t('deviceDetail.remoteDesktop.powerSleep')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}


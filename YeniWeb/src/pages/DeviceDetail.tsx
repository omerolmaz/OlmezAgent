import React, { useState, useCallback, useMemo, useEffect, useRef } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import {
  Activity,
  Terminal as TerminalIcon,
  Archive,
  Shield,
  Clock,
  FolderOpen,
  Gauge,
  ArrowLeft,
  Monitor,
  RefreshCw,
  Server,
  Cpu,
  Wifi,
  MessageSquare,
  Wrench,
  Code2,
  Package,
  ShieldCheck,
} from 'lucide-react';
import { deviceService } from '../services/device.service';
import { diagnosticsService } from '../services/diagnostics.service';
import { inventoryService } from '../services/inventory.service';
import { securityService } from '../services/security.service';
import { commandService } from '../services/command.service';
import { eventLogsService } from '../services/eventLogs.service';
import { healthService } from '../services/health.service';
import { messagingService } from '../services/messaging.service';
import { maintenanceService } from '../services/maintenance.service';
import { softwareService } from '../services/software.service';
import { privacyService } from '../services/privacy.service';
import { scriptsService } from '../services/scripts.service';
import { remoteOpsService } from '../services/remoteOps.service';
import remoteDesktopService from '../services/remoteDesktop.service';
import { getQualityPercent, DEFAULT_REMOTE_RESOLUTION, REMOTE_KEY_COMBOS } from '../constants/remoteDesktop';
import type { RemoteShortcutId } from '../constants/remoteDesktop';
import { downloadCanvasImage } from '../utils/canvas';
import { downloadBlob } from '../utils/download';
import { toErrorMessage } from '../utils/error';
import type { Device } from '../types/device.types';
import { useTranslation } from '../hooks/useTranslation';
import {
  OverviewTab,
  InventoryTab,
  SecurityTab,
  EventLogsTab,
  RemoteDesktopTab,
  TerminalTab,
  FilesTab,
  PerformanceTab,
  MessagingTab,
  MaintenanceTab,
  ScriptsTab,
  SoftwareTab,
  PatchesTab,
} from './device-detail/sections';
import type {
  DiagnosticsState,
  InventoryState,
  SecurityState,
  EventLogState,
  PerformanceState,
  RemoteControlState,
  RemoteDesktopSession,
  RemoteClipboardState,
  TerminalState,
  MessagingFormState,
  MaintenanceFormState,
  ScriptsState,
  ScriptFormState,
  DiagnosticsBundle,
  QualityLevel,
  SoftwareState,
  PatchState,
  PatchFormState,
} from './device-detail/types';

function withTimeout<T>(promise: Promise<T>, timeoutMs: number, timeoutMessage: string): Promise<T> {
  return Promise.race([
    promise,
    new Promise<T>((_, reject) => setTimeout(() => reject(new Error(timeoutMessage)), timeoutMs)),
  ]);
}

export type DetailTab =
  | 'overview'
  | 'inventory'
  | 'security'
  | 'eventlogs'
  | 'software'
  | 'patches'
  | 'remote'
  | 'terminal'
  | 'files'
  | 'performance'
  | 'messaging'
  | 'maintenance'
  | 'scripts';

interface TabDefinition {
  id: DetailTab;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
}

interface TabConfig {
  id: DetailTab;
  labelKey: string;
  icon: React.ComponentType<{ className?: string }>;
  fallback: string;
}

const tabConfig: TabConfig[] = [
  { id: 'overview', labelKey: 'deviceDetail.tabs.overview', icon: Activity, fallback: 'Overview' },
  { id: 'inventory', labelKey: 'deviceDetail.tabs.inventory', icon: Archive, fallback: 'Inventory' },
  { id: 'security', labelKey: 'deviceDetail.tabs.security', icon: Shield, fallback: 'Security' },
  { id: 'eventlogs', labelKey: 'deviceDetail.tabs.eventlogs', icon: Clock, fallback: 'Event Logs' },
  { id: 'software', labelKey: 'deviceDetail.tabs.software', icon: Package, fallback: 'Software' },
  { id: 'patches', labelKey: 'deviceDetail.tabs.patches', icon: ShieldCheck, fallback: 'Patches' },
  { id: 'remote', labelKey: 'deviceDetail.tabs.remoteDesktop', icon: Monitor, fallback: 'Remote Desktop' },
  { id: 'terminal', labelKey: 'deviceDetail.tabs.terminal', icon: TerminalIcon, fallback: 'Terminal' },
  { id: 'files', labelKey: 'deviceDetail.tabs.files', icon: FolderOpen, fallback: 'Files' },
  { id: 'performance', labelKey: 'deviceDetail.tabs.performance', icon: Gauge, fallback: 'Performance' },
  { id: 'messaging', labelKey: 'deviceDetail.tabs.messaging', icon: MessageSquare, fallback: 'Messaging' },
  { id: 'maintenance', labelKey: 'deviceDetail.tabs.maintenance', icon: Wrench, fallback: 'Maintenance' },
  { id: 'scripts', labelKey: 'deviceDetail.tabs.scripts', icon: Code2, fallback: 'Scripts' },
];
export default function DeviceDetail() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const { t } = useTranslation();
  const deviceId = id ?? '';
  const tabs = useMemo<TabDefinition[]>(
    () =>
      tabConfig.map((config) => ({
        id: config.id,
        icon: config.icon,
        label: t(config.labelKey, undefined, config.fallback),
      })),
    [t],
  );
  const terminalShellOptions = useMemo(
    () => [
      { value: 'powershell', label: t('terminal.shells.powershell') },
      { value: 'cmd', label: t('terminal.shells.cmd') },
      { value: 'admin-powershell', label: t('terminal.shells.adminPowershell') },
      { value: 'admin-cmd', label: t('terminal.shells.adminCmd') },
    ],
    [t],
  );

  const initialTab = (searchParams.get('tab') as DetailTab) || 'overview';

  const [activeTab, setActiveTab] = useState<DetailTab>(initialTab);
  const [device, setDevice] = useState<Device | null>(null);
  const [loadingDevice, setLoadingDevice] = useState(true);
  const [diagnostics, setDiagnostics] = useState<DiagnosticsState>({ loading: false });
  const [inventory, setInventory] = useState<InventoryState>({ loading: false });
  const [security, setSecurity] = useState<SecurityState>({ loading: false });
  const [eventLogs, setEventLogs] = useState<EventLogState>({ loading: false, items: [] });
  const [performance, setPerformance] = useState<PerformanceState>({ loading: false });
  const [remoteControl, setRemoteControl] = useState<RemoteControlState>({ busy: false, session: null });
  const [remoteClipboard, setRemoteClipboard] = useState<RemoteClipboardState>({
    value: '',
    loading: false,
    syncing: false,
  });
  const [remoteQuality, setRemoteQuality] = useState<QualityLevel>('medium');
  const [remoteFps, setRemoteFps] = useState(15);
  const [remoteShowSettings, setRemoteShowSettings] = useState(false);
  const [remoteFullscreen, setRemoteFullscreen] = useState(false);
  const [remoteRecording, setRemoteRecording] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const lastMouseMoveRef = useRef(0);
  const remoteSessionRef = useRef<RemoteDesktopSession | null>(null);
  const recordingRef = useRef<MediaRecorder | null>(null);
  const recordingChunksRef = useRef<Blob[]>([]);
  const [terminalState, setTerminalState] = useState<TerminalState>({
    shell: 'powershell',
    command: '',
    outputs: [],
    isRunning: false,
  });
  const [softwareState, setSoftwareState] = useState<SoftwareState>({
    loading: false,
    items: [],
  });
  const [patchState, setPatchState] = useState<PatchState>({
    loading: false,
    installed: [],
    pending: [],
  });
  const [patchForm, setPatchForm] = useState<PatchFormState>({
    patchUrl: '',
    scheduledTime: '',
  });
  const [messagingForm, setMessagingForm] = useState<MessagingFormState>({
    action: 'agentmsg',
    title: '',
    message: '',
    duration: 5000,
    sending: false,
  });
  const [maintenanceForm, setMaintenanceForm] = useState<MaintenanceFormState>({
    version: '',
    channel: '',
    force: false,
    installerUrl: '',
    preserveConfig: true,
    tailLines: 200,
    includeDiagnostics: false,
    busy: false,
    logResult: undefined,
  });
  const [scriptsState, setScriptsState] = useState<ScriptsState>({
    loading: false,
    scripts: [],
    handlers: [],
  });
  const [scriptForm, setScriptForm] = useState<ScriptFormState>({
    name: '',
    code: '',
    busy: false,
  });

  useEffect(() => {
    remoteSessionRef.current = remoteControl.session ?? null;
  }, [remoteControl.session]);

  useEffect(() => {
    return () => {
      const active = remoteSessionRef.current;
      if (active && deviceId) {
        remoteDesktopService.stopSession(deviceId, active.sessionId).catch(() => undefined);
      }
      if (recordingRef.current) {
        recordingRef.current.stop();
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
    if (!deviceId || !remoteControl.session) return;
    let cancelled = false;
    let timer: number | null = null;

    const pumpFrames = async () => {
      if (!remoteControl.session || cancelled) return;
      try {
        const frame = await remoteDesktopService.captureFrame(deviceId, remoteControl.session.sessionId);
        if (frame?.frameBase64 && !cancelled) {
          const width = frame.width ?? remoteControl.session.width ?? DEFAULT_REMOTE_RESOLUTION.width;
          const height = frame.height ?? remoteControl.session.height ?? DEFAULT_REMOTE_RESOLUTION.height;
          await drawFrameToCanvas(frame.frameBase64, width, height);
        }
      } catch (error) {
        console.error('Frame request failed:', error);
        setRemoteControl((prev) => ({
          ...prev,
          error: toErrorMessage(error, t('deviceDetail.remoteDesktop.errorStart')),
        }));
      } finally {
        if (!cancelled) {
          timer = window.setTimeout(pumpFrames, Math.max(1, Math.round(1000 / remoteFps)));
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
  }, [deviceId, remoteControl.session, remoteFps, drawFrameToCanvas, t]);

  useEffect(() => {
    const handleFullscreenChange = () => {
      setRemoteFullscreen(Boolean(document.fullscreenElement));
    };
    document.addEventListener('fullscreenchange', handleFullscreenChange);
    return () => document.removeEventListener('fullscreenchange', handleFullscreenChange);
  }, []);

  const refreshDeviceInfo = useCallback(async () => {
    if (!deviceId) return;
    setLoadingDevice(true);
    try {
      const data = await deviceService.getDeviceById(deviceId);
      setDevice(data);
    } catch (error) {
      console.error('Failed to load device info', error);
    } finally {
      setLoadingDevice(false);
    }
  }, [deviceId]);

  useEffect(() => {
    refreshDeviceInfo();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [deviceId]); // Only run when deviceId changes

  // Sync activeTab with URL when URL changes externally (e.g., browser back/forward)
  useEffect(() => {
    const urlTab = (searchParams.get('tab') as DetailTab) || 'overview';
    if (urlTab !== activeTab) {
      setActiveTab(urlTab);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchParams]);


  const handleTabChange = (tab: DetailTab) => {
    setActiveTab(tab);
    setSearchParams((params) => {
      params.set('tab', tab);
      return params;
    });
  };

  const handleRemoteToggleSettings = useCallback(() => {
    setRemoteShowSettings((prev) => !prev);
  }, []);

  const handleRemoteQualityChange = useCallback((value: QualityLevel) => {
    setRemoteQuality(value);
  }, []);

  const handleRemoteFpsChange = useCallback((value: number) => {
    setRemoteFps(value);
  }, []);

  const handleRemoteCapture = useCallback(async () => {
    if (!canvasRef.current) {
      setRemoteControl((prev) => ({ ...prev, error: t('desktop.captureNotImplemented') }));
      return;
    }
    try {
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `remote-desktop-${device?.hostname ?? deviceId}-${timestamp}.png`;
      await downloadCanvasImage(canvasRef.current, filename);
      setRemoteControl((prev) => ({ ...prev, error: undefined }));
    } catch (error) {
      setRemoteControl((prev) => ({
        ...prev,
        error: toErrorMessage(error, t('desktop.error')),
      }));
    }
  }, [device?.hostname, deviceId, t]);

  const handleRemoteToggleFullscreen = useCallback(() => {
    if (!canvasRef.current) return;
    if (!document.fullscreenElement) {
      canvasRef.current.requestFullscreen().catch(() => undefined);
    } else {
      document.exitFullscreen().catch(() => undefined);
    }
  }, []);

  const handleRemoteToggleRecording = useCallback(async () => {
    if (remoteRecording) {
      if (recordingRef.current) {
        recordingRef.current.stop();
      }
      return;
    }
    if (!canvasRef.current || !remoteControl.session) {
      setRemoteControl((prev) => ({
        ...prev,
        error: t('deviceDetail.remoteDesktop.recordingError'),
      }));
      return;
    }
    try {
      const stream = canvasRef.current.captureStream(Math.max(1, remoteFps));
      const recorder = new MediaRecorder(stream, { mimeType: 'video/webm;codecs=vp9' });
      recordingChunksRef.current = [];
      recorder.ondataavailable = (event) => {
        if (event.data.size) {
          recordingChunksRef.current.push(event.data);
        }
      };
      recorder.onstop = () => {
        const blob = new Blob(recordingChunksRef.current, { type: 'video/webm' });
        recordingChunksRef.current = [];
        if (blob.size > 0) {
          const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
          const filename = `remote-desktop-${device?.hostname ?? deviceId}-${timestamp}.webm`;
          downloadBlob(blob, filename);
        }
        recordingRef.current = null;
        setRemoteRecording(false);
      };
      recorder.start();
      recordingRef.current = recorder;
      setRemoteRecording(true);
    } catch (error) {
      recordingRef.current = null;
      recordingChunksRef.current = [];
      setRemoteRecording(false);
      setRemoteControl((prev) => ({
        ...prev,
        error: toErrorMessage(error, t('deviceDetail.remoteDesktop.recordingError')),
      }));
    }
  }, [remoteRecording, remoteControl.session, remoteFps, device?.hostname, deviceId, t]);

  const handleRemoteConnect = useCallback(async () => {
    if (!deviceId || remoteControl.busy) return;
    setRemoteControl((prev) => ({ ...prev, busy: true, error: undefined }));
    try {
      const started = await remoteDesktopService.startSession(deviceId, {
        quality: getQualityPercent(remoteQuality),
        fps: remoteFps,
      });
      const session: RemoteDesktopSession = {
        sessionId: started.sessionId,
        width: started.width || DEFAULT_REMOTE_RESOLUTION.width,
        height: started.height || DEFAULT_REMOTE_RESOLUTION.height,
        quality: remoteQuality,
        startedAt: new Date().toISOString(),
      };
      setRemoteControl({ busy: false, session, error: undefined });
      setRemoteFullscreen(false);
    } catch (error) {
      setRemoteControl((prev) => ({
        ...prev,
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.remoteDesktop.errorStart')),
      }));
    }
  }, [deviceId, remoteControl.busy, remoteQuality, remoteFps, t]);

  const handleRemoteDisconnect = useCallback(async () => {
    if (!deviceId || !remoteControl.session || remoteControl.busy) return;
    setRemoteControl((prev) => ({ ...prev, busy: true, error: undefined }));
    try {
      await remoteDesktopService.stopSession(deviceId, remoteControl.session.sessionId);
      if (document.fullscreenElement) {
        document.exitFullscreen().catch(() => undefined);
      }
      setRemoteShowSettings(false);
      if (recordingRef.current) {
        recordingRef.current.stop();
      }
      setRemoteRecording(false);
      setRemoteControl({ busy: false, session: null, error: undefined });
    } catch (error) {
      setRemoteControl((prev) => ({
        ...prev,
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.remoteDesktop.errorStop')),
      }));
    }
  }, [deviceId, remoteControl.busy, remoteControl.session, t]);

  const handleOpenRemoteWorkspace = useCallback(() => {
    if (!deviceId) return;
    window.open(`/devices/${deviceId}/desktop`, '_blank', 'noreferrer');
  }, [deviceId]);

  const handleOpenTerminalWorkspace = useCallback(() => {
    if (!deviceId) return;
    window.open(`/devices/${deviceId}/terminal`, '_blank', 'noreferrer');
  }, [deviceId]);

  const handleRemoteMouseMove = useCallback(
    (coords: { x: number; y: number }) => {
      if (!deviceId || !remoteControl.session) return;
      const now = Date.now();
      if (now - lastMouseMoveRef.current < 50) return;
      lastMouseMoveRef.current = now;
      remoteDesktopService.sendMouseMove(deviceId, remoteControl.session.sessionId, coords.x, coords.y).catch(() => undefined);
    },
    [deviceId, remoteControl.session],
  );

  const handleRemoteMouseButton = useCallback(
    (button: number, action: 'down' | 'up' | 'click') => {
      if (!deviceId || !remoteControl.session) return;
      remoteDesktopService.sendMouseButton(deviceId, remoteControl.session.sessionId, button, action).catch(() => undefined);
    },
    [deviceId, remoteControl.session],
  );

  const handleRemoteKeyEvent = useCallback(
    (keyCode: number, action: 'down' | 'up') => {
      if (!deviceId || !remoteControl.session) return;
      remoteDesktopService.sendKey(deviceId, remoteControl.session.sessionId, keyCode, action).catch(() => undefined);
    },
    [deviceId, remoteControl.session],
  );

  const handleRemoteShortcut = useCallback(
    async (shortcut: RemoteShortcutId) => {
      if (!deviceId || !remoteControl.session) return;
      try {
        const keys = REMOTE_KEY_COMBOS[shortcut];
        await remoteDesktopService.sendKeySequence(deviceId, remoteControl.session.sessionId, keys);
      } catch (error) {
        setRemoteControl((prev) => ({
          ...prev,
          error: toErrorMessage(error, t('deviceDetail.remoteDesktop.shortcutError')),
        }));
      }
    },
    [deviceId, remoteControl.session, t],
  );

  const handleClipboardChange = useCallback((value: string) => {
    setRemoteClipboard((prev) => ({ ...prev, value, error: undefined }));
  }, []);

  const handleClipboardRefresh = useCallback(async () => {
    if (!deviceId) return;
    setRemoteClipboard((prev) => ({ ...prev, loading: true, error: undefined }));
    try {
      const result = await remoteOpsService.runClipboardGet(deviceId);
      setRemoteClipboard({
        value: result.data ?? '',
        loading: false,
        syncing: false,
        error: result.error,
      });
    } catch (error) {
      setRemoteClipboard((prev) => ({
        ...prev,
        loading: false,
        error: toErrorMessage(error, t('deviceDetail.remoteDesktop.clipboardError')),
      }));
    }
  }, [deviceId, t]);

  const handleClipboardApply = useCallback(async () => {
    if (!deviceId || remoteClipboard.syncing) return;
    setRemoteClipboard((prev) => ({ ...prev, syncing: true, error: undefined }));
    try {
      await remoteOpsService.setClipboard(deviceId, remoteClipboard.value);
      setRemoteClipboard((prev) => ({ ...prev, syncing: false }));
    } catch (error) {
      setRemoteClipboard((prev) => ({
        ...prev,
        syncing: false,
        error: toErrorMessage(error, t('deviceDetail.remoteDesktop.clipboardError')),
      }));
    }
  }, [deviceId, remoteClipboard.value, remoteClipboard.syncing, t]);

  useEffect(() => {
    if (activeTab === 'remote' && deviceId) {
      handleClipboardRefresh();
    }
  }, [activeTab, deviceId, handleClipboardRefresh]);

  const handleRemotePowerAction = useCallback(
    async (action: 'restart' | 'shutdown' | 'sleep') => {
      if (!deviceId) return;
      const actionLabel =
        action === 'restart'
          ? t('deviceDetail.remoteDesktop.powerRestart')
          : action === 'shutdown'
            ? t('deviceDetail.remoteDesktop.powerShutdown')
            : t('deviceDetail.remoteDesktop.powerSleep');
      const confirmed = window.confirm(
        t('deviceDetail.remoteDesktop.powerConfirm', { action: actionLabel }, 'Send power command?'),
      );
      if (!confirmed) return;
      try {
        await remoteOpsService.sendPowerAction(deviceId, action);
        setRemoteControl((prev) => ({ ...prev, error: undefined }));
      } catch (error) {
        setRemoteControl((prev) => ({
          ...prev,
          error: toErrorMessage(error, t('deviceDetail.remoteDesktop.powerError')),
        }));
      }
    },
    [deviceId, t],
  );

  const handleTerminalShellChange = useCallback((value: string) => {
    setTerminalState((prev) => ({ ...prev, shell: value }));
  }, []);

  const handleTerminalCommandChange = useCallback((value: string) => {
    setTerminalState((prev) => ({ ...prev, command: value }));
  }, []);

  const handleTerminalClear = useCallback(() => {
    setTerminalState((prev) => ({ ...prev, outputs: [], error: undefined }));
  }, []);

  const handlePatchFormChange = useCallback((patch: Partial<PatchFormState>) => {
    setPatchForm((prev) => ({ ...prev, ...patch }));
  }, []);

  const handleTerminalExecute = useCallback(async () => {
    if (!deviceId || terminalState.isRunning) return;
    const trimmed = terminalState.command.trim();
    if (!trimmed) return;
    const shell = terminalState.shell;
    const timestamp = new Date().toLocaleTimeString();

    setTerminalState((prev) => ({ ...prev, isRunning: true, error: undefined }));
    try {
      const response = await commandService.executeAndWait<string>({
        deviceId,
        commandType: 'execute',
        parameters: { command: trimmed, shell },
      });
      const payload = response.data ?? response.command?.result ?? '';
      const formatted =
        typeof payload === 'string'
          ? payload
          : payload
            ? JSON.stringify(payload, null, 2)
            : '';
      const text = formatted && formatted !== 'undefined' ? formatted : '';
      const status = response.success ? 'success' : 'error';
      const entry = {
        id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
        cmd: trimmed,
        result:
          status === 'success'
            ? text || t('deviceDetail.terminal.runSuccess')
            : response.error ?? (text || t('deviceDetail.terminal.runError')),
        time: timestamp,
        status,
      } as TerminalState['outputs'][number];

      setTerminalState((prev) => ({
        shell: prev.shell,
        command: '',
        outputs: [...prev.outputs.slice(-49), entry],
        isRunning: false,
        error: status === 'success' ? undefined : (response.error ?? t('deviceDetail.terminal.runError')),
      }));
    } catch (error) {
      const message = toErrorMessage(error, t('deviceDetail.terminal.runError'));
      const entry = {
        id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
        cmd: trimmed,
        result: message,
        time: timestamp,
        status: 'error',
      } as TerminalState['outputs'][number];
      setTerminalState((prev) => ({
        ...prev,
        command: '',
        isRunning: false,
        error: message,
        outputs: [...prev.outputs.slice(-49), entry],
      }));
    }
  }, [deviceId, terminalState.command, terminalState.isRunning, terminalState.shell, t]);

  const loadSoftware = useCallback(async () => {
    if (!deviceId) return;
    setSoftwareState((prev) => ({ ...prev, loading: true, error: undefined, message: undefined }));
    try {
      // Get software from database
      const software = await inventoryService.getInstalledSoftware(deviceId);
      
      console.log('Software loaded from database:', software.length);
      setSoftwareState({
        loading: false,
        items: software || [],
        message: undefined,
      });
    } catch (error) {
      console.error('Software load error:', error);
      setSoftwareState({
        loading: false,
        items: [],
        error: toErrorMessage(error, t('deviceDetail.software.error')),
      });
    }
  }, [deviceId, t]);

  const handleSoftwareUninstall = useCallback(
    async (productName: string) => {
      if (!deviceId) return;
      setSoftwareState((prev) => ({ ...prev, actionTarget: productName, message: undefined, error: undefined }));
      try {
        await softwareService.uninstallSoftware(deviceId, { productName });
        setSoftwareState((prev) => ({
          ...prev,
          actionTarget: undefined,
          message: t('deviceDetail.software.uninstallQueued'),
        }));
        loadSoftware();
      } catch (error) {
        setSoftwareState((prev) => ({
          ...prev,
          actionTarget: undefined,
          error: toErrorMessage(error, t('deviceDetail.software.error')),
        }));
      }
    },
    [deviceId, loadSoftware, t],
  );

  const loadPatches = useCallback(async () => {
    if (!deviceId) return;
    setPatchState((prev) => ({ ...prev, loading: true, error: undefined, message: undefined }));
    try {
      // Get installed patches from database only
      const installedPatches = await inventoryService.getInstalledPatches(deviceId);
      
      console.log('Patches loaded - installed:', installedPatches.length);
      
      setPatchState({
        loading: false,
        installed: installedPatches || [],
        pending: [], // Pending updates not stored in DB, only visible in Inventory tab
      });
    } catch (error) {
      console.error('Patches load error:', error);
      setPatchState({
        loading: false,
        installed: [],
        pending: [],
        error: toErrorMessage(error, t('deviceDetail.patches.error')),
      });
    }
  }, [deviceId, t]);

  const handleInstallPendingUpdates = useCallback(async () => {
    if (!deviceId) return;
    setPatchState((prev) => ({ ...prev, busy: true, message: undefined, error: undefined }));
    try {
      await softwareService.installUpdates(deviceId);
      setPatchState((prev) => ({
        ...prev,
        busy: false,
        message: t('deviceDetail.patches.installQueued'),
      }));
      loadPatches();
    } catch (error) {
      setPatchState((prev) => ({
        ...prev,
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.patches.error')),
      }));
    }
  }, [deviceId, loadPatches, t]);

  const handleSchedulePatch = useCallback(async () => {
    if (!deviceId) return;
    if (!patchForm.patchUrl.trim()) {
      setPatchState((prev) => ({
        ...prev,
        error: t('deviceDetail.patches.patchUrlRequired'),
        message: undefined,
      }));
      return;
    }
    setPatchState((prev) => ({ ...prev, busy: true, message: undefined, error: undefined }));
    try {
      await softwareService.schedulePatch(deviceId, {
        patchUrl: patchForm.patchUrl.trim(),
        scheduledTime: patchForm.scheduledTime || new Date().toISOString(),
      });
      setPatchState((prev) => ({
        ...prev,
        busy: false,
        message: t('deviceDetail.patches.scheduleQueued'),
      }));
      setPatchForm((prev) => ({ ...prev, patchUrl: '', scheduledTime: '' }));
      loadPatches();
    } catch (error) {
      setPatchState((prev) => ({
        ...prev,
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.patches.error')),
      }));
    }
  }, [deviceId, loadPatches, patchForm.patchUrl, patchForm.scheduledTime, t]);

  const runMaintenanceAction = useCallback(
    async (action: () => Promise<string | undefined>) => {
      if (!deviceId) return;
      setMaintenanceForm((prev) => ({ ...prev, busy: true, message: undefined, error: undefined }));
      try {
        const message = (await action()) ?? t('deviceDetail.maintenance.genericSuccess');
        setMaintenanceForm((prev) => ({ ...prev, busy: false, message, error: undefined }));
      } catch (error) {
        setMaintenanceForm((prev) => ({
          ...prev,
          busy: false,
          error: toErrorMessage(error, t('deviceDetail.maintenance.error')),
        }));
      }
    },
    [deviceId, t],
  );

  const updateMessagingForm = useCallback((patch: Partial<MessagingFormState>) => {
    setMessagingForm((prev) => ({
      ...prev,
      ...patch,
      feedback: patch.feedback !== undefined ? patch.feedback : prev.feedback,
      error: patch.error !== undefined ? patch.error : prev.error,
    }));
  }, []);

  const handleMessagingSend = useCallback(async () => {
    if (!deviceId) return;
    setMessagingForm((prev) => ({ ...prev, sending: true, feedback: undefined, error: undefined }));
    try {
      const fallbackTitle = messagingForm.title || t('deviceDetail.messaging.defaultTitle');
      switch (messagingForm.action) {
        case 'agentmsg':
          await messagingService.sendAgentMessage(deviceId, { message: messagingForm.message });
          break;
        case 'messagebox':
          await messagingService.showMessageBox(deviceId, {
            title: fallbackTitle,
            message: messagingForm.message,
            type: 'info',
          });
          break;
        case 'notify':
          await messagingService.sendNotification(deviceId, {
            title: fallbackTitle,
            message: messagingForm.message,
          });
          break;
        case 'toast':
          await messagingService.showToast(deviceId, {
            title: fallbackTitle,
            message: messagingForm.message,
            duration: messagingForm.duration,
          });
          break;
        case 'chat':
          await messagingService.sendChatMessage(deviceId, { message: messagingForm.message });
          break;
        default:
          break;
      }
      setMessagingForm((prev) => ({
        ...prev,
        sending: false,
        feedback: t('deviceDetail.messaging.success'),
        error: undefined,
        message: '',
        title: '',
      }));
    } catch (error) {
      setMessagingForm((prev) => ({
        ...prev,
        sending: false,
        error: toErrorMessage(error, t('deviceDetail.messaging.error')),
      }));
    }
  }, [deviceId, messagingForm.action, messagingForm.duration, messagingForm.message, messagingForm.title, t]);

  const updateMaintenanceFields = useCallback((patch: Partial<MaintenanceFormState>) => {
    setMaintenanceForm((prev) => ({ ...prev, ...patch }));
  }, []);

  const updateScriptForm = useCallback((patch: Partial<ScriptFormState>) => {
    setScriptForm((prev) => ({ ...prev, ...patch }));
  }, []);

  const handleMaintenanceUpdate = useCallback(async () => {
    await runMaintenanceAction(async () => {
      const payload = {
        version: maintenanceForm.version || undefined,
        channel: maintenanceForm.channel || undefined,
        force: maintenanceForm.force,
      };
      const result = await maintenanceService.update(deviceId, payload);
      if (result.status !== 'Completed') throw new Error(result.result ?? t('deviceDetail.maintenance.error'));
      return t('deviceDetail.maintenance.updateQueued');
    });
  }, [deviceId, maintenanceForm.channel, maintenanceForm.force, maintenanceForm.version, runMaintenanceAction, t]);

  const handleMaintenanceReinstall = useCallback(async () => {
    await runMaintenanceAction(async () => {
      const result = await maintenanceService.reinstall(deviceId, {
        installerUrl: maintenanceForm.installerUrl || undefined,
        preserveConfig: maintenanceForm.preserveConfig,
      });
      if (result.status !== 'Completed') throw new Error(result.result ?? t('deviceDetail.maintenance.error'));
      return t('deviceDetail.maintenance.reinstallQueued');
    });
  }, [deviceId, maintenanceForm.installerUrl, maintenanceForm.preserveConfig, runMaintenanceAction, t]);

  const handleMaintenanceLogs = useCallback(async () => {
    await runMaintenanceAction(async () => {
      const result = await maintenanceService.collectLogs(deviceId, {
        tailLines: maintenanceForm.tailLines,
        includeDiagnostics: maintenanceForm.includeDiagnostics,
      });
      if (result.status !== 'Completed') throw new Error(result.result ?? t('deviceDetail.maintenance.error'));
      const text = result.result ?? t('deviceDetail.maintenance.logsQueued');
      setMaintenanceForm((prev) => ({ ...prev, logResult: text }));
      return t('deviceDetail.maintenance.logsQueued');
    });
  }, [
    deviceId,
    maintenanceForm.includeDiagnostics,
    maintenanceForm.tailLines,
    runMaintenanceAction,
    t,
  ]);

  const handlePrivacyShow = useCallback(async () => {
    await runMaintenanceAction(async () => {
      const result = await privacyService.showBar(deviceId);
      if (result.status !== 'Completed') throw new Error(result.result ?? t('deviceDetail.maintenance.error'));
      return t('deviceDetail.maintenance.privacyShown');
    });
  }, [deviceId, runMaintenanceAction, t]);

  const handlePrivacyHide = useCallback(async () => {
    await runMaintenanceAction(async () => {
      const result = await privacyService.hideBar(deviceId);
      if (result.status !== 'Completed') throw new Error(result.result ?? t('deviceDetail.maintenance.error'));
      return t('deviceDetail.maintenance.privacyHidden');
    });
  }, [deviceId, runMaintenanceAction, t]);

  const loadScripts = useCallback(async () => {
    if (!deviceId) return;
    setScriptsState((prev) => ({ ...prev, loading: true, error: undefined }));
    try {
      const result = await scriptsService.list(deviceId);
      setScriptsState({
        loading: false,
        scripts: result.data?.scripts ?? [],
        handlers: result.data?.handlers ?? [],
        error: result.error,
      });
    } catch (error) {
      setScriptsState({
        loading: false,
        scripts: [],
        handlers: [],
        error: toErrorMessage(error, t('deviceDetail.scripts.error')),
      });
    }
  }, [deviceId, t]);

  const handleScriptDeploy = useCallback(async () => {
    if (!deviceId) return;
    setScriptForm((prev) => ({ ...prev, busy: true, feedback: undefined, error: undefined }));
    try {
      const result = await scriptsService.deploy(deviceId, {
        name: scriptForm.name || undefined,
        code: scriptForm.code,
      });
      if (!result.success) throw new Error(result.error ?? 'Deploy failed');
      setScriptForm((prev) => ({ ...prev, busy: false, feedback: t('deviceDetail.scripts.deploySuccess'), code: '' }));
      loadScripts();
    } catch (error) {
      setScriptForm((prev) => ({
        ...prev,
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.scripts.error')),
      }));
    }
  }, [deviceId, loadScripts, scriptForm.code, scriptForm.name, t]);

  const handleScriptReload = useCallback(async () => {
    if (!deviceId) return;
    setScriptForm((prev) => ({ ...prev, busy: true, feedback: undefined, error: undefined }));
    try {
      const result = await scriptsService.reload(deviceId);
      if (!result.success) throw new Error(result.error ?? 'Reload failed');
      setScriptForm((prev) => ({ ...prev, busy: false, feedback: t('deviceDetail.scripts.reloadSuccess') }));
      loadScripts();
    } catch (error) {
      setScriptForm((prev) => ({
        ...prev,
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.scripts.error')),
      }));
    }
  }, [deviceId, loadScripts, t]);

  const handleScriptRemove = useCallback(
    async (name: string) => {
      if (!deviceId) return;
      setScriptsState((prev) => ({ ...prev, loading: true }));
      try {
        const result = await scriptsService.remove(deviceId, { name });
        if (!result.success) throw new Error(result.error ?? 'Remove failed');
        await loadScripts();
      } catch (error) {
        setScriptsState({
          loading: false,
          scripts: [],
          handlers: [],
          error: toErrorMessage(error, t('deviceDetail.scripts.error')),
        });
      }
    },
    [deviceId, loadScripts, t],
  );

  useEffect(() => {
    loadScripts();
  }, [loadScripts]);

  const runDiagnostics = useCallback(async () => {
    if (!deviceId) return;
    setDiagnostics((prev) => ({ ...prev, loading: true, error: undefined }));
    const timeoutSeconds = 15;
    const timeoutMessage = t(
      'deviceDetail.errors.timeout',
      { seconds: timeoutSeconds },
      'Request timed out. Agent may be offline or not responding.',
    );

    try {
      const diagnostics = await withTimeout<DiagnosticsBundle>(
        Promise.all([
          diagnosticsService.getStatus(deviceId),
          diagnosticsService.getAgentInfo(deviceId),
          diagnosticsService.getConnectionDetails(deviceId),
          diagnosticsService.ping(deviceId),
        ]),
        timeoutSeconds * 1000,
        timeoutMessage,
      );

      const [status, agentInfo, connectionDetails, pingResult] = diagnostics;

      setDiagnostics({
        loading: false,
        status: status.data,
        agentInfo: agentInfo.data,
        connectionDetails: connectionDetails.data,
        pingResult: pingResult.data,
      });
    } catch (error) {
      console.error('Diagnostics error:', error);
      setDiagnostics({
        loading: false,
        error: toErrorMessage(error, t('deviceDetail.errors.diagnostics', undefined, timeoutMessage)),
      });
    }
  }, [deviceId, t]);

  const loadInventory = useCallback(async () => {
    if (!deviceId || inventory.loading) return;
    setInventory({ loading: true });
    try {
      const result = await inventoryService.getFullInventory(deviceId);
      console.log('Inventory API Response:', result);
      console.log('Inventory result.data:', result.data);
      console.log('Inventory software count:', result.data?.software?.length);
      console.log('Inventory software sample:', result.data?.software?.slice(0, 3));
      
      if (!result.success || !result.data) {
        setInventory({
          loading: false,
          error: result.error ?? t('deviceDetail.errors.inventory'),
        });
      } else {
        setInventory({
          loading: false,
          data: result.data,
        });
      }
    } catch (error) {
      console.error('Inventory error:', error);
      setInventory({ loading: false, error: toErrorMessage(error, t('deviceDetail.errors.inventory')) });
    }
  }, [deviceId, inventory.loading, t]);

  const loadSecurity = useCallback(async () => {
    if (!deviceId || security.loading) return;
    setSecurity({ loading: true });
    try {
      const snapshot = await securityService.getSecurityStatus(deviceId);
      setSecurity({ loading: false, snapshot: snapshot.data ?? undefined });
    } catch (error) {
      setSecurity({ loading: false, error: toErrorMessage(error, t('deviceDetail.errors.security')) });
    }
  }, [deviceId, security.loading, t]);


  const loadEventLogs = useCallback(async () => {
    if (!deviceId) return;
    setEventLogs((prev) => {
      if (prev.loading) return prev; // Already loading, skip
      return { ...prev, loading: true, error: undefined };
    });
    try {
      const result = await eventLogsService.getAll(deviceId, { maxEvents: 100 });
      console.log('EventLogs API Response:', result);
      console.log('EventLogs result.data:', result.data);
      console.log('EventLogs result.data type:', typeof result.data);
      console.log('EventLogs is array?', Array.isArray(result.data));
      
      // Ensure items is always an array
      let items: any[] = [];
      if (Array.isArray(result.data)) {
        items = result.data;
      } else if (result.data && typeof result.data === 'object') {
        // Check if data is wrapped in an object (e.g., { events: [...] }, { items: [...] } or { logs: [...] })
        if (Array.isArray((result.data as any).events)) {
          items = (result.data as any).events;
        } else if (Array.isArray((result.data as any).items)) {
          items = (result.data as any).items;
        } else if (Array.isArray((result.data as any).logs)) {
          items = (result.data as any).logs;
        } else if (Array.isArray((result.data as any).entries)) {
          items = (result.data as any).entries;
        } else {
          console.warn('EventLogs data is object but no array found:', result.data);
        }
      }
      
      console.log('EventLogs final items:', items);
      console.log('EventLogs items count:', items.length);
      setEventLogs({ loading: false, items });
    } catch (error) {
      console.error('EventLogs error:', error);
      setEventLogs({ loading: false, items: [], error: toErrorMessage(error, t('deviceDetail.errors.eventLogs')) });
    }
  }, [deviceId, t]);

  const loadPerformance = useCallback(async () => {
    if (!deviceId || performance.loading) return;
    setPerformance((prev) => ({ ...prev, loading: true, error: undefined }));
    const timeoutSeconds = 15;
    const timeoutMessage = t(
      'deviceDetail.errors.timeout',
      { seconds: timeoutSeconds },
      'Request timed out. Agent may be offline or not responding.',
    );

    try {
      const metrics = await withTimeout(
        healthService.getMetrics(deviceId),
        timeoutSeconds * 1000,
        timeoutMessage,
      );

      setPerformance({ loading: false, metrics: metrics.data });
    } catch (error) {
      console.error('Performance error:', error);
      setPerformance({
        loading: false,
        error: toErrorMessage(error, t('deviceDetail.errors.performance', undefined, timeoutMessage)),
      });
    }
  }, [deviceId, performance.loading, t]);

  useEffect(() => {
    if (!deviceId) return;
    switch (activeTab) {
      case 'overview':
        runDiagnostics();
        loadPerformance();
        break;
      case 'inventory':
        loadInventory();
        break;
      case 'security':
        loadSecurity();
        break;
      case 'eventlogs':
        loadEventLogs();
        break;
      case 'software':
        loadSoftware();
        break;
      case 'patches':
        loadPatches();
        break;
      case 'performance':
        loadPerformance();
        break;
      default:
        break;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, deviceId]); // Only depend on tab and deviceId changes, not on the functions themselves

  const statusBadge = useMemo(() => {
    if (!device) return null;
    const color =
      device.status === 'Connected'
        ? 'bg-emerald-500'
        : device.status === 'Error'
          ? 'bg-destructive'
          : device.status === 'Connecting'
            ? 'bg-blue-400'
            : 'bg-muted';
    const statusLabel = t(`devices.statusLabels.${device.status}`, undefined, device.status);
    return (
      <span className="inline-flex items-center gap-2 rounded-full bg-secondary px-3 py-1 text-xs font-medium text-muted-foreground">
        <span className={`h-2 w-2 rounded-full ${color}`} />
        {statusLabel}
      </span>
    );
  }, [device, t]);

  if (!deviceId) {
    return (
      <div className="flex h-full items-center justify-center">
        <p className="text-muted-foreground">{t('deviceDetail.messages.missingId')}</p>
      </div>
    );
  }

  if (loadingDevice) {
    return (
      <div className="flex h-full items-center justify-center gap-3 text-muted-foreground">
        <RefreshCw className="h-5 w-5 animate-spin" />
        {t('deviceDetail.messages.loading')}
      </div>
    );
  }

  if (!device) {
    return (
      <div className="flex h-full flex-col items-center justify-center gap-4 text-center">
        <p className="text-lg font-semibold text-muted-foreground">{t('deviceDetail.messages.notFound')}</p>
        <button
          onClick={() => navigate('/devices')}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90"
        >
          <ArrowLeft className="h-4 w-4" />
          {t('deviceDetail.back')}
        </button>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      <div className="border-b border-border bg-card">
        <div className="flex items-center justify-between px-6 py-4">
          <div className="flex items-center gap-4">
            <button
              onClick={() => navigate('/devices')}
              className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70"
            >
              <ArrowLeft className="mr-2 inline h-4 w-4" />
              {t('deviceDetail.messages.back')}
            </button>
            <div>
              <h1 className="text-2xl font-semibold">{device.hostname}</h1>
              <div className="mt-2 flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
                {statusBadge}
                <span className="flex items-center gap-2">
                  <Server className="h-4 w-4" />
                  {device.domain ?? t('deviceDetail.messages.noDomain')}
                </span>
                <span className="flex items-center gap-2">
                  <Cpu className="h-4 w-4" />
                  {device.osVersion ?? t('deviceDetail.messages.unknownOs')}
                </span>
                <span className="flex items-center gap-2">
                  <Wifi className="h-4 w-4" />
                  {device.ipAddress ?? t('deviceDetail.messages.noIp')}
                </span>
              </div>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={refreshDeviceInfo}
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/80"
            >
              <RefreshCw className="h-4 w-4" />
              {t('deviceDetail.header.refresh')}
            </button>
            <button
              onClick={() => {
                handleTabChange('remote');
                handleRemoteConnect();
              }}
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90"
            >
              <Monitor className="h-4 w-4" />
              {t('deviceDetail.header.openDesktop')}
            </button>
            <button
              onClick={() => handleTabChange('terminal')}
              className="inline-flex items-center gap-2 rounded-lg bg-secondary px-4 py-2 text-sm font-medium text-foreground transition hover:bg-secondary/80"
            >
              <TerminalIcon className="h-4 w-4" />
              {t('deviceDetail.header.openTerminal')}
            </button>
          </div>
        </div>

        <div className="flex h-14 items-center gap-1 overflow-x-auto px-4">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = tab.id === activeTab;
            return (
              <button
                key={tab.id}
                onClick={() => handleTabChange(tab.id)}
                className={`inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition ${
                  isActive ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:bg-secondary/70'
                }`}
              >
                <Icon className="h-4 w-4" />
                {tab.label}
              </button>
            );
          })}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto bg-muted/10 p-6">
        {activeTab === 'overview' && (
          <OverviewTab
            diagnostics={diagnostics}
            onRefresh={runDiagnostics}
            device={device}
            performance={performance}
            onRefreshPerformance={loadPerformance}
          />
        )}
        {activeTab === 'remote' && (
          <RemoteDesktopTab
            control={remoteControl}
            canvasRef={canvasRef}
            quality={remoteQuality}
            fps={remoteFps}
            showSettings={remoteShowSettings}
            isRecording={remoteRecording}
            isFullscreen={remoteFullscreen}
            onQualityChange={handleRemoteQualityChange}
            onFpsChange={handleRemoteFpsChange}
            onConnect={handleRemoteConnect}
            onDisconnect={handleRemoteDisconnect}
            onToggleSettings={handleRemoteToggleSettings}
            onToggleRecording={handleRemoteToggleRecording}
            onToggleFullscreen={handleRemoteToggleFullscreen}
            onOpenWorkspace={handleOpenRemoteWorkspace}
            onMouseMove={handleRemoteMouseMove}
            onMouseButton={handleRemoteMouseButton}
            onKeyEvent={handleRemoteKeyEvent}
            onCapture={handleRemoteCapture}
            clipboard={remoteClipboard}
            onClipboardChange={handleClipboardChange}
            onClipboardRefresh={handleClipboardRefresh}
            onClipboardApply={handleClipboardApply}
            onPowerAction={handleRemotePowerAction}
            onSendShortcut={handleRemoteShortcut}
          />
        )}
        {activeTab === 'inventory' && (
          <InventoryTab state={inventory} onRefresh={loadInventory} />
        )}
        {activeTab === 'security' && (
          <SecurityTab state={security} onRefresh={loadSecurity} />
        )}
        {activeTab === 'eventlogs' && (
          <EventLogsTab state={eventLogs} onRefresh={loadEventLogs} deviceId={device.id} />
        )}
        {activeTab === 'software' && (
          <SoftwareTab state={softwareState} onRefresh={loadSoftware} onUninstall={handleSoftwareUninstall} />
        )}
        {activeTab === 'patches' && (
          <PatchesTab
            state={patchState}
            form={patchForm}
            onFormChange={handlePatchFormChange}
            onRefresh={loadPatches}
            onInstallPending={handleInstallPendingUpdates}
            onSchedulePatch={handleSchedulePatch}
          />
        )}
        {activeTab === 'terminal' && (
          <TerminalTab
            state={terminalState}
            shellOptions={terminalShellOptions}
            onCommandChange={handleTerminalCommandChange}
            onShellChange={handleTerminalShellChange}
            onExecute={handleTerminalExecute}
            onClear={handleTerminalClear}
            onOpenStandalone={handleOpenTerminalWorkspace}
          />
        )}
        {activeTab === 'files' && <FilesTab deviceId={device.id} />}
        {activeTab === 'performance' && (
          <PerformanceTab state={performance} onRefresh={loadPerformance} deviceId={device.id} />
        )}
        {activeTab === 'messaging' && (
          <MessagingTab
            state={messagingForm}
            onChange={updateMessagingForm}
            onSend={handleMessagingSend}
            disabled={!deviceId}
          />
        )}
        {activeTab === 'maintenance' && (
          <MaintenanceTab
            state={maintenanceForm}
            onChange={updateMaintenanceFields}
            onUpdate={handleMaintenanceUpdate}
            onReinstall={handleMaintenanceReinstall}
            onCollectLogs={handleMaintenanceLogs}
            onShowPrivacy={handlePrivacyShow}
            onHidePrivacy={handlePrivacyHide}
          />
        )}
        {activeTab === 'scripts' && (
          <ScriptsTab
            state={scriptsState}
            form={scriptForm}
            onFormChange={updateScriptForm}
            onDeploy={handleScriptDeploy}
            onReload={handleScriptReload}
            onRefresh={loadScripts}
            onRemove={handleScriptRemove}
          />
        )}
      </div>
    </div>
  );
}
  const handleRemoteToggleRecording = useCallback(async () => {
    if (remoteRecording) {
      if (recordingRef.current) {
        recordingRef.current.stop();
      }
      return;
    }
    if (!canvasRef.current || !remoteControl.session) {
      setRemoteControl((prev) => ({
        ...prev,
        error: t('deviceDetail.remoteDesktop.recordingError'),
      }));
      return;
    }
    try {
      const stream = canvasRef.current.captureStream(Math.max(1, remoteFps));
      const recorder = new MediaRecorder(stream, { mimeType: 'video/webm;codecs=vp9' });
      recordingChunksRef.current = [];
      recorder.ondataavailable = (event) => {
        if (event.data.size) {
          recordingChunksRef.current.push(event.data);
        }
      };
      recorder.onstop = () => {
        const blob = new Blob(recordingChunksRef.current, { type: 'video/webm' });
        recordingChunksRef.current = [];
        if (blob.size > 0) {
          const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
          const filename = `remote-desktop-${device?.hostname ?? deviceId}-${timestamp}.webm`;
          downloadBlob(blob, filename);
        }
        recordingRef.current = null;
        setRemoteRecording(false);
      };
      recorder.start();
      recordingRef.current = recorder;
      setRemoteRecording(true);
    } catch (error) {
      recordingRef.current = null;
      recordingChunksRef.current = [];
      setRemoteRecording(false);
      setRemoteControl((prev) => ({
        ...prev,
        error: toErrorMessage(error, t('deviceDetail.remoteDesktop.recordingError')),
      }));
    }
  }, [remoteRecording, remoteControl.session, remoteFps, device?.hostname, deviceId, t]);

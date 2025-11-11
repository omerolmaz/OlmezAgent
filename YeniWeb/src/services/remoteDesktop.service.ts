import { apiService } from './api.service';
import { commandService } from './command.service';
import { DEFAULT_REMOTE_RESOLUTION } from '../constants/remoteDesktop';

export interface RemoteDesktopStartOptions {
  quality: number;
  fps: number;
}

export interface RemoteDesktopSessionInfo {
  sessionId: string;
  width: number;
  height: number;
  quality: number;
}

export interface RemoteDesktopFrame {
  sessionId: string;
  frameBase64: string;
  size: number;
  width?: number;
  height?: number;
}

const FRAME_TIMEOUT_MS = 20000;

export const remoteDesktopService = {
  async startSession(deviceId: string, options: RemoteDesktopStartOptions): Promise<RemoteDesktopSessionInfo> {
    const sessionId =
      typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(16).slice(2)}`;

    const targetQuality = options.quality;
    const payload = {
      sessionId,
      quality: targetQuality,
      fps: options.fps,
    };

    const result = await commandService.executeAndWait<RemoteDesktopSessionInfo>(
      {
        deviceId,
        commandType: 'desktopstart',
        parameters: payload,
        sessionId,
      },
      { timeoutMs: FRAME_TIMEOUT_MS },
    );

    if (!result.success || !result.data) {
      throw new Error(result.error ?? 'Desktop session could not be started.');
    }

    return {
      sessionId: result.data.sessionId ?? sessionId,
      width: result.data.width ?? DEFAULT_REMOTE_RESOLUTION.width,
      height: result.data.height ?? DEFAULT_REMOTE_RESOLUTION.height,
      quality: result.data.quality ?? targetQuality,
    };
  },

  async stopSession(deviceId: string, sessionId: string): Promise<void> {
    await commandService.executeAndWait(
      {
        deviceId,
        commandType: 'desktopstop',
        parameters: { sessionId },
        sessionId,
      },
      { timeoutMs: FRAME_TIMEOUT_MS },
    );
  },

  async captureFrame(deviceId: string, sessionId: string): Promise<RemoteDesktopFrame | undefined> {
    const result = await commandService.executeAndWait<RemoteDesktopFrame>(
      {
        deviceId,
        commandType: 'desktopframe',
        parameters: { sessionId },
        sessionId,
      },
      { timeoutMs: FRAME_TIMEOUT_MS },
    );

    if (!result.success || !result.data?.frameBase64) {
      return undefined;
    }

    return result.data;
  },

  async sendMouseMove(deviceId: string, sessionId: string, x: number, y: number) {
    return apiService.post('/remote-desktop/input/move', { deviceId, sessionId, x, y });
  },

  async sendMouseButton(deviceId: string, sessionId: string, button: number, action: 'down' | 'up' | 'click') {
    return apiService.post('/remote-desktop/input/button', { deviceId, sessionId, button, action });
  },

  async sendKey(deviceId: string, sessionId: string, keyCode: number, action: 'down' | 'up' | 'press') {
    return apiService.post('/remote-desktop/input/key', { deviceId, sessionId, keyCode, action });
  },

  async sendKeySequence(deviceId: string, sessionId: string, keys: number[]) {
    for (const key of keys) {
      // eslint-disable-next-line no-await-in-loop
      await remoteDesktopService.sendKey(deviceId, sessionId, key, 'down');
    }
    for (const key of [...keys].reverse()) {
      // eslint-disable-next-line no-await-in-loop
      await remoteDesktopService.sendKey(deviceId, sessionId, key, 'up');
    }
  },
};

export default remoteDesktopService;


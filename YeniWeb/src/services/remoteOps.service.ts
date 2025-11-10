import { apiService } from './api.service';
import { commandService } from './command.service';
import type { CommandResultPayload } from '../types/command.types';

export interface FileEntry {
  name: string;
  type: 'file' | 'directory';
  size?: number;
  modifiedAt?: string;
}

export const remoteOpsService = {
  async listDirectory(deviceId: string, path: string): Promise<CommandResultPayload<FileEntry[]>> {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/ls/${deviceId}`, { path });
    const command = await commandService.waitForCommand(response.id, { timeoutMs: 20000 });
    
    let parsed: FileEntry[] = [];
    if (command.status === 'Completed' && command.result) {
      try {
        const result = JSON.parse(command.result);
        // Yeni format: { entries: [...] } veya eski format: [...]
        const entries = result.entries || result;
        
        // Ensure the result is an array
        if (Array.isArray(entries)) {
          parsed = entries.map((item: any) => ({
            name: item.name,
            type: item.type === 'directory' ? 'directory' : 'file',
            size: item.size || item.length,
            modifiedAt: item.modifiedAt || item.created
          }));
        }
      } catch (err) {
        console.error('Failed to parse directory listing:', err);
        parsed = [];
      }
    }

    return {
      success: command.status === 'Completed',
      data: parsed,
      error: command.status !== 'Completed' ? command.result ?? 'Files could not be retrieved' : undefined,
      command,
    };
  },

  async deletePath(deviceId: string, path: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/rm/${deviceId}`, { path });
    return commandService.waitForCommand(response.id);
  },

  async createDirectory(deviceId: string, path: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/mkdir/${deviceId}`, { path });
    return commandService.waitForCommand(response.id);
  },

  async sendPowerAction(deviceId: string, action: 'restart' | 'shutdown' | 'sleep') {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/power/${deviceId}`, { action });
    return commandService.waitForCommand(response.id);
  },

  async manageService(deviceId: string, serviceName: string, action: 'start' | 'stop' | 'restart') {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/service/${deviceId}`, {
      serviceName,
      action,
    });
    return commandService.waitForCommand(response.id);
  },
  
  async zip(deviceId: string, sourcePath: string, destinationPath: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/zip/${deviceId}`, {
      sourcePath,
      destinationPath,
    });
    return commandService.waitForCommand(response.id);
  },

  async unzip(deviceId: string, sourcePath: string, destinationPath: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/unzip/${deviceId}`, {
      sourcePath,
      destinationPath,
    });
    return commandService.waitForCommand(response.id);
  },

  async openUrl(deviceId: string, url: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/openurl/${deviceId}`, { url });
    return commandService.waitForCommand(response.id);
  },

  async wakeOnLan(deviceId: string, macAddress: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/wakeonlan/${deviceId}`, { macAddress });
    return commandService.waitForCommand(response.id);
  },

  async runClipboardGet(deviceId: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/clipboard/get/${deviceId}`);
    const command = await commandService.waitForCommand(response.id);
    return {
      success: command.status === 'Completed',
      data: command.result,
      error: command.status !== 'Completed' ? command.result ?? 'Clipboard request failed' : undefined,
      command,
    } as CommandResultPayload<string>;
  },

  async setClipboard(deviceId: string, content: string) {
    const response = await apiService.post<{ id: string }>(`/api/remoteops/clipboard/set/${deviceId}`, { content });
    return commandService.waitForCommand(response.id);
  },
};

export default remoteOpsService;


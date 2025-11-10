import { apiService } from './api.service';
import { commandService } from './command.service';
import type { CommandExecution } from '../types/command.types';

export interface MaintenanceUpdatePayload {
  version?: string;
  channel?: string;
  force?: boolean;
}

export interface MaintenanceReinstallPayload {
  installerUrl?: string;
  preserveConfig?: boolean;
}

export interface MaintenanceLogPayload {
  tailLines?: number;
  includeDiagnostics?: boolean;
}

export interface MaintenanceDownloadPayload {
  sourceUrl: string;
  destinationPath: string;
}

function normalizePayload(payload?: object) {
  if (!payload) return undefined;
  return { ...payload };
}

async function postMaintenanceCommand(route: string, deviceId: string, payload?: object): Promise<CommandExecution> {
  const response = await apiService.post<{ id: string }>(`/api/maintenance/${route}/${deviceId}`, normalizePayload(payload));
  return commandService.waitForCommand(response.id);
}

export const maintenanceService = {
  update(deviceId: string, payload: MaintenanceUpdatePayload) {
    return postMaintenanceCommand('update', deviceId, payload);
  },
  reinstall(deviceId: string, payload: MaintenanceReinstallPayload) {
    return postMaintenanceCommand('reinstall', deviceId, payload);
  },
  collectLogs(deviceId: string, payload: MaintenanceLogPayload) {
    return postMaintenanceCommand('logs', deviceId, payload);
  },
  downloadFile(deviceId: string, payload: MaintenanceDownloadPayload) {
    return postMaintenanceCommand('download', deviceId, payload);
  },
};

export default maintenanceService;

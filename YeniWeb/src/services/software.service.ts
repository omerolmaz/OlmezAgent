import { apiService } from './api.service';
import { commandService } from './command.service';
import type { CommandExecution } from '../types/command.types';

interface InstallPayload {
  packagePath: string;
  arguments?: string;
}

interface UninstallPayload {
  productName: string;
}

interface UpdatePayload {
  updateIds?: string[];
}

interface PatchSchedulePayload {
  patchUrl: string;
  scheduledTime: string;
}

async function executeAndTrack(
  endpoint: string,
  payload: unknown,
  timeoutMs = 30000,
): Promise<CommandExecution> {
  const response = await apiService.post<{ id: string }>(endpoint, payload);
  return commandService.waitForCommand(response.id, { timeoutMs });
}

export const softwareService = {
  installSoftware(deviceId: string, payload: InstallPayload) {
    return executeAndTrack(`/api/software/install/${deviceId}`, payload);
  },
  uninstallSoftware(deviceId: string, payload: UninstallPayload) {
    return executeAndTrack(`/api/software/uninstall/${deviceId}`, payload);
  },
  installUpdates(deviceId: string, payload?: UpdatePayload) {
    return executeAndTrack(`/api/software/updates/install/${deviceId}`, payload ?? {});
  },
  schedulePatch(deviceId: string, payload: PatchSchedulePayload) {
    return executeAndTrack(`/api/software/patch/schedule/${deviceId}`, payload);
  },
};

export default softwareService;


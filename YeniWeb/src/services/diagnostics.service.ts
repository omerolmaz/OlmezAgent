import { apiService } from './api.service';
import { commandService } from './command.service';
import type { CommandResultPayload } from '../types/command.types';

type DiagnosticCommand = 'ping' | 'status' | 'agentinfo' | 'versions' | 'connectiondetails';

async function runDiagnostic<T = Record<string, unknown>>(
  deviceId: string,
  command: DiagnosticCommand,
): Promise<CommandResultPayload<T>> {
  const response = await apiService.post<{ id: string }>(`/api/diagnostics/${command}/${deviceId}`);
  const commandRecord = await commandService.waitForCommand(response.id, { timeoutMs: 10000, pollIntervalMs: 500 });
  const parsed = commandRecord.result ? (JSON.parse(commandRecord.result) as T) : undefined;

  return {
    success: commandRecord.status === 'Completed',
    data: parsed,
    error: commandRecord.status !== 'Completed' ? commandRecord.result ?? 'Command failed' : undefined,
    command: commandRecord,
  };
}

export const diagnosticsService = {
  ping(deviceId: string) {
    return runDiagnostic<{ latencyMs?: number }>(deviceId, 'ping');
  },
  getStatus(deviceId: string) {
    return runDiagnostic(deviceId, 'status');
  },
  getAgentInfo(deviceId: string) {
    return runDiagnostic(deviceId, 'agentinfo');
  },
  getVersions(deviceId: string) {
    return runDiagnostic(deviceId, 'versions');
  },
  getConnectionDetails(deviceId: string) {
    return runDiagnostic(deviceId, 'connectiondetails');
  },
};

export default diagnosticsService;



import { apiService } from './api.service';
import { commandService } from './command.service';
import type { CommandResultPayload } from '../types/command.types';

export interface EventLogEntry {
  source?: string;
  level?: string;
  message?: string;
  loggedAt?: string;
  eventId?: number;
  user?: string;
}

interface EventLogRequest {
  logName?: string;
  maxEvents?: number;
  since?: string;
}

export interface EventMonitorOptions {
  logName: string;
  monitorId: string;
}

export interface StopMonitorOptions {
  monitorId: string;
}

export interface ClearLogOptions {
  logName: string;
}

async function runEventLogCommand<T = EventLogEntry[]>(
  deviceId: string,
  route: string,
  body?: EventLogRequest,
  timeoutMs = 20000,
) {
  const payload = body ?? { maxEvents: 100 };
  const response = await apiService.post<{ id: string }>(`/api/eventlog/${route}/${deviceId}`, payload);
  const command = await commandService.waitForCommand(response.id, { timeoutMs });
  const parsed = command.result ? (JSON.parse(command.result) as T) : undefined;

  return {
    success: command.status === 'Completed',
    data: parsed,
    error: command.status !== 'Completed' ? command.result ?? 'Event log request failed' : undefined,
    command,
  } as CommandResultPayload<T>;
}

export const eventLogsService = {
  getAll(deviceId: string, payload?: EventLogRequest) {
    return runEventLogCommand(deviceId, 'get', payload);
  },
  getSecurity(deviceId: string, payload?: EventLogRequest) {
    return runEventLogCommand(deviceId, 'security', payload);
  },
  getApplication(deviceId: string, payload?: EventLogRequest) {
    return runEventLogCommand(deviceId, 'application', payload);
  },
  getSystem(deviceId: string, payload?: EventLogRequest) {
    return runEventLogCommand(deviceId, 'system', payload);
  },
  async startMonitor(deviceId: string, payload: EventMonitorOptions) {
    const response = await apiService.post<{ id: string }>(`/api/eventlog/monitor/start/${deviceId}`, payload);
    return commandService.waitForCommand(response.id);
  },
  async stopMonitor(deviceId: string, payload: StopMonitorOptions) {
    const response = await apiService.post<{ id: string }>(`/api/eventlog/monitor/stop/${deviceId}`, payload);
    return commandService.waitForCommand(response.id);
  },
  async clearLog(deviceId: string, payload: ClearLogOptions) {
    const response = await apiService.post<{ id: string }>(`/api/eventlog/clear/${deviceId}`, payload);
    return commandService.waitForCommand(response.id);
  },
};

export default eventLogsService;


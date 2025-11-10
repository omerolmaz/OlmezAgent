import { apiService } from './api.service';
import type {
  ActiveConnectionsSnapshot,
  CommandExecution,
  CommandResultPayload,
  CommandStatus,
  ExecuteCommandRequest,
  ExecuteCommandResponse,
} from '../types/command.types';

interface CommandEnvelope<T> {
  success: boolean;
  data: T;
  error?: string;
}

const TERMINAL_STATUSES: CommandStatus[] = ['Success', 'Completed', 'Failed', 'Error', 'Cancelled'];

async function pollCommandUntilFinished(commandId: string, timeoutMs = 25000, pollIntervalMs = 1000) {
  const startedAt = Date.now();
  let attempt = 0;

  while (Date.now() - startedAt < timeoutMs) {
    const command = await commandService.getCommandById(commandId);
    if (TERMINAL_STATUSES.includes(command.status)) {
      return command;
    }

    attempt += 1;
    await new Promise((resolve) => setTimeout(resolve, pollIntervalMs + Math.min(attempt * 100, 1000)));
  }

  throw new Error('Command timed out');
}

function tryParseResult<T>(result?: string | null): T | undefined {
  if (!result) return undefined;
  try {
    return JSON.parse(result) as T;
  } catch {
    return undefined;
  }
}

export const commandService = {
  async getCommandById(id: string): Promise<CommandExecution> {
    try {
      const response = await apiService.get<CommandExecution>(`/api/commands/${id}`);
      return response;
    } catch (error) {
      console.error('Error fetching command:', error);
      throw error;
    }
  },

  async getCommandsByDevice(deviceId: string): Promise<CommandExecution[]> {
    try {
      const response = await apiService.get<CommandExecution[]>(`/api/commands/device/${deviceId}`);
      return response;
    } catch (error) {
      console.error('Error fetching commands:', error);
      throw error;
    }
  },

  async executeCommand(request: ExecuteCommandRequest): Promise<ExecuteCommandResponse> {
    try {
      const payload = {
        deviceId: request.deviceId,
        commandType: request.commandType,
        parameters:
          typeof request.parameters === 'string'
            ? request.parameters
            : request.parameters
              ? JSON.stringify(request.parameters)
              : null,
        sessionId: request.sessionId ?? null,
      };

      const response = await apiService.post<ExecuteCommandResponse>('/api/commands/execute', payload);
      return response;
    } catch (error) {
      console.error('Error executing command:', error);
      throw error;
    }
  },

  async executeAndWait<T = unknown>(
    request: ExecuteCommandRequest,
    options: { timeoutMs?: number; pollIntervalMs?: number } = {},
  ): Promise<CommandResultPayload<T>> {
    const { timeoutMs = 25000, pollIntervalMs = 1000 } = options;
    const command = await commandService.executeCommand(request);
    const completed = await pollCommandUntilFinished(command.id, timeoutMs, pollIntervalMs);
    const parsedResult = tryParseResult<T>(completed.result);

    const isSuccessful = completed.status === 'Completed' || completed.status === 'Success';
    return {
      success: isSuccessful,
      data: parsedResult,
      error: isSuccessful ? undefined : completed.result ?? 'Command failed',
      command: completed,
    };
  },

  async getActiveConnections(): Promise<ActiveConnectionsSnapshot> {
    try {
      const response = await apiService.get<CommandEnvelope<ActiveConnectionsSnapshot>>('/api/commands/active');
      return response.data;
    } catch (error) {
      console.error('Error fetching active connections:', error);
      throw error;
    }
  },

  async getDeviceConnectionStatus(deviceId: string): Promise<CommandEnvelope<{ deviceId: string; isConnected: boolean; timestamp: string }>> {
    try {
      const response = await apiService.get<CommandEnvelope<{ deviceId: string; isConnected: boolean; timestamp: string }>>(
        `/api/commands/status/${deviceId}`,
      );
      return response;
    } catch (error) {
      console.error('Error checking device status:', error);
      throw error;
    }
  },

  async waitForCommand(commandId: string, options: { timeoutMs?: number; pollIntervalMs?: number } = {}) {
    const { timeoutMs = 25000, pollIntervalMs = 1000 } = options;
    return pollCommandUntilFinished(commandId, timeoutMs, pollIntervalMs);
  },
};

export default commandService;

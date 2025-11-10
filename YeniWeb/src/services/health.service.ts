import { apiService } from './api.service';
import { commandService } from './command.service';
import type { PerformanceMetrics } from '../types/device.types';
import type { CommandResultPayload } from '../types/command.types';

export const healthService = {
  async getMetrics(deviceId: string): Promise<CommandResultPayload<PerformanceMetrics>> {
    const response = await apiService.post<{ id: string }>(`/api/health/metrics/${deviceId}`);
    const command = await commandService.waitForCommand(response.id, { timeoutMs: 10000, pollIntervalMs: 500 });
    const parsed = command.result ? (JSON.parse(command.result) as PerformanceMetrics) : undefined;

    return {
      success: command.status === 'Completed',
      data: parsed,
      error: command.status !== 'Completed' ? command.result ?? 'Metrics request failed' : undefined,
      command,
    };
  },

  async getHealthCheck(deviceId: string) {
    const response = await apiService.post<{ id: string }>(`/api/health/check/${deviceId}`);
    const command = await commandService.waitForCommand(response.id, { timeoutMs: 10000, pollIntervalMs: 500 });
    const parsed = command.result ? JSON.parse(command.result) : undefined;
    return {
      success: command.status === 'Completed',
      data: parsed,
      error: command.status !== 'Completed' ? command.result ?? 'Health info request failed' : undefined,
      command,
    } as CommandResultPayload<Record<string, unknown>>;
  },

  async getUptime(deviceId: string) {
    const response = await apiService.post<{ id: string }>(`/api/health/uptime/${deviceId}`);
    const command = await commandService.waitForCommand(response.id, { timeoutMs: 10000, pollIntervalMs: 500 });
    const parsed = command.result ? JSON.parse(command.result) : undefined;
    return {
      success: command.status === 'Completed',
      data: parsed,
      error: command.status !== 'Completed' ? command.result ?? 'Uptime request failed' : undefined,
      command,
    } as CommandResultPayload<{ uptimeSeconds: number }>;
  },
};

export default healthService;



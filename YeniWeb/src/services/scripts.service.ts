import { apiService } from './api.service';
import { commandService } from './command.service';
import type { CommandResultPayload } from '../types/command.types';

export interface ScriptDeployRequest {
  name?: string;
  code?: string;
  codeBase64?: string;
}

export interface ScriptRemoveRequest {
  name: string;
}

export interface ScriptListPayload {
  handlers?: string[];
  scripts?: string[];
  ack?: boolean;
  name?: string;
}

async function invokeCommand<T = unknown>(method: 'GET' | 'POST' | 'DELETE', url: string, payload?: object) {
  let id: string;
  const body = payload ? { ...payload } : undefined;
  if (method === 'GET') {
    const response = await apiService.get<{ id: string }>(url);
    id = response.id;
  } else if (method === 'POST') {
    const response = await apiService.post<{ id: string }>(url, body);
    id = response.id;
  } else {
    const response = await apiService.delete<{ id: string }>(url, body);
    id = response.id;
  }
  const command = await commandService.waitForCommand(id);
  let parsed: T | undefined;
  if (command.result) {
    try {
      parsed = JSON.parse(command.result) as T;
    } catch {
      /* ignore */
    }
  }
  return {
    success: command.status === 'Completed',
    data: parsed,
    error: command.status !== 'Completed' ? command.result ?? 'Script command failed' : undefined,
    command,
  } as CommandResultPayload<T>;
}

export const scriptsService = {
  list(deviceId: string) {
    return invokeCommand<ScriptListPayload>('GET', `/api/scripts/${deviceId}`);
  },
  deploy(deviceId: string, payload: ScriptDeployRequest) {
    return invokeCommand<ScriptListPayload>('POST', `/api/scripts/deploy/${deviceId}`, payload);
  },
  reload(deviceId: string) {
    return invokeCommand<ScriptListPayload>('POST', `/api/scripts/reload/${deviceId}`);
  },
  remove(deviceId: string, payload: ScriptRemoveRequest) {
    return invokeCommand<ScriptListPayload>('DELETE', `/api/scripts/${deviceId}`, payload);
  },
};

export default scriptsService;

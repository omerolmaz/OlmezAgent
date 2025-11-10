import { apiService } from './api.service';
import type { SessionSummary, StartSessionRequest } from '../types/session.types';

interface SessionResponse<T> {
  success: boolean;
  data: T;
  error?: string;
}

export const sessionsService = {
  async getSessionsByDevice(deviceId: string): Promise<SessionSummary[]> {
    try {
      const response = await apiService.get<SessionSummary[]>(`/api/sessions/device/${deviceId}`);
      return response;
    } catch (error) {
      console.error('Error fetching device sessions:', error);
      throw error;
    }
  },

  async startDesktopSession(request: StartSessionRequest) {
    // initialData obje ise JSON string'e çevir
    const payload = {
      ...request,
      sessionType: request.sessionType || 'desktop',
      initialData: typeof request.initialData === 'object' 
        ? JSON.stringify(request.initialData) 
        : request.initialData
    };
    const response = await apiService.post<SessionResponse<SessionSummary>>('/api/sessions/desktop/start', payload);
    return response.data;
  },

  async stopDesktopSession(sessionId: string) {
    return apiService.post('/api/sessions/desktop/stop', { sessionId });
  },

  async requestDesktopFrame(sessionId: string) {
    return apiService.post(`/api/sessions/desktop/frame?sessionId=${sessionId}`, {});
  },

  async startConsoleSession(request: StartSessionRequest) {
    // initialData obje ise JSON string'e çevir
    const payload = {
      ...request,
      sessionType: request.sessionType || 'console',
      initialData: typeof request.initialData === 'object' 
        ? JSON.stringify(request.initialData) 
        : request.initialData
    };
    const response = await apiService.post<SessionResponse<SessionSummary>>('/api/sessions/console/start', payload);
    return response.data;
  },

  async executeConsoleCommand(sessionId: string, command: string) {
    return apiService.post('/api/sessions/console/execute', { sessionId, command });
  },

  async endSession(sessionId: string) {
    return apiService.post(`/api/sessions/${sessionId}/end`);
  },
};

export default sessionsService;


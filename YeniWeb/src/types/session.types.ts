export interface SessionSummary {
  id: string;
  deviceId: string;
  userId: string;
  sessionType: string;
  sessionId: string;
  sessionData?: string;
  isActive: boolean;
  startedAt: string;
  endedAt?: string;
  lastActivityAt?: string;
}

export interface StartSessionRequest {
  deviceId: string;
  sessionType: string;
  initialData?: Record<string, unknown> | string;
}

export interface EndSessionRequest {
  sessionId: string; // Backend Guid bekliyor ama string olarak gönderiyoruz
}

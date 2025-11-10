import { apiService } from './api.service';
import { commandService } from './command.service';

export interface AgentMessageRequest {
  message: string;
  fromUser?: string;
}

export interface MessageBoxRequest {
  title: string;
  message: string;
  type?: 'info' | 'warning' | 'error';
}

export interface NotificationRequest {
  title: string;
  message: string;
}

export interface ToastRequest {
  title: string;
  message: string;
  duration?: number;
}

export interface ChatRequest {
  message: string;
  fromUser?: string;
}

async function sendMessagingCommand(
  action: string,
  deviceId: string,
  payload?: unknown,
) {
  const response = await apiService.post<{ id: string }>(`/api/messaging/${action}/${deviceId}`, payload ?? {});
  return commandService.waitForCommand(response.id);
}

export const messagingService = {
  sendAgentMessage(deviceId: string, payload: AgentMessageRequest) {
    return sendMessagingCommand('agentmsg', deviceId, payload);
  },
  showMessageBox(deviceId: string, payload: MessageBoxRequest) {
    return sendMessagingCommand('messagebox', deviceId, payload);
  },
  sendNotification(deviceId: string, payload: NotificationRequest) {
    return sendMessagingCommand('notify', deviceId, payload);
  },
  showToast(deviceId: string, payload: ToastRequest) {
    return sendMessagingCommand('toast', deviceId, payload);
  },
  sendChatMessage(deviceId: string, payload: ChatRequest) {
    return sendMessagingCommand('chat', deviceId, payload);
  },
};

export default messagingService;

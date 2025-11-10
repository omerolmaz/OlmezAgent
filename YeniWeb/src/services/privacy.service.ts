import { apiService } from './api.service';
import { commandService } from './command.service';

async function postPrivacyCommand(route: string, deviceId: string, payload?: Record<string, unknown>) {
  const response = await apiService.post<{ id: string }>(`/api/privacy/${route}/${deviceId}`, payload ?? {});
  return commandService.waitForCommand(response.id);
}

export const privacyService = {
  showBar(deviceId: string, message?: string) {
    return postPrivacyCommand('bar/show', deviceId, message ? { message } : undefined);
  },
  hideBar(deviceId: string) {
    return postPrivacyCommand('bar/hide', deviceId);
  },
};

export default privacyService;

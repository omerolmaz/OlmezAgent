import { apiService } from './api.service';
import { commandService } from './command.service';
import type { SecuritySnapshot } from '../types/device.types';
import type { CommandResultPayload } from '../types/command.types';
import type { CommandExecution } from '../types/command.types';

type SecurityCommand = 'status' | 'antivirus' | 'firewall' | 'defender' | 'uac' | 'encryption';

async function runSecurityCommand<T = SecuritySnapshot>(
  deviceId: string,
  command: SecurityCommand,
): Promise<CommandResultPayload<T>> {
  const endpoint = `/api/security/${command}/${deviceId}`;
  const executeResponse = await apiService.post<{ id: string }>(endpoint);
  const completed = await commandService.waitForCommand(executeResponse.id);
  const parsed = completed.result ? (JSON.parse(completed.result) as T) : undefined;

  return {
    success: completed.status === 'Completed',
    data: parsed,
    error: completed.status !== 'Completed' ? completed.result ?? 'Command failed' : undefined,
    command: completed,
  };
}

export const securityService = {
  getSecurityStatus(deviceId: string) {
    return runSecurityCommand(deviceId, 'status');
  },
  getAntivirusStatus(deviceId: string) {
    return runSecurityCommand(deviceId, 'antivirus');
  },
  getFirewallStatus(deviceId: string) {
    return runSecurityCommand(deviceId, 'firewall');
  },
  getDefenderStatus(deviceId: string) {
    return runSecurityCommand(deviceId, 'defender');
  },
  getUacStatus(deviceId: string) {
    return runSecurityCommand(deviceId, 'uac');
  },
  getEncryptionStatus(deviceId: string) {
    return runSecurityCommand(deviceId, 'encryption');
  },
  async getCachedSecuritySnapshot(deviceId: string): Promise<{
    snapshot: SecuritySnapshot | null;
    source?: CommandExecution;
  }> {
    const commands = await commandService.getCommandsByDevice(deviceId);
    if (!commands.length) {
      return { snapshot: null };
    }

    const latest = [...commands]
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
      .find((command) => command.commandType?.toLowerCase() === 'getsecuritystatus' && command.result);

    if (!latest?.result) {
      return { snapshot: null };
    }

    try {
      const parsed = JSON.parse(latest.result) as SecuritySnapshot;
      return { snapshot: parsed, source: latest };
    } catch {
      return { snapshot: null, source: latest };
    }
  },
};

export default securityService;


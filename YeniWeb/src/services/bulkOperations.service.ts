import softwareService from './software.service';
import commandService from './command.service';
import type { CommandExecution } from '../types/command.types';
import { toErrorMessage } from '../utils/error';

export type BulkOperationType = 'install' | 'uninstall' | 'update' | 'patch';

export interface BulkOperationRequest {
  deviceIds: string[];
  operation: BulkOperationType;
  payload: Record<string, unknown>;
  scheduledTime?: string;
}

export interface BulkOperationResult {
  deviceId: string;
  success: boolean;
  command?: CommandExecution;
  error?: string;
}

export async function runBulkOperation(request: BulkOperationRequest): Promise<BulkOperationResult[]> {
  const results: BulkOperationResult[] = [];

  for (const deviceId of request.deviceIds) {
    try {
      let command: CommandExecution;

      switch (request.operation) {
        case 'install':
          command = await softwareService.installSoftware(deviceId, {
            packagePath: request.payload.packagePath as string,
            arguments: request.payload.arguments as string | undefined,
          });
          break;
        case 'uninstall':
          command = await softwareService.uninstallSoftware(deviceId, {
            productName: request.payload.productName as string,
          });
          break;
        case 'update':
          command = await softwareService.installUpdates(deviceId, {
            updateIds: request.payload.updateIds as string[] | undefined,
          });
          break;
        case 'patch':
          command = await softwareService.schedulePatch(deviceId, {
            patchUrl: request.payload.patchUrl as string,
            scheduledTime:
              request.scheduledTime ??
              (request.payload.scheduledTime as string) ??
              new Date().toISOString(),
          });
          break;
        default:
          throw new Error(`Unsupported operation: ${request.operation}`);
      }

      results.push({
        deviceId,
        success: command.status === 'Completed',
        command,
        error: command.status === 'Completed' ? undefined : command.result ?? 'Operation failed',
      });
    } catch (error: unknown) {
      results.push({
        deviceId,
        success: false,
        error: toErrorMessage(error, 'Unexpected error'),
      });
    }
  }

  return results;
}

export async function waitForCommand(commandId: string) {
  return commandService.waitForCommand(commandId);
}


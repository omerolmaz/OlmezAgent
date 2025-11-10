import { inventoryService } from './inventory.service';
import { securityService } from './security.service';
import { commandService } from './command.service';
import { deviceService } from './device.service';
import type { Device } from '../types/device.types';
import type { CommandExecution } from '../types/command.types';

export type ReportCategory = 'inventory' | 'security' | 'updates' | 'operations' | 'devices';

export interface GeneratedReport<T = unknown> {
  category: ReportCategory;
  title: string;
  generatedAt: string;
  data: T;
  sourceCommands?: CommandExecution[];
}

export const reportsService = {
  async generateInventoryReport(deviceId: string): Promise<GeneratedReport> {
    const inventory = await inventoryService.getFullInventory(deviceId);
    if (!inventory.success || !inventory.data) {
      throw new Error(inventory.error ?? 'Inventory not collected yet.');
    }
    return {
      category: 'inventory',
      title: 'Full Inventory',
      generatedAt: new Date().toISOString(),
      data: inventory.data,
      sourceCommands: [],
    };
  },

  async generateSecurityReport(deviceId: string): Promise<GeneratedReport> {
    const status = await securityService.getSecurityStatus(deviceId);
    return {
      category: 'security',
      title: 'Security Overview',
      generatedAt: new Date().toISOString(),
      data: status.data,
      sourceCommands: status.command ? [status.command] : [],
    };
  },

  async generateOperationsReport(deviceId: string): Promise<GeneratedReport<CommandExecution[]>> {
    const commands = await commandService.getCommandsByDevice(deviceId);
    return {
      category: 'operations',
      title: 'Command History',
      generatedAt: new Date().toISOString(),
      data: commands,
    };
  },

  async generateDeviceSummary(): Promise<GeneratedReport<Device[]>> {
    const devices = await deviceService.getDevices();
    return {
      category: 'devices',
      title: 'Device Summary',
      generatedAt: new Date().toISOString(),
      data: devices,
    };
  },

  async generateReport(category: ReportCategory, deviceId?: string): Promise<GeneratedReport> {
    switch (category) {
      case 'inventory':
        if (!deviceId) throw new Error('Device id required for inventory report');
        return reportsService.generateInventoryReport(deviceId);
      case 'security':
        if (!deviceId) throw new Error('Device id required for security report');
        return reportsService.generateSecurityReport(deviceId);
      case 'operations':
        if (!deviceId) throw new Error('Device id required for operations report');
        return reportsService.generateOperationsReport(deviceId);
      case 'devices':
        return reportsService.generateDeviceSummary();
      case 'updates':
        if (!deviceId) throw new Error('Device id required for updates report');
        return reportsService.generateInventoryReport(deviceId);
      default:
        throw new Error(`Unsupported report category: ${category}`);
    }
  },
};

export type ReportResult = Promise<GeneratedReport>;

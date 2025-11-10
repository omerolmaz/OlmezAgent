import { deviceService } from './device.service';
import { commandService } from './command.service';
import type { Device } from '../types/device.types';
import type { CommandExecution } from '../types/command.types';

export interface DashboardStats {
  totalDevices: number;
  onlineDevices: number;
  warningCount: number;
  activeSessions: number;
}

const isOnline = (status: Device['status']) => status === 'Connected';

export const dashboardService = {
  async getStats(): Promise<DashboardStats> {
    try {
      const [devices, activeConnections] = await Promise.all([
        deviceService.getDevices(),
        commandService.getActiveConnections(),
      ]);

      const onlineCount = devices.filter((device) => isOnline(device.status)).length;
      const totalCount = devices.length;
      const warningCount = Math.max(totalCount - onlineCount, 0);

      return {
        totalDevices: totalCount,
        onlineDevices: onlineCount,
        warningCount,
        activeSessions: activeConnections.connectedCount,
      };
    } catch (error) {
      console.error('Error fetching dashboard stats:', error);
      return {
        totalDevices: 0,
        onlineDevices: 0,
        warningCount: 0,
        activeSessions: 0,
      };
    }
  },

  async getRecentDevices(): Promise<Device[]> {
    try {
      const devices = await deviceService.getDevices();
      return devices
        .slice()
        .sort((a, b) => {
          const dateA = a.lastSeenAt ? new Date(a.lastSeenAt).getTime() : 0;
          const dateB = b.lastSeenAt ? new Date(b.lastSeenAt).getTime() : 0;
          return dateB - dateA;
        })
        .slice(0, 6);
    } catch (error) {
      console.error('Error fetching recent devices:', error);
      return [];
    }
  },

  async getRecentCommands(): Promise<CommandExecution[]> {
    try {
      const devices = await deviceService.getDevices();
      if (devices.length === 0) return [];

      const commandPromises = devices.slice(0, 5).map((device) =>
        commandService.getCommandsByDevice(device.id).catch(() => []),
      );

      const commandGroups = await Promise.all(commandPromises);
      return commandGroups
        .flat()
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .slice(0, 10);
    } catch (error) {
      console.error('Error fetching recent commands:', error);
      return [];
    }
  },
};

export default dashboardService;


import { apiService } from './api.service';
import type { ConnectionStatus, Device } from '../types/device.types';

export interface DeviceConnectionStatus {
  success: boolean;
  data: {
    deviceId: string;
    isConnected: boolean;
    timestamp: string;
  };
}

export const deviceService = {
  // Get all devices
  getDevices: async (): Promise<Device[]> => {
    try {
      const response = await apiService.get<Device[]>('/api/devices');
      return response.map(normalizeDevice);
    } catch (error) {
      console.error('Error fetching devices:', error);
      throw error;
    }
  },

  // Get device by ID
  getDeviceById: async (id: string): Promise<Device> => {
    try {
      const response = await apiService.get<Device>(`/api/devices/${id}`);
      return normalizeDevice(response);
    } catch (error) {
      console.error('Error fetching device:', error);
      throw error;
    }
  },

  // Get devices by group
  getDevicesByGroup: async (groupId: string): Promise<Device[]> => {
    try {
      const response = await apiService.get<Device[]>(`/api/devices/group/${groupId}`);
      return response.map(normalizeDevice);
    } catch (error) {
      console.error('Error fetching devices by group:', error);
      throw error;
    }
  },

  // Delete device
  deleteDevice: async (id: string): Promise<void> => {
    try {
      await apiService.delete(`/api/devices/${id}`);
    } catch (error) {
      console.error('Error deleting device:', error);
      throw error;
    }
  },

  // Get device connection status
  getDeviceStatus: async (deviceId: string): Promise<DeviceConnectionStatus['data']> => {
    try {
      const response = await apiService.get<DeviceConnectionStatus>(`/api/commands/status/${deviceId}`);
      return response.data;
    } catch (error) {
      console.error('Error fetching device status:', error);
      return {
        deviceId,
        isConnected: false,
        timestamp: new Date().toISOString(),
      };
    }
  },
};

export default deviceService;

function normalizeDevice(device: Device): Device {
  const normalizedStatus = parseConnectionStatus(device.status as unknown as number | ConnectionStatus);
  return {
    ...device,
    status: normalizedStatus,
  };
}

function parseConnectionStatus(value: number | ConnectionStatus): ConnectionStatus {
  if (typeof value === 'string') {
    return value as ConnectionStatus;
  }

  switch (value) {
    case 0:
      return 'Disconnected';
    case 1:
      return 'Connecting';
    case 2:
      return 'Connected';
    case 3:
      return 'Reconnecting';
    case 4:
      return 'Error';
    default:
      return 'Disconnected';
  }
}


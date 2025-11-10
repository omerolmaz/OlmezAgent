import { isAxiosError } from 'axios';
import { apiService } from './api.service';
import { commandService } from './command.service';
import type { InstalledSoftware, PatchInfo } from '../types/device.types';
import type { CommandResultPayload } from '../types/command.types';

type InventoryCommand =
  | 'full'
  | 'software'
  | 'patches'
  | 'updates'
  | 'sysinfo'
  | 'cpu'
  | 'network'
  | 'smbios'
  | 'vm'
  | 'wifi'
  | 'perfcounters';

interface FileListEntry {
  name: string;
  type: 'file' | 'directory';
  size?: number;
  modifiedAt?: string;
}

async function runInventoryCommand<T>(
  deviceId: string,
  command: InventoryCommand,
  body?: Record<string, unknown>,
): Promise<CommandResultPayload<T>> {
  const endpoint = `/api/inventory/${command}/${deviceId}`;
  const executeResponse = await apiService.post<{ id: string }>(endpoint, body ?? {});
  const completed = await commandService.waitForCommand(executeResponse.id);
  const parsed = completed.result ? (JSON.parse(completed.result) as T) : undefined;

  return {
    success: completed.status === 'Completed',
    data: parsed,
    error: completed.status !== 'Completed' ? completed.result ?? 'Command failed' : undefined,
    command: completed,
  };
}

export interface InventoryResult {
  success: boolean;
  data?: InventoryDetail;
  error?: string;
}

export interface InventoryDetail {
  deviceId: string;
  collectedAt: string;
  updatedAt: string;
  manufacturer?: string;
  model?: string;
  serialNumber?: string;
  biosVersion?: string;
  biosManufacturer?: string;
  biosReleaseDate?: string;
  systemSku?: string;
  systemFamily?: string;
  chassisType?: string;
  processorName?: string;
  processorManufacturer?: string;
  processorCores?: number;
  processorLogicalProcessors?: number;
  processorArchitecture?: string;
  processorMaxClockSpeed?: number;
  totalPhysicalMemoryBytes?: number;
  memorySlots?: number;
  memoryType?: string;
  memorySpeed?: number;
  totalDiskSpaceBytes?: number;
  diskCount?: number;
  osName?: string;
  osVersion?: string;
  osBuild?: string;
  osArchitecture?: string;
  osInstallDate?: string;
  osSerialNumber?: string;
  osProductKey?: string;
  primaryMacAddress?: string;
  primaryIpAddress?: string;
  hostName?: string;
  domainName?: string;
  graphicsCard?: string;
  graphicsCardMemory?: string;
  currentResolution?: string;
  networkAdapters: NetworkAdapterSummary[];
  diskDrives: DiskDriveSummary[];
  monitors: MonitorInfoSummary[];
  software: InventorySoftware[];
  patches: InventoryPatch[];
}

export interface NetworkAdapterSummary {
  name?: string;
  description?: string;
  status?: string;
  speedBitsPerSecond?: number;
  macAddress?: string;
  ipAddresses: string[];
}

export interface DiskDriveSummary {
  deviceId?: string;
  sizeBytes?: number;
  freeBytes?: number;
  fileSystem?: string;
}

export interface MonitorInfoSummary {
  manufacturer?: string;
  model?: string;
  serialNumber?: string;
  resolution?: string;
  size?: string;
}

export interface InventorySoftware {
  id: string;
  name: string;
  version?: string;
  publisher?: string;
  installDate?: string;
  installLocation?: string;
  sizeInBytes?: number;
}

export interface InventoryPatch {
  id: string;
  hotFixId: string;
  description?: string;
  installedOn?: string;
  installedBy?: string;
}

interface InventoryApiEnvelope {
  success: boolean;
  data?: InventoryApiDetail;
  error?: string;
}

type InventorySoftwareApi = InventorySoftware & { sizeInBytes?: number | string };
type InventoryPatchApi = InventoryPatch;

interface InventoryApiDetail {
  deviceId: string;
  collectedAt: string;
  updatedAt: string;
  manufacturer?: string;
  model?: string;
  serialNumber?: string;
  biosVersion?: string;
  biosManufacturer?: string;
  biosReleaseDate?: string;
  systemSku?: string;
  systemFamily?: string;
  chassisType?: string;
  processorName?: string;
  processorManufacturer?: string;
  processorCores?: number;
  processorLogicalProcessors?: number;
  processorArchitecture?: string;
  processorMaxClockSpeed?: number;
  totalPhysicalMemoryBytes?: number | string;
  memorySlots?: number;
  memoryType?: string;
  memorySpeed?: number;
  totalDiskSpaceBytes?: number | string;
  diskCount?: number;
  osName?: string;
  osVersion?: string;
  osBuild?: string;
  osArchitecture?: string;
  osInstallDate?: string;
  osSerialNumber?: string;
  osProductKey?: string;
  primaryMacAddress?: string;
  primaryIpAddress?: string;
  hostName?: string;
  domainName?: string;
  graphicsCard?: string;
  graphicsCardMemory?: string;
  currentResolution?: string;
  networkAdapters?: Array<
    NetworkAdapterSummary & { speedBitsPerSecond?: number | string; ipAddresses?: string[] }
  >;
  diskDrives?: Array<DiskDriveSummary & { sizeBytes?: number | string; freeBytes?: number | string }>;
  monitors?: MonitorInfoSummary[];
  software?: InventorySoftwareApi[];
  patches?: InventoryPatchApi[];
}

export const inventoryService = {
  async getFullInventory(deviceId: string): Promise<InventoryResult> {
    try {
      const response = await apiService.get<InventoryApiEnvelope>(`/api/inventory/devices/${deviceId}`);
      if (!response.success || !response.data) {
        return {
          success: false,
          error: response.error ?? 'Inventory not collected yet. Please refresh.',
          data: undefined,
        };
      }

      return {
        success: true,
        data: normalizeInventory(response.data),
      };
    } catch (error: unknown) {
      if (isAxiosError(error) && error.response?.status === 404) {
        return {
          success: false,
          error: 'Inventory not collected yet. Please refresh.',
          data: undefined,
        };
      }
      throw error;
    }
  },

  async refreshInventory(deviceId: string, userId: string) {
    return await apiService.post<{ commandId: string; message: string }>(
      `/api/inventory/devices/${deviceId}/refresh`,
      { userId },
    );
  },

  // Get installed software from database
  async getInstalledSoftware(deviceId: string) {
    return await apiService.get<InstalledSoftware[]>(`/api/inventory/devices/${deviceId}/software`);
  },

  // Get installed patches from database
  async getInstalledPatches(deviceId: string) {
    return await apiService.get<PatchInfo[]>(`/api/inventory/devices/${deviceId}/patches`);
  },

  getSystemInfo(deviceId: string) {
    return runInventoryCommand<Record<string, unknown>>(deviceId, 'sysinfo');
  },

  getCpuInfo(deviceId: string) {
    return runInventoryCommand<Record<string, unknown>>(deviceId, 'cpu');
  },

  getNetworkInfo(deviceId: string) {
    return runInventoryCommand<Record<string, unknown>>(deviceId, 'network');
  },

  getSmbios(deviceId: string) {
    return runInventoryCommand<Record<string, unknown>>(deviceId, 'smbios');
  },

  detectVirtualMachine(deviceId: string) {
    return runInventoryCommand<{ isVirtualMachine: boolean; details?: string }>(deviceId, 'vm');
  },

  scanWifi(deviceId: string) {
    return runInventoryCommand<{ networks: Array<Record<string, unknown>> }>(deviceId, 'wifi');
  },

  getPerformanceCounters(deviceId: string) {
    return runInventoryCommand<Record<string, unknown>>(deviceId, 'perfcounters');
  },
};

export const fileInventoryService = {
  async listPath(deviceId: string, path: string) {
    const executeResponse = await apiService.post<{ id: string }>(`/api/remoteops/ls/${deviceId}`, { path });
    const completed = await commandService.waitForCommand(executeResponse.id, { timeoutMs: 20000 });
    const parsed = completed.result ? (JSON.parse(completed.result) as FileListEntry[]) : [];

    return {
      success: completed.status === 'Completed',
      data: parsed,
      error: completed.status !== 'Completed' ? completed.result ?? 'Listing failed' : undefined,
      command: completed,
    } as CommandResultPayload<FileListEntry[]>;
  },
};

export default inventoryService;

function normalizeInventory(response: InventoryApiDetail): InventoryDetail {
  const diskDrives = (response.diskDrives ?? []).map((drive) => ({
    deviceId: drive.deviceId,
    fileSystem: drive.fileSystem,
    sizeBytes: coerceNumber(drive.sizeBytes),
    freeBytes: coerceNumber(drive.freeBytes),
  }));

  const totalDiskSpace =
    coerceNumber(response.totalDiskSpaceBytes) ??
    (diskDrives.length > 0
      ? diskDrives
          .map((drive) => drive.sizeBytes ?? 0)
          .reduce((total, value) => total + value, 0)
      : undefined);

  return {
    deviceId: response.deviceId,
    collectedAt: response.collectedAt,
    updatedAt: response.updatedAt,
    manufacturer: response.manufacturer,
    model: response.model,
    serialNumber: response.serialNumber,
    biosVersion: response.biosVersion,
    biosManufacturer: response.biosManufacturer,
    biosReleaseDate: response.biosReleaseDate,
    systemSku: response.systemSku,
    systemFamily: response.systemFamily,
    chassisType: response.chassisType,
    processorName: response.processorName,
    processorManufacturer: response.processorManufacturer,
    processorCores: response.processorCores,
    processorLogicalProcessors: response.processorLogicalProcessors,
    processorArchitecture: response.processorArchitecture,
    processorMaxClockSpeed: response.processorMaxClockSpeed,
    totalPhysicalMemoryBytes: coerceNumber(response.totalPhysicalMemoryBytes),
    memorySlots: response.memorySlots,
    memoryType: response.memoryType,
    memorySpeed: response.memorySpeed,
    totalDiskSpaceBytes: totalDiskSpace,
    diskCount: response.diskCount ?? (diskDrives.length > 0 ? diskDrives.length : undefined),
    osName: response.osName,
    osVersion: response.osVersion,
    osBuild: response.osBuild,
    osArchitecture: response.osArchitecture,
    osInstallDate: response.osInstallDate,
    osSerialNumber: response.osSerialNumber,
    osProductKey: response.osProductKey,
    primaryMacAddress: response.primaryMacAddress,
    primaryIpAddress: response.primaryIpAddress,
    hostName: response.hostName,
    domainName: response.domainName,
    graphicsCard: response.graphicsCard,
    graphicsCardMemory: response.graphicsCardMemory,
    currentResolution: response.currentResolution,
    networkAdapters: (response.networkAdapters ?? []).map((adapter) => ({
      name: adapter.name,
      description: adapter.description,
      status: adapter.status,
      macAddress: adapter.macAddress,
      speedBitsPerSecond: coerceNumber(adapter.speedBitsPerSecond),
      ipAddresses: Array.isArray(adapter.ipAddresses) ? adapter.ipAddresses : [],
    })),
    diskDrives,
    monitors: response.monitors ?? [],
    software: (response.software ?? []).map(normalizeSoftware),
    patches: (response.patches ?? []).map(normalizePatch),
  };
}

function normalizeSoftware(item: InventorySoftwareApi): InventorySoftware {
  return {
    id: item.id,
    name: item.name,
    version: item.version,
    publisher: item.publisher,
    installDate: item.installDate,
    installLocation: item.installLocation,
    sizeInBytes: coerceNumber(item.sizeInBytes),
  };
}

function normalizePatch(item: InventoryPatchApi): InventoryPatch {
  return {
    id: item.id,
    hotFixId: item.hotFixId,
    description: item.description,
    installedOn: item.installedOn,
    installedBy: item.installedBy,
  };
}

function coerceNumber(value: number | string | undefined | null): number | undefined {
  if (value === undefined || value === null) return undefined;
  if (typeof value === 'number') {
    return Number.isNaN(value) ? undefined : value;
  }
  const parsed = Number(value);
  return Number.isNaN(parsed) ? undefined : parsed;
}

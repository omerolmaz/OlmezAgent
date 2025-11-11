export type ConnectionStatus = 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting' | 'Error';

export interface Device {
  id: string;
  hostname: string;
  macAddress?: string;
  domain?: string;
  osVersion?: string;
  architecture?: string;
  ipAddress?: string;
  agentVersion?: string;
  status: ConnectionStatus;
  lastSeenAt?: string;
  registeredAt: string;
  groupId?: string;
  groupName?: string;
}

export interface DeviceListResponse {
  success: boolean;
  data: Device[];
}

export interface DeviceDetail extends Device {
  hardware?: HardwareInventory;
  software?: SoftwareInventory;
  security?: SecuritySnapshot;
}

export interface HardwareInventory {
  systemManufacturer?: string;
  systemModel?: string;
  processor?: string;
  physicalMemory?: string;
  biosVersion?: string;
  disks?: DiskInfo[];
  motherboard?: string;
  gpu?: string;
}

export interface DiskInfo {
  name: string;
  sizeBytes: number;
  freeBytes: number;
  filesystem?: string;
  type?: string;
}

export interface SoftwareInventory {
  installedApplications?: InstalledSoftware[];
  installedPatches?: PatchInfo[];
  pendingUpdates?: UpdateInfo[];
}

export interface InstalledSoftware {
  name: string;
  version?: string;
  publisher?: string;
  installDate?: string;
  sizeMb?: number;
  uninstallString?: string;
  sizeInBytes?: number;
}

export interface PatchInfo {
  kbNumber: string;
  title: string;
  installDate?: string;
  description?: string;
}

export interface UpdateInfo {
  kbNumber: string;
  title: string;
  severity?: 'Critical' | 'Important' | 'Moderate' | 'Low';
  sizeMb?: number;
  description?: string;
}

export interface AntivirusProduct {
  displayName?: string;
  instanceGuid?: string;
  pathToSignedProductExe?: string;
  productState?: number;
  enabled?: boolean;
  upToDate?: boolean;
  timestamp?: string;
  error?: string;
}

export interface FirewallProduct {
  displayName?: string;
  productState?: number;
  enabled?: boolean;
  error?: string;
}

export interface FirewallProfile {
  profile?: string;
  enabled?: boolean;
  error?: string;
}

export interface FirewallSnapshot {
  products?: FirewallProduct[];
  profiles?: FirewallProfile[];
  error?: string;
}

export interface DefenderStatus {
  antivirusEnabled?: boolean;
  antivirusSignatureLastUpdated?: string;
  antivirusSignatureVersion?: string;
  antiSpywareEnabled?: boolean;
  antiSpywareSignatureLastUpdated?: string;
  realtimeProtectionEnabled?: boolean;
  behaviorMonitorEnabled?: boolean;
  ioavProtectionEnabled?: boolean;
  onAccessProtectionEnabled?: boolean;
  quickScanAge?: number;
  fullScanAge?: number;
  computerState?: number;
  error?: string;
  [key: string]: string | number | boolean | undefined;
}

export interface UacStatus {
  enabled?: boolean;
  consentPromptBehaviorAdmin?: string;
  promptOnSecureDesktop?: boolean;
  level?: string;
  error?: string;
  [key: string]: string | number | boolean | undefined;
}

export interface BitLockerVolumeStatus {
  driveLetter?: string;
  protectionStatus?: string;
  conversionStatus?: string;
  encryptionMethod?: string;
  error?: string;
}

export interface EncryptionStatus {
  bitlockerVolumes?: BitLockerVolumeStatus[];
  error?: string;
}

export interface SecurityCenterSnapshot {
  componentHealth?: Record<string, boolean>;
  error?: string;
}

export interface SecuritySnapshot {
  timestamp?: string;
  antivirus?: AntivirusProduct[];
  firewall?: FirewallSnapshot;
  defender?: DefenderStatus;
  uac?: UacStatus;
  encryption?: EncryptionStatus;
  securityCenter?: SecurityCenterSnapshot;
  lastSecurityScan?: string;
  riskScore?: number;
}

export interface PerformanceMetrics {
  cpuUsage: number;
  memoryUsage: number;
  diskUsage: number;
  uptimeSeconds: number;
  network?: NetworkMetrics;
  timestamp: string;
}

export interface NetworkMetrics {
  totalSentKb: number;
  totalReceivedKb: number;
  interfaces: NetworkInterfaceMetrics[];
}

export interface NetworkInterfaceMetrics {
  name: string;
  ipAddress?: string;
  macAddress?: string;
  speedMbps?: number;
  isUp: boolean;
}


import type { InventoryDetail } from '../../services/inventory.service';
import type { SecuritySnapshot, PerformanceMetrics, InstalledSoftware, PatchInfo, UpdateInfo } from '../../types/device.types';
import type { EventLogEntry } from '../../services/eventLogs.service';
import type { CommandResultPayload } from '../../types/command.types';
import type { RemoteQualityLevel } from '../../constants/remoteDesktop';

export interface DiagnosticsState {
  loading: boolean;
  status?: Record<string, unknown>;
  agentInfo?: Record<string, unknown>;
  connectionDetails?: Record<string, unknown>;
  pingResult?: Record<string, unknown>;
  error?: string;
}

export interface InventoryState {
  loading: boolean;
  error?: string;
  data?: InventoryDetail;
}

export interface SecurityState {
  loading: boolean;
  snapshot?: SecuritySnapshot;
  error?: string;
}

export interface EventLogState {
  loading: boolean;
  items: EventLogEntry[];
  error?: string;
}

export interface PerformanceState {
  loading: boolean;
  metrics?: PerformanceMetrics;
  error?: string;
}

export type MessagingAction = 'agentmsg' | 'messagebox' | 'notify' | 'toast' | 'chat';

export interface MessagingFormState {
  action: MessagingAction;
  title: string;
  message: string;
  duration: number;
  sending: boolean;
  feedback?: string;
  error?: string;
}

export interface MaintenanceFormState {
  version: string;
  channel: string;
  force: boolean;
  installerUrl: string;
  preserveConfig: boolean;
  tailLines: number;
  includeDiagnostics: boolean;
  busy: boolean;
  message?: string;
  error?: string;
  logResult?: string;
}

export interface ScriptsState {
  loading: boolean;
  scripts: string[];
  handlers: string[];
  error?: string;
}

export interface ScriptFormState {
  name: string;
  code: string;
  busy: boolean;
  feedback?: string;
  error?: string;
}

export interface RemoteDesktopSession {
  sessionId: string;
  width: number;
  height: number;
  quality: QualityLevel;
  startedAt: string;
}

export interface RemoteControlState {
  busy: boolean;
  session?: RemoteDesktopSession | null;
  error?: string;
}

export interface RemoteClipboardState {
  value: string;
  loading: boolean;
  syncing: boolean;
  error?: string;
}

export type QualityLevel = RemoteQualityLevel;

export interface TerminalEntry {
  id: string;
  cmd: string;
  result: string;
  time: string;
  status: 'success' | 'error';
}

export interface TerminalState {
  shell: string;
  command: string;
  outputs: TerminalEntry[];
  isRunning: boolean;
  error?: string;
}

export interface SoftwareState {
  loading: boolean;
  items: InstalledSoftware[];
  actionTarget?: string;
  message?: string;
  error?: string;
}

export interface PatchState {
  loading: boolean;
  installed: PatchInfo[];
  pending: UpdateInfo[];
  busy?: boolean;
  message?: string;
  error?: string;
}

export interface PatchFormState {
  patchUrl: string;
  scheduledTime: string;
}

export type DiagnosticsBundle = [
  CommandResultPayload<Record<string, unknown>>,
  CommandResultPayload<Record<string, unknown>>,
  CommandResultPayload<Record<string, unknown>>,
  CommandResultPayload<Record<string, unknown>>,
];


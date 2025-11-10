export type RemoteQualityLevel = 'low' | 'medium' | 'high';

export const REMOTE_QUALITY_MAP: Record<RemoteQualityLevel, number> = {
  low: 45,
  medium: 65,
  high: 85,
};

export const DEFAULT_REMOTE_RESOLUTION = {
  width: 1280,
  height: 720,
};

export function getQualityPercent(level: RemoteQualityLevel): number {
  return REMOTE_QUALITY_MAP[level];
}

export type RemoteShortcutId = 'ctrlAltDel' | 'ctrlEsc' | 'ctrlShiftEsc';

export const REMOTE_KEY_COMBOS: Record<RemoteShortcutId, number[]> = {
  ctrlAltDel: [17, 18, 46],
  ctrlEsc: [17, 27],
  ctrlShiftEsc: [17, 16, 27],
};

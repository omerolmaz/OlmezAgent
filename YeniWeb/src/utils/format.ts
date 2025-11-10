const BYTE_UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'] as const;

export function formatBytes(value?: number | null, fractionDigits = 1): string {
  if (value === undefined || value === null || Number.isNaN(value)) {
    return '-';
  }

  let numeric = Number(value);
  if (numeric < 0) numeric = 0;
  if (numeric === 0) return '0 B';

  const unitIndex = Math.min(Math.floor(Math.log(numeric) / Math.log(1024)), BYTE_UNITS.length - 1);
  const scaled = numeric / 1024 ** unitIndex;

  return `${scaled.toFixed(unitIndex === 0 ? 0 : fractionDigits)} ${BYTE_UNITS[unitIndex]}`;
}

export function formatDateTime(value?: string | null, locale: string = 'en-GB'): string {
  if (!value) return '-';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  try {
    return new Intl.DateTimeFormat(locale, {
      year: 'numeric',
      month: 'short',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  } catch {
    return date.toISOString();
  }
}

export function formatRelativeTime(value?: string | null, locale: string = 'en-GB'): string {
  if (!value) return '-';
  const date = new Date(value);
  const now = Date.now();
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const diffMs = date.getTime() - now;
  const diffMinutes = Math.round(diffMs / (1000 * 60));

  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: 'auto' });
  if (Math.abs(diffMinutes) < 60) {
    return rtf.format(diffMinutes, 'minute');
  }
  const diffHours = Math.round(diffMinutes / 60);
  if (Math.abs(diffHours) < 48) {
    return rtf.format(diffHours, 'hour');
  }
  const diffDays = Math.round(diffHours / 24);
  return rtf.format(diffDays, 'day');
}

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Shield,
  ShieldCheck,
  ShieldOff,
  ShieldAlert,
  Search,
  RefreshCw,
  Loader2,
  AlertCircle,
} from 'lucide-react';
import { deviceService } from '../services/device.service';
import { securityService } from '../services/security.service';
import type { Device, SecuritySnapshot } from '../types/device.types';
import type { CommandExecution } from '../types/command.types';
import { useTranslation } from '../hooks/useTranslation';
import { formatDateTime, formatRelativeTime } from '../utils/format';
import { toErrorMessage } from '../utils/error';

type Tone = 'good' | 'warning' | 'bad' | 'unknown';

type SecurityRowState = {
  device: Device;
  status: 'loading' | 'ready' | 'error';
  snapshot?: SecuritySnapshot | null;
  command?: CommandExecution;
  error?: string;
};

const STATUS_FIELDS = [
  { key: 'antivirus', labelKey: 'security.statusCards.antivirus' },
  { key: 'firewall', labelKey: 'security.statusCards.firewall' },
  { key: 'defender', labelKey: 'security.statusCards.defender' },
  { key: 'uac', labelKey: 'security.statusCards.uac' },
  { key: 'encryption', labelKey: 'security.statusCards.encryption' },
] as const;

type StatusFieldKey = (typeof STATUS_FIELDS)[number]['key'];
type Translator = ReturnType<typeof useTranslation>['t'];

const TONE_PRIORITY: Record<Tone, number> = {
  good: 1,
  warning: 2,
  bad: 3,
  unknown: 0,
};

const TONE_BADGE: Record<Tone, string> = {
  good: 'bg-emerald-500/10 text-emerald-600',
  warning: 'bg-amber-500/10 text-amber-600',
  bad: 'bg-destructive/10 text-destructive',
  unknown: 'bg-muted text-muted-foreground',
};

export default function Security() {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const [devices, setDevices] = useState<Device[]>([]);
  const [rows, setRows] = useState<Record<string, SecurityRowState>>({});
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<'all' | Tone>('all');
  const [pageLoading, setPageLoading] = useState(false);
  const [pageError, setPageError] = useState('');
  const [summariesLoading, setSummariesLoading] = useState(false);

  const rowList: SecurityRowState[] = useMemo(
    () => devices.map((device) => rows[device.id] ?? { device, status: 'loading' as const }),
    [devices, rows],
  );

  const filteredRows = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    return rowList.filter((row) => {
      const matchesSearch =
        !normalized ||
        [row.device.hostname, row.device.domain, row.device.osVersion]
          .filter(Boolean)
          .join(' ')
          .toLowerCase()
          .includes(normalized);

      if (!matchesSearch) return false;

      const tone = determineOverallTone(row.snapshot);
      if (filter === 'all') return true;
      return tone === filter;
    });
  }, [filter, rowList, search]);

  const summaryStats = useMemo(() => {
    const totals = { total: rowList.length, good: 0, warning: 0, bad: 0, unknown: 0 };
    rowList.forEach((row) => {
      const tone = determineOverallTone(row.snapshot);
      if (tone === 'good') totals.good += 1;
      else if (tone === 'warning') totals.warning += 1;
      else if (tone === 'bad') totals.bad += 1;
      else totals.unknown += 1;
    });
    return totals;
  }, [rowList]);

  const ensureRow = useCallback((device: Device) => {
    setRows((prev) => {
      if (prev[device.id]) return prev;
      return {
        ...prev,
        [device.id]: {
          device,
          status: 'loading',
        },
      };
    });
  }, []);

  const loadSecuritySnapshots = useCallback(
    async (targetDevices?: Device[]) => {
      const list = targetDevices ?? devices;
      if (!list.length) return;

      setSummariesLoading(true);
      let index = 0;

      const worker = async () => {
        while (index < list.length) {
          const device = list[index++];
          ensureRow(device);
          try {
            const { snapshot, source } = await securityService.getCachedSecuritySnapshot(device.id);
            if (!snapshot) {
              setRows((prev) => ({
                ...prev,
                [device.id]: {
                  device,
                  status: 'error',
                  snapshot: null,
                  error: t('security.missing'),
                  command: source,
                },
              }));
            } else {
              setRows((prev) => ({
                ...prev,
                [device.id]: {
                  device,
                  status: 'ready',
                  snapshot,
                  command: source,
                },
              }));
            }
          } catch (error) {
            setRows((prev) => ({
              ...prev,
              [device.id]: {
                device,
                status: 'error',
                snapshot: prev[device.id]?.snapshot,
                command: prev[device.id]?.command,
                error: toErrorMessage(error, t('security.error')),
              },
            }));
          }
        }
      };

      const concurrency = Math.min(4, list.length || 1);
      await Promise.all(Array.from({ length: concurrency }, () => worker()));
      setSummariesLoading(false);
    },
    [devices, ensureRow, t],
  );

  const loadDevices = useCallback(async () => {
    setPageLoading(true);
    setPageError('');
    try {
      const list = await deviceService.getDevices();
      setDevices(list);
      list.forEach(ensureRow);
      if (list.length > 0) {
        await loadSecuritySnapshots(list);
      }
    } catch (error) {
      setPageError(toErrorMessage(error, t('security.error')));
    } finally {
      setPageLoading(false);
    }
  }, [ensureRow, loadSecuritySnapshots, t]);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  const handleRefreshAll = useCallback(async () => {
    await loadSecuritySnapshots(devices);
  }, [devices, loadSecuritySnapshots]);

  const handleRefreshRow = useCallback(
    async (deviceId: string) => {
      const device = devices.find((item) => item.id === deviceId);
      if (!device) return;

      setRows((prev) => ({
        ...prev,
        [deviceId]: {
          ...(prev[deviceId] ?? { device }),
          device,
          status: 'loading',
        },
      }));

      try {
        const result = await securityService.getSecurityStatus(deviceId);
        if (!result.success || !result.data) {
          throw new Error(result.error ?? t('security.error'));
        }
        setRows((prev) => ({
          ...prev,
          [deviceId]: {
            device,
            status: 'ready',
            snapshot: result.data,
            command: result.command ?? prev[deviceId]?.command,
          },
        }));
      } catch (error) {
        setRows((prev) => ({
          ...prev,
          [deviceId]: {
            device,
            status: 'error',
            snapshot: prev[deviceId]?.snapshot,
            command: prev[deviceId]?.command,
            error: toErrorMessage(error, t('security.error')),
          },
        }));
      }
    },
    [devices, t],
  );

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{t('security.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('security.description')}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative flex items-center">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t('security.searchPlaceholder', undefined, t('common.searchPlaceholder'))}
              className="w-64 rounded-lg border border-border bg-card pl-9 pr-3 py-2 text-sm"
              aria-label={t('common.searchPlaceholder')}
            />
          </div>
          <button
            type="button"
            onClick={handleRefreshAll}
            disabled={summariesLoading}
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/60 px-3 py-2 text-sm text-foreground transition hover:bg-secondary/80 disabled:opacity-60"
          >
            {summariesLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
            {t('security.refresh')}
          </button>
        </div>
      </div>

      {pageError && (
        <div className="flex items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{pageError}</span>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-4">
        <SecurityStat
          icon={Shield}
          label={t('devices.title')}
          value={summaryStats.total.toString()}
        />
        <SecurityStat
          icon={ShieldCheck}
          label={t('security.risk.low')}
          value={summaryStats.good.toString()}
          tone="good"
        />
        <SecurityStat
          icon={ShieldAlert}
          label={t('security.risk.medium')}
          value={summaryStats.warning.toString()}
          tone="warning"
        />
        <SecurityStat
          icon={ShieldOff}
          label={t('security.risk.high')}
          value={summaryStats.bad.toString()}
          tone="bad"
        />
      </div>

      <FilterBar filter={filter} onFilterChange={setFilter} />

      <div className="overflow-hidden rounded-xl border border-border bg-card shadow-sm">
        <table className="min-w-full divide-y divide-border text-sm">
          <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">{t('inventory.device')}</th>
              {STATUS_FIELDS.map((field) => (
                <th key={field.key} className="px-4 py-3 text-left">
                  {t(field.labelKey)}
                </th>
              ))}
              <th className="px-4 py-3 text-left">{t('security.meta.riskScore')}</th>
              <th className="px-4 py-3 text-left">{t('security.meta.lastScan')}</th>
              <th className="px-4 py-3 text-left">{t('commands.table.finished')}</th>
              <th className="px-4 py-3 text-right">{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {pageLoading ? (
              <tr>
                <td colSpan={STATUS_FIELDS.length + 4} className="px-4 py-8 text-center text-muted-foreground">
                  <Loader2 className="mr-2 inline h-4 w-4 animate-spin" />
                  {t('security.loading')}
                </td>
              </tr>
            ) : filteredRows.length === 0 ? (
              <tr>
                <td colSpan={STATUS_FIELDS.length + 4} className="px-4 py-8 text-center text-muted-foreground">
                  {t('security.missing')}
                </td>
              </tr>
            ) : (
              filteredRows.map((row) => {
                const { device, snapshot, command, status, error } = row;
                const tone = determineOverallTone(snapshot);
                const riskScore = parseRiskScore(snapshot?.riskScore);
                const lastScan = snapshot?.lastSecurityScan;
                const lastUpdated = command?.completedAt ?? command?.createdAt;

                return (
                  <tr key={device.id} className="border-t border-border/60">
                    <td className="px-4 py-3">
                      <div className="flex items-start gap-3">
                        <span
                          className={`mt-1 inline-block h-2.5 w-2.5 rounded-full ${toneToIndicator(tone)}`}
                          aria-hidden
                        />
                        <div className="space-y-1">
                          <div className="font-semibold text-foreground">{device.hostname}</div>
                          <p className="text-xs text-muted-foreground">
                            {device.domain ?? t('deviceDetail.messages.noDomain')}
                          </p>
                        </div>
                      </div>
                    </td>
                    {STATUS_FIELDS.map((field) => {
                      const statusTone = evaluateFieldTone(snapshot, field.key);
                      const value = formatFieldValue(snapshot, field.key, t);
                      return (
                        <td key={field.key} className="px-4 py-3">
                          <StatusBadge tone={statusTone} value={value} />
                        </td>
                      );
                    })}
                    <td className="px-4 py-3">
                      {riskScore !== null ? (
                        <StatusBadge tone={toneFromRisk(riskScore)} value={riskScore.toFixed(0)} />
                      ) : (
                        <StatusBadge tone="unknown" value="-" />
                      )}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {lastScan ? (
                        <div className="space-y-1">
                          <span>{formatDateTime(lastScan, locale)}</span>
                          <div className="text-xs text-muted-foreground">{formatRelativeTime(lastScan, locale)}</div>
                        </div>
                      ) : (
                        <span>{t('security.meta.noScan')}</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {status === 'loading' ? (
                        <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                      ) : lastUpdated ? (
                        <div className="space-y-1">
                          <span>{formatDateTime(lastUpdated, locale)}</span>
                          <div className="text-xs text-muted-foreground">{formatRelativeTime(lastUpdated, locale)}</div>
                        </div>
                      ) : (
                        <span>-</span>
                      )}
                      {status === 'error' && (
                        <div className="mt-2 flex items-center gap-1 text-xs text-destructive">
                          <AlertCircle className="h-3 w-3" />
                          <span>{error ?? t('security.error')}</span>
                        </div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        type="button"
                        onClick={() => handleRefreshRow(device.id)}
                        disabled={status === 'loading'}
                        className="inline-flex items-center gap-2 rounded-lg border border-border px-3 py-1 text-xs text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
                      >
                        {status === 'loading' ? (
                          <Loader2 className="h-3 w-3 animate-spin" />
                        ) : (
                          <RefreshCw className="h-3 w-3" />
                        )}
                        {t('security.refresh')}
                      </button>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function determineOverallTone(snapshot?: SecuritySnapshot | null): Tone {
  if (!snapshot) return 'unknown';

  const tones = STATUS_FIELDS.map((field) => evaluateFieldTone(snapshot, field.key));
  const dominantTone = tones.reduce<Tone>((acc, tone) => {
    return TONE_PRIORITY[tone] > TONE_PRIORITY[acc] ? tone : acc;
  }, 'unknown');

  const riskScore = parseRiskScore(snapshot.riskScore);
  if (riskScore !== null) {
    const riskTone = toneFromRisk(riskScore);
    return TONE_PRIORITY[riskTone] > TONE_PRIORITY[dominantTone] ? riskTone : dominantTone;
  }

  return dominantTone;
}

function evaluateFieldTone(snapshot?: SecuritySnapshot | null, key?: StatusFieldKey): Tone {
  if (!snapshot || !key) return 'unknown';

  switch (key) {
    case 'antivirus': {
      const products = snapshot.antivirus;
      if (!products) return 'unknown';
      if (!products.length) return 'bad';
      if (products.some((item) => item?.error)) return 'warning';
      const enabledCount = products.filter((item) => item?.enabled).length;
      const outdated = products.some((item) => item?.upToDate === false);
      if (enabledCount === 0) return 'bad';
      if (outdated) return 'warning';
      return 'good';
    }
    case 'firewall': {
      const firewall = snapshot.firewall;
      if (!firewall) return 'unknown';
      if (firewall.error) return 'warning';
      const profiles = Array.isArray(firewall.profiles) ? firewall.profiles : [];
      const products = Array.isArray(firewall.products) ? firewall.products : [];

      if (profiles.length > 0) {
        const enabledProfiles = profiles.filter((profile) => profile?.enabled).length;
        if (enabledProfiles === 0) return 'bad';
        if (enabledProfiles === profiles.length) {
          const hasProducts = products.length > 0;
          if (!hasProducts) {
            return 'good';
          }
        } else {
          return 'warning';
        }
      }

      if (products.length > 0) {
        const enabledProducts = products.filter((product) => product?.enabled).length;
        if (enabledProducts === 0) return 'bad';
        if (enabledProducts !== products.length) return 'warning';
        return 'good';
      }

      if (profiles.length > 0) {
        return 'warning';
      }

      return 'unknown';
    }
    case 'defender': {
      const defender = snapshot.defender;
      if (!defender) return 'unknown';
      if (defender.error) return 'warning';
      const bools = [
        defender.antivirusEnabled,
        defender.realtimeProtectionEnabled,
        defender.behaviorMonitorEnabled,
        defender.antiSpywareEnabled,
        defender.ioavProtectionEnabled,
        defender.onAccessProtectionEnabled,
      ].filter((value) => value !== undefined) as boolean[];
      if (!bools.length) return 'unknown';
      const enabled = bools.filter(Boolean).length;
      if (enabled === bools.length) return 'good';
      if (enabled === 0) return 'bad';
      return 'warning';
    }
    case 'uac': {
      const uac = snapshot.uac;
      if (!uac) return 'unknown';
      if (uac.error) return 'warning';
      if (uac.enabled === true) return 'good';
      if (uac.enabled === false) return 'bad';
      return 'unknown';
    }
    case 'encryption': {
      const encryption = snapshot.encryption;
      if (!encryption) return 'unknown';
      if (encryption.error) return 'warning';
      const volumes = Array.isArray(encryption.bitlockerVolumes) ? encryption.bitlockerVolumes : [];
      if (!volumes.length) return 'warning';
      const protectedVolumes = volumes.filter((volume) =>
        typeof volume?.protectionStatus === 'string' && volume.protectionStatus.toLowerCase().includes('protect'),
      ).length;
      if (protectedVolumes === volumes.length) return 'good';
      if (protectedVolumes === 0) return 'bad';
      return 'warning';
    }
    default:
      return 'unknown';
  }
}

function formatFieldValue(snapshot: SecuritySnapshot | null | undefined, key: StatusFieldKey, t: Translator): string {
  const unknown = t('deviceDetail.security.states.unknown', undefined, 'Unknown');
  const enabled = t('deviceDetail.security.states.enabled', undefined, 'Enabled');
  const disabled = t('deviceDetail.security.states.disabled', undefined, 'Disabled');
  const notInstalled = t('deviceDetail.security.states.notInstalled', undefined, 'Not installed');
  const warning = t('security.statusStates.warning', undefined, 'Needs review');
  const protectedLabel = t('security.statusStates.good', undefined, 'Protected');

  if (!snapshot) return unknown;

  switch (key) {
    case 'antivirus': {
      const products = snapshot.antivirus;
      if (!products) return unknown;
      if (!products.length) return notInstalled;
      const error = products.find((item) => item?.error)?.error;
      if (error) return error;
      const enabledCount = products.filter((item) => item?.enabled).length;
      const outdated = products.some((item) => item?.upToDate === false);
      if (enabledCount === 0) return disabled;
      if (outdated) return warning;
      return enabled;
    }
    case 'firewall': {
      const firewall = snapshot.firewall;
      if (!firewall) return unknown;
      if (firewall.error) return firewall.error;
      const profiles = Array.isArray(firewall.profiles) ? firewall.profiles : [];
      const products = Array.isArray(firewall.products) ? firewall.products : [];

      if (profiles.length > 0) {
        const enabledProfiles = profiles.filter((profile) => profile?.enabled).length;
        if (enabledProfiles === 0) return disabled;
        if (enabledProfiles === profiles.length) {
          const enabledProducts = products.filter((product) => product?.enabled).length;
          if (products.length === 0 || enabledProducts === products.length) {
            return enabled;
          }
          if (enabledProducts === 0) return disabled;
          return warning;
        }
        return warning;
      }

      if (products.length > 0) {
        const enabledProducts = products.filter((product) => product?.enabled).length;
        if (enabledProducts === 0) return disabled;
        if (enabledProducts === products.length) return enabled;
        return warning;
      }

      return unknown;
    }
    case 'defender': {
      const defender = snapshot.defender;
      if (!defender) return unknown;
      if (defender.error) return defender.error;
      const bools = [
        defender.antivirusEnabled,
        defender.realtimeProtectionEnabled,
        defender.behaviorMonitorEnabled,
        defender.antiSpywareEnabled,
        defender.ioavProtectionEnabled,
        defender.onAccessProtectionEnabled,
      ].filter((value) => value !== undefined) as boolean[];
      if (!bools.length) return unknown;
      const enabledCount = bools.filter(Boolean).length;
      if (enabledCount === bools.length) return protectedLabel;
      if (enabledCount === 0) return disabled;
      return warning;
    }
    case 'uac': {
      const uac = snapshot.uac;
      if (!uac) return unknown;
      if (uac.error) return uac.error;
      if (uac.enabled === true) return enabled;
      if (uac.enabled === false) return disabled;
      return unknown;
    }
    case 'encryption': {
      const encryption = snapshot.encryption;
      if (!encryption) return unknown;
      if (encryption.error) return encryption.error;
      const volumes = Array.isArray(encryption.bitlockerVolumes) ? encryption.bitlockerVolumes : [];
      if (!volumes.length) return notInstalled;
      const protectedVolumes = volumes.filter((volume) =>
        typeof volume?.protectionStatus === 'string' && volume.protectionStatus.toLowerCase().includes('protect'),
      ).length;
      if (protectedVolumes === volumes.length) return enabled;
      if (protectedVolumes === 0) return disabled;
      return warning;
    }
    default:
      return unknown;
  }
}

function parseRiskScore(value?: number | string | null): number | null {
  if (value === undefined || value === null || value === '') return null;
  const numeric = Number(value);
  if (Number.isNaN(numeric)) return null;
  return Math.min(Math.max(numeric, 0), 100);
}

function toneFromRisk(score: number): Tone {
  if (score <= 33) return 'good';
  if (score <= 66) return 'warning';
  return 'bad';
}

function toneToIndicator(tone: Tone): string {
  if (tone === 'bad') return 'bg-destructive';
  if (tone === 'warning') return 'bg-amber-500';
  if (tone === 'good') return 'bg-emerald-500';
  return 'bg-muted-foreground/60';
}

function StatusBadge({ tone, value }: { tone: Tone; value: string | number }) {
  return (
    <span className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-medium ${TONE_BADGE[tone]}`}>
      {value}
    </span>
  );
}

function SecurityStat({
  icon: Icon,
  label,
  value,
  tone = 'default',
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  tone?: 'default' | 'good' | 'warning' | 'bad';
}) {
  const background =
    tone === 'good'
      ? 'bg-emerald-500/10 text-emerald-600'
      : tone === 'warning'
        ? 'bg-amber-500/10 text-amber-600'
        : tone === 'bad'
          ? 'bg-destructive/10 text-destructive'
          : 'bg-primary/10 text-primary';
  const valueClass =
    tone === 'good'
      ? 'text-emerald-600'
      : tone === 'warning'
        ? 'text-amber-600'
        : tone === 'bad'
          ? 'text-destructive'
          : 'text-foreground';

  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <div className="mb-3 flex items-center gap-2">
        <div className={`flex h-9 w-9 items-center justify-center rounded-full ${background}`}>
          <Icon className="h-4 w-4" />
        </div>
        <span className="text-sm font-semibold text-foreground">{label}</span>
      </div>
      <p className={`text-2xl font-semibold ${valueClass}`}>{value}</p>
    </div>
  );
}

function FilterBar({
  filter,
  onFilterChange,
}: {
  filter: 'all' | Tone;
  onFilterChange: (value: 'all' | Tone) => void;
}) {
  const { t } = useTranslation();
  const options: Array<{ key: 'all' | Tone; label: string }> = [
    { key: 'all', label: t('common.viewAll') ?? 'Tümü' },
    { key: 'good', label: t('security.risk.low') },
    { key: 'warning', label: t('security.risk.medium') },
    { key: 'bad', label: t('security.risk.high') },
    { key: 'unknown', label: t('security.statusStates.unknown') },
  ];

  return (
    <div className="flex flex-wrap items-center gap-2">
      {options.map((option) => (
        <button
          key={option.key}
          type="button"
          onClick={() => onFilterChange(option.key)}
          className={`rounded-full px-3 py-1 text-xs font-medium transition ${
            filter === option.key
              ? 'bg-primary text-primary-foreground shadow'
              : 'bg-secondary/60 text-muted-foreground hover:bg-secondary/80'
          }`}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}


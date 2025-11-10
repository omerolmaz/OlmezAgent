import { useCallback, useEffect, useMemo, useState } from 'react';
import { Layers3, RefreshCw, Search, Loader2, AlertCircle } from 'lucide-react';
import { deviceService } from '../services/device.service';
import { inventoryService, type InventoryDetail } from '../services/inventory.service';
import type { Device } from '../types/device.types';
import InventoryOverview from '../components/inventory/InventoryOverview';
import { useTranslation } from '../hooks/useTranslation';
import { formatBytes, formatDateTime, formatRelativeTime } from '../utils/format';
import { toErrorMessage } from '../utils/error';

type InventoryRowState = {
  device: Device;
  status: 'loading' | 'ready' | 'error';
  detail?: InventoryDetail;
  error?: string;
};

const STATUS_COLORS: Record<Device['status'], string> = {
  Connected: 'bg-emerald-500',
  Connecting: 'bg-blue-400',
  Disconnected: 'bg-muted-foreground/40',
  Reconnecting: 'bg-amber-500',
  Error: 'bg-destructive',
};

const STALE_THRESHOLD_MS = 1000 * 60 * 60 * 24; // 24 saat

export default function Inventory() {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const [devices, setDevices] = useState<Device[]>([]);
  const [rows, setRows] = useState<Record<string, InventoryRowState>>({});
  const [selectedDeviceId, setSelectedDeviceId] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [pageError, setPageError] = useState<string>('');
  const [pageLoading, setPageLoading] = useState(false);
  const [summariesLoading, setSummariesLoading] = useState(false);

  const deviceStatusLabels = useMemo(
    () => ({
      Connected: t('devices.statusLabels.Connected'),
      Connecting: t('devices.statusLabels.Connecting'),
      Disconnected: t('devices.statusLabels.Disconnected'),
      Reconnecting: t('devices.statusLabels.Reconnecting'),
      Error: t('devices.statusLabels.Error'),
    }),
    [t],
  );

  const rowList: InventoryRowState[] = useMemo(
    () => devices.map((device) => rows[device.id] ?? { device, status: 'loading' as const }),
    [devices, rows],
  );

  const filteredRows = useMemo(() => {
    const normalized = search.trim().toLowerCase();
    if (!normalized) return rowList;
    return rowList.filter((row) => {
      const { device, detail } = row;
      const haystack = [
        device.hostname,
        device.domain,
        device.osVersion,
        detail?.manufacturer,
        detail?.model,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return haystack.includes(normalized);
    });
  }, [rowList, search]);

  const summaryStats = useMemo(() => {
    const withInventory = rowList.filter((row) => row.detail);
    const stale = withInventory.filter(
      (row) =>
        row.detail &&
        Date.now() - new Date(row.detail.collectedAt ?? row.detail.updatedAt).getTime() > STALE_THRESHOLD_MS,
    );
    return {
      total: rowList.length,
      collected: withInventory.length,
      stale: stale.length,
    };
  }, [rowList]);

  const selectedRow = selectedDeviceId ? rows[selectedDeviceId] : null;

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

  const loadSummaries = useCallback(
    async (targetDevices?: Device[]) => {
      const list = targetDevices ?? devices;
      if (!list.length) return;

      setSummariesLoading(true);
      let pointer = 0;

      const worker = async () => {
        while (pointer < list.length) {
          const currentIndex = pointer++;
          const device = list[currentIndex];
          ensureRow(device);
          try {
            const result = await inventoryService.getFullInventory(device.id);
            if (!result.success || !result.data) {
              throw new Error(result.error ?? t('inventory.error'));
            }
            setRows((prev) => ({
              ...prev,
              [device.id]: {
                device,
                status: 'ready',
                detail: result.data,
              },
            }));
          } catch (error) {
            setRows((prev) => ({
              ...prev,
              [device.id]: {
                device,
                status: 'error',
                error: toErrorMessage(error, t('inventory.error')),
                detail: prev[device.id]?.detail,
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
        setSelectedDeviceId((current) => current ?? list[0].id);
        await loadSummaries(list);
      } else {
        setRows({});
        setSelectedDeviceId(null);
      }
    } catch (error) {
      setPageError(toErrorMessage(error, t('inventory.error')));
    } finally {
      setPageLoading(false);
    }
  }, [ensureRow, loadSummaries, t]);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  useEffect(() => {
    if (selectedDeviceId && rows[selectedDeviceId]?.detail) {
      return;
    }
    const firstWithDetail = rowList.find((row) => row.detail);
    if (firstWithDetail) {
      setSelectedDeviceId(firstWithDetail.device.id);
    }
  }, [rowList, rows, selectedDeviceId]);

  const handleRefreshAll = useCallback(async () => {
    await loadSummaries(devices);
  }, [devices, loadSummaries]);

  const renderStatusBadge = (device: Device) => {
    const label = deviceStatusLabels[device.status] ?? device.status;
    const tone = STATUS_COLORS[device.status] ?? 'bg-muted-foreground';
    return (
      <span className="inline-flex items-center gap-2">
        <span className={`inline-block h-2 w-2 rounded-full ${tone}`} aria-hidden />
        <span>{label}</span>
      </span>
    );
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{t('inventory.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('inventory.description')}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="relative flex items-center">
            <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder={t('inventory.searchPlaceholder', undefined, t('common.searchPlaceholder'))}
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
            {t('inventory.refresh')}
          </button>
        </div>
      </div>

      {pageError && (
        <div className="flex items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{pageError}</span>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-3">
        <InventoryStat
          icon={Layers3}
          label={t('devices.title')}
          value={summaryStats.total.toString()}
        />
        <InventoryStat
          icon={RefreshCw}
          label={t('inventory.refreshing', undefined, t('inventory.refresh'))}
          value={summaryStats.collected.toString()}
        />
        <InventoryStat
          icon={AlertCircle}
          label={t('inventory.notCollected')}
          value={summaryStats.stale.toString()}
          tone="warning"
        />
      </div>

      <div className="overflow-hidden rounded-xl border border-border bg-card shadow-sm">
        <table className="min-w-full divide-y divide-border text-sm">
          <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">{t('inventory.device')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.labels.osName')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.labels.manufacturer')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.labels.totalPhysicalMemory')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.labels.totalDiskSpace')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.softwareTitle')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.patchesTitle')}</th>
              <th className="px-4 py-3 text-left">{t('inventory.labels.collectedAt')}</th>
            </tr>
          </thead>
          <tbody>
            {pageLoading ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                  <Loader2 className="mr-2 inline h-4 w-4 animate-spin" />
                  {t('inventory.loading')}
                </td>
              </tr>
            ) : filteredRows.length === 0 ? (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                  {t('inventory.empty')}
                </td>
              </tr>
            ) : (
              filteredRows.map((row) => {
                const { device, detail, status, error } = row;
                const selected = selectedDeviceId === device.id;
                const collectedAt = detail?.collectedAt ?? detail?.updatedAt;
                const isStale =
                  collectedAt &&
                  Date.now() - new Date(collectedAt).getTime() > STALE_THRESHOLD_MS;
                return (
                  <tr
                    key={device.id}
                    onClick={() => setSelectedDeviceId(device.id)}
                    className={`cursor-pointer border-t border-border/60 transition hover:bg-secondary/40 ${
                      selected ? 'bg-secondary/30' : ''
                    }`}
                  >
                    <td className="px-4 py-3">
                      <div className="space-y-1">
                        <div className="font-semibold text-foreground">{device.hostname}</div>
                        <div className="text-xs text-muted-foreground">{renderStatusBadge(device)}</div>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {detail?.osName ?? device.osVersion ?? t('devices.unknownOs')}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {detail
                        ? [detail.manufacturer, detail.model].filter(Boolean).join(' ') || '-'
                        : '-'}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {detail ? formatBytes(detail.totalPhysicalMemoryBytes) : '-'}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {detail ? formatBytes(detail.totalDiskSpaceBytes) : '-'}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {detail?.software?.length ?? 0}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {detail?.patches?.length ?? 0}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {status === 'loading' ? (
                        <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
                      ) : status === 'error' ? (
                        <span className="flex items-center gap-1 text-destructive">
                          <AlertCircle className="h-4 w-4" />
                          {error ?? t('inventory.error')}
                        </span>
                      ) : detail ? (
                        <div className="space-y-1">
                          <span className={isStale ? 'text-amber-600' : undefined}>
                            {formatDateTime(detail.collectedAt ?? detail.updatedAt, locale)}
                          </span>
                          <div className="text-xs text-muted-foreground">
                            {formatRelativeTime(detail.collectedAt ?? detail.updatedAt, locale)}
                          </div>
                        </div>
                      ) : (
                        <span>{t('inventory.notCollected')}</span>
                      )}
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {selectedRow?.detail ? (
        <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="text-lg font-semibold text-foreground">
                {selectedRow.device.hostname}
              </h2>
              <p className="text-xs text-muted-foreground">
                {t('inventory.labels.collectedAt')}: {formatDateTime(selectedRow.detail.collectedAt, locale)}
              </p>
            </div>
          </div>
          <InventoryOverview data={selectedRow.detail} locale={locale} />
        </div>
      ) : selectedRow && selectedRow.status === 'error' ? (
        <div className="flex items-center gap-3 rounded-xl border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{selectedRow.error ?? t('inventory.error')}</span>
        </div>
      ) : null}
    </div>
  );
}

function InventoryStat({
  icon: Icon,
  label,
  value,
  tone = 'default',
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  tone?: 'default' | 'warning';
}) {
  const toneClass = tone === 'warning' ? 'text-amber-600' : 'text-foreground';
  const backgroundClass = tone === 'warning' ? 'bg-amber-500/10 text-amber-600' : 'bg-primary/10 text-primary';

  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <div className="mb-3 flex items-center gap-2">
        <div className={`flex h-9 w-9 items-center justify-center rounded-full ${backgroundClass}`}>
          <Icon className="h-4 w-4" />
        </div>
        <span className="text-sm font-semibold text-foreground">{label}</span>
      </div>
      <p className={`text-2xl font-semibold ${toneClass}`}>{value}</p>
    </div>
  );
}

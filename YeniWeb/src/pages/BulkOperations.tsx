import { Children, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Upload, Download, Layers3, AlertCircle, Loader2 } from 'lucide-react';
import { runBulkOperation, type BulkOperationType, type BulkOperationResult } from '../services/bulkOperations.service';
import { inventoryService, type InventoryDetail } from '../services/inventory.service';
import { deviceService } from '../services/device.service';
import type { Device } from '../types/device.types';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';
import { formatBytes, formatDateTime } from '../utils/format';

type SoftwareOption = {
  id: string;
  name: string;
  version?: string;
  devices: string[];
  sizeBytes?: number;
};

type PatchOption = {
  id: string;
  description?: string;
  devices: string[];
  lastInstalled?: string;
};

interface CatalogState {
  loading: boolean;
  error: string;
  software: SoftwareOption[];
  patches: PatchOption[];
}

export default function BulkOperations() {
  const location = useLocation();
  const navigate = useNavigate();
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const selectedDevices = useMemo(
    () => (Array.isArray(location.state?.selectedDevices) ? (location.state.selectedDevices as string[]) : []),
    [location.state],
  );

  const [operation, setOperation] = useState<BulkOperationType>('install');
  const [installTarget, setInstallTarget] = useState({ packagePath: '', parameters: '' });
  const [uninstallTarget, setUninstallTarget] = useState('');
  const [updateSelection, setUpdateSelection] = useState<string[]>([]);
  const [patchUrl, setPatchUrl] = useState('');
  const [schedule, setSchedule] = useState({ date: '', time: '' });
  const [results, setResults] = useState<BulkOperationResult[]>([]);
  const [running, setRunning] = useState(false);
  const [formError, setFormError] = useState('');
  const [catalog, setCatalog] = useState<CatalogState>({
    loading: false,
    error: '',
    software: [],
    patches: [],
  });
  const [deviceMap, setDeviceMap] = useState<Record<string, Device>>({});

  const scheduledTime = useMemo(() => {
    if (!schedule.date || !schedule.time) return undefined;
    return new Date(`${schedule.date}T${schedule.time}:00`).toISOString();
  }, [schedule.date, schedule.time]);

  useEffect(() => {
    let cancelled = false;

    async function loadDevices() {
      try {
        const list = await deviceService.getDevices();
        if (cancelled) return;
        const map: Record<string, Device> = {};
        list.forEach((device) => {
          if (selectedDevices.includes(device.id)) {
            map[device.id] = device;
          }
        });
        setDeviceMap(map);
      } catch {
        // Cihaz eşlemeleri olmazsa sonuçlarda ID gösterilir.
      }
    }

    void loadDevices();
    return () => {
      cancelled = true;
    };
  }, [selectedDevices]);

  useEffect(() => {
    if (!selectedDevices.length) {
      setCatalog({ loading: false, error: '', software: [], patches: [] });
      return;
    }

    let cancelled = false;

    async function loadCatalog() {
      setCatalog({ loading: true, error: '', software: [], patches: [] });
      try {
        const summaries = await fetchInventorySummaries(selectedDevices);
        if (cancelled) return;
        const { software, patches } = buildCatalogs(selectedDevices, summaries);
        setCatalog({
          loading: false,
          error: '',
          software: software.sort((a, b) => a.name.localeCompare(b.name)),
          patches: patches.sort((a, b) => a.id.localeCompare(b.id)),
        });
      } catch (error) {
        if (cancelled) return;
        setCatalog({
          loading: false,
          error: toErrorMessage(error, t('bulk.catalogError', undefined, 'Catalog could not be loaded.')),
          software: [],
          patches: [],
        });
      }
    }

    void loadCatalog();
    return () => {
      cancelled = true;
    };
  }, [selectedDevices, t]);

  const selectedDevicesLabel = useMemo(() => {
    if (!selectedDevices.length) {
      return t('bulk.noDevicesSelected', undefined, 'No devices selected.');
    }
    return selectedDevices
      .map((deviceId) => deviceMap[deviceId]?.hostname ?? deviceId)
      .join(', ');
  }, [deviceMap, selectedDevices, t]);

  const handleRun = useCallback(async () => {
    if (!selectedDevices.length) {
      setFormError(t('bulk.noDevicesSelected', undefined, 'No devices selected.'));
      return;
    }

    const payload: Record<string, unknown> = {};

    if (operation === 'install') {
      if (!installTarget.packagePath.trim()) {
        setFormError(t('bulk.validation.packagePath', undefined, 'Please provide a package path.'));
        return;
      }
      payload.packagePath = installTarget.packagePath.trim();
      if (installTarget.parameters.trim()) {
        payload.arguments = installTarget.parameters.trim();
      }
    }

    if (operation === 'uninstall') {
      if (!uninstallTarget) {
        setFormError(t('bulk.validation.software', undefined, 'Select the software to uninstall.'));
        return;
      }
      payload.productName = uninstallTarget;
    }

    if (operation === 'update') {
      if (!updateSelection.length) {
        setFormError(t('bulk.validation.updates', undefined, 'Select one or more updates.'));
        return;
      }
      payload.updateIds = updateSelection;
    }

    if (operation === 'patch') {
      if (!patchUrl.trim()) {
        setFormError(t('bulk.validation.patchUrl', undefined, 'Provide a patch URL.'));
        return;
      }
      payload.patchUrl = patchUrl.trim();
    }

    setFormError('');
    setRunning(true);
    setResults([]);
    try {
      const outcome = await runBulkOperation({
        deviceIds: selectedDevices,
        operation,
        payload,
        scheduledTime,
      });
      setResults(outcome);
    } catch (error) {
      setFormError(toErrorMessage(error, t('bulk.executionError', undefined, 'Bulk operation failed.')));
    } finally {
      setRunning(false);
    }
  }, [installTarget, operation, patchUrl, scheduledTime, selectedDevices, t, uninstallTarget, updateSelection]);

  const resetForm = useCallback(() => {
    setInstallTarget({ packagePath: '', parameters: '' });
    setUninstallTarget('');
    setUpdateSelection([]);
    setPatchUrl('');
    setSchedule({ date: '', time: '' });
    setFormError('');
    setResults([]);
  }, []);

  return (
    <div className="space-y-6 p-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{t('bulk.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('bulk.subtitle')}</p>
        </div>
        <button
          onClick={() => navigate('/devices')}
          className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm text-muted-foreground transition hover:bg-secondary/70"
        >
          {t('bulk.back')}
        </button>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-4 flex items-center gap-2 text-sm text-muted-foreground">
          <Layers3 className="h-4 w-4 text-primary" />
          {t('bulk.selected', { count: selectedDevices.length })}
        </div>
        <p className="text-sm text-muted-foreground">{selectedDevicesLabel}</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <CatalogCard
          loading={catalog.loading}
          error={catalog.error}
          title={t('bulk.softwareLibrary')}
          emptyLabel={t('bulk.noSoftwareData', undefined, 'No software inventory collected.')}
        >
          <table className="min-w-full divide-y divide-border text-xs">
            <thead className="bg-secondary/40 text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left">{t('bulk.softwareName', undefined, 'Software')}</th>
                <th className="px-3 py-2 text-left">{t('inventory.labels.version')}</th>
                <th className="px-3 py-2 text-left">{t('bulk.softwareSize', undefined, 'Size')}</th>
                <th className="px-3 py-2 text-left">{t('bulk.softwareDevices', undefined, 'Devices')}</th>
              </tr>
            </thead>
            <tbody>
              {catalog.software.map((option) => (
                <tr key={option.id} className="border-t border-border/60">
                  <td className="px-3 py-2 text-foreground">{option.name}</td>
                  <td className="px-3 py-2 text-muted-foreground">{option.version ?? '-'}</td>
                  <td className="px-3 py-2 text-muted-foreground">
                    {option.sizeBytes ? formatBytes(option.sizeBytes) : '-'}
                  </td>
                  <td className="px-3 py-2 text-muted-foreground">{option.devices.length}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </CatalogCard>

        <CatalogCard
          loading={catalog.loading}
          error={catalog.error}
          title={t('bulk.patchLibrary')}
          emptyLabel={t('bulk.noPatchData', undefined, 'No patch data available.')}
        >
          <table className="min-w-full divide-y divide-border text-xs">
            <thead className="bg-secondary/40 text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left">{t('bulk.patchId', undefined, 'Patch')}</th>
                <th className="px-3 py-2 text-left">{t('bulk.patchDescription', undefined, 'Description')}</th>
                <th className="px-3 py-2 text-left">{t('bulk.patchDevices', undefined, 'Devices')}</th>
                <th className="px-3 py-2 text-left">{t('bulk.patchLastInstalled', undefined, 'Last installed')}</th>
              </tr>
            </thead>
            <tbody>
              {catalog.patches.map((option) => (
                <tr key={option.id} className="border-t border-border/60">
                  <td className="px-3 py-2 text-foreground">{option.id}</td>
                  <td className="px-3 py-2 text-muted-foreground">{option.description ?? '-'}</td>
                  <td className="px-3 py-2 text-muted-foreground">{option.devices.length}</td>
                  <td className="px-3 py-2 text-muted-foreground">
                    {option.lastInstalled ? formatDateTime(option.lastInstalled, locale) : '-'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </CatalogCard>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <h2 className="mb-4 text-sm font-semibold">{t('bulk.configuration', undefined, 'Configuration')}</h2>
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('bulk.operation')}</label>
            <select
              value={operation}
              onChange={(event) => {
                setOperation(event.target.value as BulkOperationType);
                setFormError('');
              }}
              className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
            >
              <option value="install">{t('bulk.operations.install')}</option>
              <option value="uninstall">{t('bulk.operations.uninstall')}</option>
              <option value="update">{t('bulk.operations.update')}</option>
              <option value="patch">{t('bulk.operations.patch')}</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('bulk.scheduleDate')}</label>
            <input
              type="date"
              value={schedule.date}
              onChange={(event) => setSchedule((prev) => ({ ...prev, date: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('bulk.scheduleTime')}</label>
            <input
              type="time"
              value={schedule.time}
              onChange={(event) => setSchedule((prev) => ({ ...prev, time: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
            />
          </div>
        </div>

        <div className="mt-6 space-y-4">
          {operation === 'install' && (
            <>
              <div>
                <label className="mb-1 block text-xs uppercase text-muted-foreground">
                  {t('bulk.packagePathLabel', undefined, 'Package path')}
                </label>
                <input
                  value={installTarget.packagePath}
                  onChange={(event) => setInstallTarget((prev) => ({ ...prev, packagePath: event.target.value }))}
                  placeholder={t('bulk.installPlaceholder')}
                  className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
                />
              </div>
              <div>
                <label className="mb-1 block text-xs uppercase text-muted-foreground">
                  {t('bulk.packageArgsLabel', undefined, 'Install arguments')}
                </label>
                <input
                  value={installTarget.parameters}
                  onChange={(event) => setInstallTarget((prev) => ({ ...prev, parameters: event.target.value }))}
                  placeholder={t('bulk.genericPlaceholder')}
                  className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
                />
              </div>
            </>
          )}

          {operation === 'uninstall' && (
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('bulk.selectSoftware', undefined, 'Select software')}
              </label>
              <select
                value={uninstallTarget}
                onChange={(event) => setUninstallTarget(event.target.value)}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              >
                <option value="">{t('bulk.selectSoftwarePlaceholder', undefined, 'Choose software')}</option>
                {catalog.software.map((option) => (
                  <option key={option.id} value={option.name}>
                    {option.name}
                    {option.version ? ` ${option.version}` : ''} · {option.devices.length}{' '}
                    {t('bulk.softwareDevices', undefined, 'devices')}
                  </option>
                ))}
              </select>
            </div>
          )}

          {operation === 'update' && (
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('bulk.selectUpdates', undefined, 'Select updates')}
              </label>
              <select
                multiple
                value={updateSelection}
                onChange={(event) =>
                  setUpdateSelection(Array.from(event.target.selectedOptions).map((option) => option.value))
                }
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              >
                {catalog.patches.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.id} · {option.devices.length} {t('bulk.softwareDevices', undefined, 'devices')}
                  </option>
                ))}
              </select>
              <p className="mt-2 text-xs text-muted-foreground">
                {t('bulk.updateHint', undefined, 'Updates will be requested from each agent.')}
              </p>
            </div>
          )}

          {operation === 'patch' && (
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('bulk.patchUrlLabel', undefined, 'Patch URL')}
              </label>
              <input
                value={patchUrl}
                onChange={(event) => setPatchUrl(event.target.value)}
                placeholder="https://example.com/patch.msu"
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>
          )}
        </div>

        {formError && (
          <div className="mt-4 flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
            <AlertCircle className="h-3 w-3" />
            <span>{formError}</span>
          </div>
        )}

        <div className="mt-6 flex items-center gap-2">
          <button
            onClick={handleRun}
            disabled={running || !selectedDevices.length}
            className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {running ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
            {running ? t('bulk.executing') : t('bulk.execute')}
          </button>
          <button
            onClick={resetForm}
            className="inline-flex items-center gap-2 rounded-lg border border-border px-4 py-2 text-sm text-muted-foreground transition hover:bg-secondary/70"
          >
            <Download className="h-4 w-4" />
            {t('bulk.clear')}
          </button>
        </div>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <h2 className="mb-4 text-sm font-semibold">{t('bulk.results')}</h2>
        {results.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('bulk.empty')}</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-border text-sm">
              <thead className="bg-secondary/40 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">{t('inventory.device')}</th>
                  <th className="px-4 py-3 text-left">{t('commands.table.status')}</th>
                  <th className="px-4 py-3 text-left">{t('commands.table.result')}</th>
                  <th className="px-4 py-3 text-left">{t('commands.table.finished')}</th>
                </tr>
              </thead>
              <tbody>
                {results.map((result) => {
                  const device = deviceMap[result.deviceId];
                  return (
                    <tr key={`${result.deviceId}-${result.command?.id ?? Math.random()}`} className="border-t border-border/60">
                      <td className="px-4 py-3 font-semibold text-foreground">
                        {device?.hostname ?? result.deviceId}
                      </td>
                      <td className="px-4 py-3">
                        <span
                          className={`inline-flex items-center rounded-full px-3 py-1 text-xs font-medium ${
                            result.success ? 'bg-emerald-500/10 text-emerald-600' : 'bg-destructive/10 text-destructive'
                          }`}
                        >
                          {result.success ? t('bulk.success') : t('bulk.failed')}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs text-muted-foreground">
                        {result.error ?? result.command?.result ?? '-'}
                      </td>
                      <td className="px-4 py-3 text-xs text-muted-foreground">
                        {result.command?.completedAt
                          ? formatDateTime(result.command.completedAt, locale)
                          : '-'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

async function fetchInventorySummaries(deviceIds: string[]): Promise<Record<string, InventoryDetail>> {
  const summaries: Record<string, InventoryDetail> = {};

  for (const deviceId of deviceIds) {
    const response = await inventoryService.getFullInventory(deviceId);
    if (!response.success || !response.data) {
      throw new Error(response.error ?? 'Inventory data not available.');
    }
    summaries[deviceId] = response.data;
  }

  return summaries;
}

function buildCatalogs(
  deviceIds: string[],
  inventories: Record<string, InventoryDetail>,
): { software: SoftwareOption[]; patches: PatchOption[] } {
  const softwareMap = new Map<string, SoftwareOption>();
  const patchMap = new Map<string, PatchOption>();

  deviceIds.forEach((deviceId) => {
    const detail = inventories[deviceId];
    if (!detail) return;

    detail.software?.forEach((item) => {
      const key = `${item.name}|${item.version ?? ''}`.toLowerCase();
      const existing = softwareMap.get(key);
      if (existing) {
        if (!existing.devices.includes(deviceId)) {
          existing.devices.push(deviceId);
        }
        if (!existing.sizeBytes && item.sizeInBytes) {
          existing.sizeBytes = item.sizeInBytes;
        }
      } else {
        softwareMap.set(key, {
          id: key,
          name: item.name,
          version: item.version ?? undefined,
          devices: [deviceId],
          sizeBytes: item.sizeInBytes ?? undefined,
        });
      }
    });

    detail.patches?.forEach((item) => {
      const id = item.hotFixId || item.id || item.description || 'unknown';
      const key = id.toLowerCase();
      const existing = patchMap.get(key);
      if (existing) {
        if (!existing.devices.includes(deviceId)) {
          existing.devices.push(deviceId);
        }
        if (!existing.lastInstalled && item.installedOn) {
          existing.lastInstalled = item.installedOn;
        }
      } else {
        patchMap.set(key, {
          id,
          description: item.description ?? undefined,
          devices: [deviceId],
          lastInstalled: item.installedOn ?? undefined,
        });
      }
    });
  });

  return {
    software: Array.from(softwareMap.values()),
    patches: Array.from(patchMap.values()),
  };
}

function CatalogCard({
  loading,
  error,
  title,
  emptyLabel,
  children,
}: {
  loading: boolean;
  error: string;
  title: string;
  emptyLabel: string;
  children: ReactNode;
}) {
  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-foreground">{title}</h2>
        {loading && <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />}
      </div>
      {error ? (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
          <AlertCircle className="h-3 w-3" />
          <span>{error}</span>
        </div>
      ) : (
        <div className="overflow-auto rounded-lg border border-border/60">
          {Children.count(children) === 0 ? (
            <p className="p-3 text-xs text-muted-foreground">{emptyLabel}</p>
          ) : (
            children
          )}
        </div>
      )}
    </div>
  );
}

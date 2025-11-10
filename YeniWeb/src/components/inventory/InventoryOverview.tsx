import { useMemo } from 'react';
import type { ReactNode } from 'react';
import { useTranslation } from '../../hooks/useTranslation';
import type { InventoryDetail } from '../../services/inventory.service';

interface InventoryOverviewProps {
  data: InventoryDetail;
  locale: string;
  className?: string;
}

export function InventoryOverview({ data, locale, className }: InventoryOverviewProps) {
  const { t } = useTranslation();
  const containerClass = className ? `space-y-6 ${className}` : 'space-y-6';

  const summaryCards = useMemo(
    () =>
      [
        { key: 'collectedAt', value: formatDate(data.collectedAt, locale) },
        { key: 'updatedAt', value: formatDate(data.updatedAt, locale) },
        { key: 'hostName', value: fallback(data.hostName) },
        { key: 'primaryIpAddress', value: fallback(data.primaryIpAddress) },
        { key: 'primaryMacAddress', value: fallback(data.primaryMacAddress) },
        { key: 'domainName', value: fallback(data.domainName) },
      ].map((card) => ({
        ...card,
        label: t(`inventory.labels.${card.key}`),
      })),
    [data.collectedAt, data.updatedAt, data.hostName, data.primaryIpAddress, data.primaryMacAddress, data.domainName, t, locale],
  );

  const systemSection = useMemo(
    () =>
      buildSection('system', t, [
        { key: 'manufacturer', value: data.manufacturer },
        { key: 'model', value: data.model },
        { key: 'serialNumber', value: data.serialNumber },
        { key: 'systemSku', value: data.systemSku },
        { key: 'systemFamily', value: data.systemFamily },
        { key: 'chassisType', value: data.chassisType },
      ]),
    [data.manufacturer, data.model, data.serialNumber, data.systemSku, data.systemFamily, data.chassisType, t],
  );

  const osSection = useMemo(
    () =>
      buildSection('operatingSystem', t, [
        { key: 'osName', value: data.osName },
        { key: 'osVersion', value: data.osVersion },
        { key: 'osBuild', value: data.osBuild },
        { key: 'osArchitecture', value: data.osArchitecture },
        { key: 'osInstallDate', value: data.osInstallDate, formatter: (value) => formatDate(value, locale) },
      ]),
    [data.osName, data.osVersion, data.osBuild, data.osArchitecture, data.osInstallDate, t, locale],
  );

  const processorSection = useMemo(
    () =>
      buildSection('processor', t, [
        { key: 'processorName', value: data.processorName },
        { key: 'processorManufacturer', value: data.processorManufacturer },
        { key: 'processorArchitecture', value: data.processorArchitecture },
        { key: 'processorCores', value: data.processorCores },
        { key: 'processorLogicalProcessors', value: data.processorLogicalProcessors },
        {
          key: 'processorMaxClockSpeed',
          value: data.processorMaxClockSpeed,
          formatter: (value) => (value ? `${value} MHz` : '-'),
        },
      ]),
    [
      data.processorName,
      data.processorManufacturer,
      data.processorArchitecture,
      data.processorCores,
      data.processorLogicalProcessors,
      data.processorMaxClockSpeed,
      t,
    ],
  );

  const memorySection = useMemo(
    () =>
      buildSection('memory', t, [
        {
          key: 'totalPhysicalMemory',
          value: data.totalPhysicalMemoryBytes,
          formatter: (value) => formatBytes(Number(value)),
        },
        { key: 'memorySlots', value: data.memorySlots },
        { key: 'memoryType', value: data.memoryType },
        {
          key: 'memorySpeed',
          value: data.memorySpeed,
          formatter: (value) => (value ? `${value} MHz` : '-'),
        },
      ]),
    [data.totalPhysicalMemoryBytes, data.memorySlots, data.memoryType, data.memorySpeed, t],
  );

  const hasMonitorInfo = data.monitors.length > 0;

  const storageSection = buildSection(
    'storage',
    t,
    [
      {
        key: 'totalDiskSpace',
        value: data.totalDiskSpaceBytes,
        formatter: (value) => formatBytes(Number(value)),
      },
      { key: 'diskCount', value: data.diskCount },
    ],
    data.diskDrives.length > 0 ? (
      <div className="mt-3 max-h-60 overflow-auto text-sm text-muted-foreground">
        <table className="min-w-full divide-y divide-border">
          <thead className="bg-secondary/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-3 py-2 text-left">{t('inventory.tables.disks.name')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.disks.size')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.disks.free')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.disks.filesystem')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/70">
            {data.diskDrives.map((disk) => (
              <tr key={disk.deviceId ?? `${disk.sizeBytes}-${disk.freeBytes}`}>
                <td className="px-3 py-2">{fallback(disk.deviceId)}</td>
                <td className="px-3 py-2">{formatBytes(disk.sizeBytes)}</td>
                <td className="px-3 py-2">{formatBytes(disk.freeBytes)}</td>
                <td className="px-3 py-2">{fallback(disk.fileSystem)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    ) : (
      <p className="mt-3 text-sm text-muted-foreground">{t('inventory.messages.noDisks')}</p>
    ),
  );

  const networkSection = buildSection(
    'network',
    t,
    [],
    data.networkAdapters.length > 0 ? (
      <div className="mt-3 max-h-60 overflow-auto text-sm text-muted-foreground">
        <table className="min-w-full divide-y divide-border">
          <thead className="bg-secondary/40 text-xs uppercase text-muted-foreground">
            <tr>
              <th className="px-3 py-2 text-left">{t('inventory.tables.network.name')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.network.mac')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.network.ips')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.network.speed')}</th>
              <th className="px-3 py-2 text-left">{t('inventory.tables.network.status')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/70">
            {data.networkAdapters.map((adapter) => (
              <tr key={`${adapter.name}-${adapter.macAddress}`}>
                <td className="px-3 py-2">{fallback(adapter.name ?? adapter.description)}</td>
                <td className="px-3 py-2">{fallback(adapter.macAddress)}</td>
                <td className="px-3 py-2">{adapter.ipAddresses.length ? adapter.ipAddresses.join(', ') : '-'}</td>
                <td className="px-3 py-2">{formatSpeed(adapter.speedBitsPerSecond)}</td>
                <td className="px-3 py-2">{fallback(adapter.status)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    ) : (
      <p className="mt-3 text-sm text-muted-foreground">{t('inventory.messages.noAdapters')}</p>
    ),
  );

  const graphicsSection = buildSection(
    'graphics',
    t,
    [
      { key: 'graphicsCard', value: data.graphicsCard },
      { key: 'graphicsCardMemory', value: data.graphicsCardMemory },
      { key: 'currentResolution', value: data.currentResolution },
    ],
    hasMonitorInfo ? (
      <div className="mt-3 space-y-2 text-sm text-muted-foreground">
        {data.monitors.map((monitor, index) => (
          <div key={`${monitor.model ?? 'monitor'}-${index}`} className="rounded-lg border border-border/60 p-3">
            <p className="font-medium text-foreground">{fallback(monitor.model ?? monitor.manufacturer)}</p>
            <p>{t('inventory.labels.serialNumber')}: {fallback(monitor.serialNumber)}</p>
            <p>{t('inventory.labels.currentResolution')}: {fallback(monitor.resolution)}</p>
            <p>{t('inventory.labels.size')}: {fallback(monitor.size)}</p>
          </div>
        ))}
      </div>
    ) : undefined,
  );

  return (
    <div className={containerClass}>
      {summaryCards.length > 0 && (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {summaryCards.map((card) => (
            <div key={card.key} className="rounded-xl border border-border bg-card p-4 shadow-sm">
              <p className="text-xs uppercase text-muted-foreground">{card.label}</p>
              <p className="mt-2 text-lg font-semibold text-foreground">{card.value}</p>
            </div>
          ))}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        {renderSection(systemSection)}
        {renderSection(osSection)}
        {renderSection(processorSection)}
        {renderSection(memorySection)}
        {renderSection(storageSection)}
        {renderSection(networkSection)}
        {renderSection(graphicsSection)}
      </div>

      <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
        <div className="mb-3 flex items-center justify-between">
          <div>
            <h3 className="text-sm font-semibold">{t('inventory.software')}</h3>
            <p className="text-xs text-muted-foreground">
              {t('inventory.softwareSummary', { count: data.software.length })}
            </p>
          </div>
        </div>
        {data.software.length > 0 ? (
          <div className="max-h-80 overflow-auto text-sm text-muted-foreground">
            <table className="min-w-full divide-y divide-border">
              <thead className="bg-secondary/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 text-left">{t('inventory.softwareHeaders.name')}</th>
                  <th className="px-3 py-2 text-left">{t('inventory.softwareHeaders.version')}</th>
                  <th className="px-3 py-2 text-left">{t('inventory.softwareHeaders.publisher')}</th>
                  <th className="px-3 py-2 text-left">{t('inventory.softwareHeaders.installed')}</th>
                  <th className="px-3 py-2 text-left">{t('inventory.softwareHeaders.size')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/70">
                {data.software.map((app) => (
                  <tr key={`${app.id}-${app.name}`}>
                    <td className="px-3 py-2 text-foreground">{app.name}</td>
                    <td className="px-3 py-2">{fallback(app.version)}</td>
                    <td className="px-3 py-2">{fallback(app.publisher)}</td>
                    <td className="px-3 py-2">{formatDate(app.installDate, locale)}</td>
                    <td className="px-3 py-2">{formatBytes(app.sizeInBytes)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{t('inventory.softwareMissing')}</p>
        )}
      </div>

      <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
        <div className="mb-3 flex items-center justify-between">
          <div>
            <h3 className="text-sm font-semibold">{t('inventory.patchesTitle')}</h3>
            <p className="text-xs text-muted-foreground">
              {t('inventory.patchesSummary', { count: data.patches.length })}
            </p>
          </div>
        </div>
        {data.patches.length > 0 ? (
          <div className="max-h-60 overflow-auto text-sm text-muted-foreground">
            <table className="min-w-full divide-y divide-border">
              <thead className="bg-secondary/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <th className="px-3 py-2 text-left">{t('inventory.tables.patches.id')}</th>
                  <th className="px-3 py-2 text-left">{t('inventory.tables.patches.description')}</th>
                  <th className="px-3 py-2 text-left">{t('inventory.tables.patches.installedOn')}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/70">
                {data.patches.map((patch) => (
                  <tr key={`${patch.id}-${patch.hotFixId}`}>
                    <td className="px-3 py-2 text-foreground">{fallback(patch.hotFixId)}</td>
                    <td className="px-3 py-2">{fallback(patch.description)}</td>
                    <td className="px-3 py-2">{formatDate(patch.installedOn, locale)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{t('inventory.messages.noPatches')}</p>
        )}
      </div>
    </div>
  );
}

export default InventoryOverview;

type SectionItem = {
  key: string;
  value?: string | number | null;
  formatter?: (value: string | number | null | undefined) => string;
};

type SectionDescriptor = {
  key: string;
  title: string;
  items: Array<{ key: string; label: string; value: string }>;
  extra?: ReactNode;
};

function buildSection(
  titleKey: string,
  t: ReturnType<typeof useTranslation>['t'],
  items: SectionItem[],
  extra?: ReactNode,
): SectionDescriptor | null {
  const mapped = items.map((item) => ({
    key: item.key,
    label: t(`inventory.labels.${item.key}`),
    value: item.formatter ? item.formatter(item.value) : fallback(item.value),
  }));

  const hasContent = mapped.some((item) => item.value !== '-');
  if (!hasContent && !extra) {
    return null;
  }

  return {
    key: titleKey,
    title: t(`inventory.sections.${titleKey}`),
    items: mapped,
    extra,
  };
}

function renderSection(section: SectionDescriptor | null): ReactNode {
  if (!section || (section.items.length === 0 && !section.extra)) {
    return null;
  }

  return (
    <div key={section.key} className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <h3 className="mb-3 text-sm font-semibold text-foreground">{section.title}</h3>
      {section.items.length > 0 && (
        <ul className="space-y-2 text-sm text-muted-foreground">
          {section.items.map((item) => (
            <li key={item.key} className="flex justify-between gap-4">
              <span className="font-medium text-foreground/80">{item.label}</span>
              <span className="text-right">{item.value}</span>
            </li>
          ))}
        </ul>
      )}
      {section.extra}
    </div>
  );
}

function formatBytes(value?: number): string {
  if (!value || Number.isNaN(value)) return '-';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let result = value;
  let unitIndex = 0;
  while (result >= 1024 && unitIndex < units.length - 1) {
    result /= 1024;
    unitIndex += 1;
  }
  return `${result.toFixed(unitIndex === 0 ? 0 : 2)} ${units[unitIndex]}`;
}

function formatSpeed(value?: number): string {
  if (!value || Number.isNaN(value)) return '-';
  const units = [
    { label: 'Gbps', divider: 1_000_000_000 },
    { label: 'Mbps', divider: 1_000_000 },
    { label: 'Kbps', divider: 1_000 },
  ];

  for (const unit of units) {
    if (value >= unit.divider) {
      return `${(value / unit.divider).toFixed(2)} ${unit.label}`;
    }
  }

  return `${value} bps`;
}

function formatDate(value?: string | number | null, locale?: string): string {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return typeof value === 'string' ? value : '-';
  }
  return parsed.toLocaleString(locale ?? 'en-GB');
}

function fallback(value?: string | number | null): string {
  if (value === undefined || value === null) return '-';
  const text = String(value).trim();
  return text.length === 0 ? '-' : text;
}

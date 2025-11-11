import React, { useState, useEffect, useMemo } from 'react';
import type { RefObject } from 'react';
import {
  RefreshCw,
  Monitor,
  Terminal as TerminalIcon,
  Folder,
  File,
  ArrowLeft,
  Server,
  Cpu,
  HardDrive,
  Activity,
  ShieldAlert,
  ClipboardList,
  Wifi,
  Archive,
  Send,
  Play,
  Square,
  ExternalLink,
  Camera,
  Video,
  Settings,
  Maximize,
  Minimize,
  AlertCircle,
} from 'lucide-react';
import { useTranslation } from '../../hooks/useTranslation';
import { useAuthStore } from '../../stores/authStore';
import InventoryOverview from '../../components/inventory/InventoryOverview';
import { inventoryService } from '../../services/inventory.service';
import { remoteOpsService } from '../../services/remoteOps.service';
import { eventLogsService } from '../../services/eventLogs.service';
import { DEFAULT_REMOTE_RESOLUTION } from '../../constants/remoteDesktop';
import type { RemoteShortcutId } from '../../constants/remoteDesktop';
import type { Device } from '../../types/device.types';
import { toErrorMessage } from '../../utils/error';
import type {
  DiagnosticsState,
  InventoryState,
  SecurityState,
  EventLogState,
  PerformanceState,
  RemoteControlState,
  RemoteClipboardState,
  TerminalState,
  MessagingFormState,
  MaintenanceFormState,
  ScriptsState,
  ScriptFormState,
  QualityLevel,
  SoftwareState,
  PatchState,
  PatchFormState,
} from './types';

const formatUptime = (seconds: number): string => {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);

  if (days > 0) {
    return `${days}g ${hours}s ${minutes}d`;
  }
  if (hours > 0) {
    return `${hours}s ${minutes}d`;
  }
  return `${minutes}d`;
};
export function OverviewTab({
  diagnostics,
  onRefresh,
  device,
  performance,
  onRefreshPerformance,
}: {
  diagnostics: DiagnosticsState;
  onRefresh: () => void;
  device: Device;
  performance: PerformanceState;
  onRefreshPerformance: () => void;
}) {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  return (
    <div className="space-y-6">
      <section className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold">{t('deviceDetail.overview.diagnosticsTitle')}</h2>
            <p className="text-sm text-muted-foreground">
              {t('deviceDetail.overview.diagnosticsDescription')}
            </p>
          </div>
          <button
            onClick={onRefresh}
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
          >
            <RefreshCw className="h-4 w-4" />
            {t('deviceDetail.overview.diagnosticsRefresh')}
          </button>
        </div>
        {diagnostics.loading ? (
          <div className="flex h-40 items-center justify-center text-muted-foreground">
            <RefreshCw className="mr-3 h-4 w-4 animate-spin" />
            {t('deviceDetail.overview.diagnosticsLoading')}
          </div>
        ) : diagnostics.error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {diagnostics.error}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <DiagnosticCard title={t('deviceDetail.overview.cards.status')} icon={ShieldAlert} data={diagnostics.status} />
            <DiagnosticCard title={t('deviceDetail.overview.cards.agent')} icon={ClipboardList} data={diagnostics.agentInfo} />
            <DiagnosticCard
              title={t('deviceDetail.overview.cards.connection')}
              icon={Monitor}
              data={diagnostics.connectionDetails}
            />
            <DiagnosticCard title={t('deviceDetail.overview.cards.ping')} icon={Wifi} data={diagnostics.pingResult} />
          </div>
        )}
      </section>

      <section className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold">{t('deviceDetail.overview.performanceTitle')}</h2>
            <p className="text-sm text-muted-foreground">{t('deviceDetail.overview.performanceDescription')}</p>
          </div>
          <button
            onClick={onRefreshPerformance}
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
          >
            <RefreshCw className="h-4 w-4" />
            {t('deviceDetail.overview.performanceRefresh')}
          </button>
        </div>
        {performance.loading ? (
          <div className="flex h-32 items-center justify-center text-muted-foreground">
            <RefreshCw className="mr-3 h-4 w-4 animate-spin" />
            {t('deviceDetail.overview.performanceLoading')}
          </div>
        ) : performance.error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
            {performance.error}
          </div>
        ) : performance.metrics ? (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <MetricCard title={t('deviceDetail.performance.labels.cpu')} value={`${performance.metrics.cpuUsage.toFixed(1)} %`} icon={Cpu} />
            <MetricCard title={t('deviceDetail.performance.labels.memory')} value={`${performance.metrics.memoryUsage.toFixed(1)} %`} icon={HardDrive} />
            <MetricCard title={t('deviceDetail.performance.labels.disk')} value={`${performance.metrics.diskUsage.toFixed(1)} %`} icon={Archive} />
            <MetricCard
              title={t('deviceDetail.performance.labels.uptime')}
              value={formatUptime(performance.metrics.uptimeSeconds)}
              icon={Activity}
            />
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">{t('deviceDetail.overview.metricsUnavailable')}</p>
        )}
      </section>

      <section className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <h2 className="mb-4 text-lg font-semibold">{t('deviceDetail.overview.metaTitle')}</h2>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <Property label={t('deviceDetail.overview.fields.hostname')} value={device.hostname} />
          <Property label={t('deviceDetail.overview.fields.ipAddress')} value={device.ipAddress ?? '-'} />
          <Property label={t('deviceDetail.overview.fields.macAddress')} value={device.macAddress ?? '-'} />
          <Property label={t('deviceDetail.overview.fields.domain')} value={device.domain ?? '-'} />
          <Property label={t('deviceDetail.overview.fields.os')} value={device.osVersion ?? '-'} />
          <Property label={t('deviceDetail.overview.fields.agentVersion')} value={device.agentVersion ?? '-'} />
          <Property label={t('deviceDetail.overview.fields.registeredAt')} value={new Date(device.registeredAt).toLocaleString(locale)} />
          <Property
            label={t('deviceDetail.overview.fields.lastSeen')}
            value={device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleString(locale) : '-'}
          />
        </div>
      </section>
    </div>
  );
}
export function DiagnosticCard({
  title,
  icon: Icon,
  data,
}: {
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  data?: Record<string, unknown>;
}) {
  const { t } = useTranslation();

  return (
    <div className="rounded-xl border border-border bg-background/60 p-4">
      <div className="mb-3 flex items-center gap-2">
        <Icon className="h-4 w-4 text-primary" />
        <h3 className="text-sm font-semibold">{title}</h3>
      </div>
      <div className="space-y-1 text-xs text-muted-foreground">
        {data ? (
          Object.entries(data).map(([key, value]) => (
            <div key={key} className="flex items-center justify-between gap-4">
              <span className="font-medium text-foreground/80">{formatKey(key)}</span>
              <span className="truncate">{String(value)}</span>
            </div>
          ))
        ) : (
          <p>{t('common.noData')}</p>
        )}
      </div>
    </div>
  );
}

export function MetricCard({
  title,
  value,
  icon: Icon,
}: {
  title: string;
  value: string;
  icon: React.ComponentType<{ className?: string }>;
}) {
  return (
    <div className="rounded-xl border border-border bg-background/60 p-4">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs uppercase text-muted-foreground">{title}</p>
          <p className="mt-1 text-lg font-semibold">{value}</p>
        </div>
        <Icon className="h-6 w-6 text-primary" />
      </div>
    </div>
  );
}

export function Property({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-border bg-background/40 p-3">
      <p className="text-xs uppercase text-muted-foreground">{label}</p>
      <p className="text-sm font-medium text-foreground">{value}</p>
    </div>
  );
}
export function InventoryTab({ state, onRefresh }: { state: InventoryState; onRefresh: () => void }) {
  const { t, language } = useTranslation();
  const { user } = useAuthStore();
  const [refreshing, setRefreshing] = React.useState(false);
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const handleRefresh = async () => {
    if (!state.data?.deviceId || !user?.id || refreshing) return;

    setRefreshing(true);
    try {
      await inventoryService.refreshInventory(state.data.deviceId, user.id);
      setTimeout(() => {
        onRefresh();
        setRefreshing(false);
      }, 2000);
    } catch (error) {
      console.error('Refresh failed:', error);
      setRefreshing(false);
    }
  };

  const resolvedError = state.error
    ? state.error === 'Inventory not collected yet. Please refresh.'
      ? t('deviceDetail.inventory.notCollected')
      : state.error
    : null;

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.inventory.title')}</h2>
          <p className="text-sm text-muted-foreground">
            {state.data?.updatedAt
              ? `${t('deviceDetail.inventory.lastUpdated')}: ${new Date(state.data.updatedAt).toLocaleString(locale)}`
              : t('deviceDetail.inventory.notCollected')}
          </p>
        </div>
        <button
          onClick={handleRefresh}
          disabled={!state.data?.deviceId || !user?.id || state.loading || refreshing}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <RefreshCw className={'h-4 w-4 ' + ((state.loading || refreshing) ? 'animate-spin' : '')} />
          {t('deviceDetail.inventory.refresh')}
        </button>
      </div>

      {state.loading ? (
        <div className="flex h-40 items-center justify-center gap-3 text-muted-foreground">
          <RefreshCw className="h-4 w-4 animate-spin" />
          {t('deviceDetail.inventory.loading')}
        </div>
      ) : resolvedError ? (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
          {resolvedError}
        </div>
      ) : state.data ? (
        <InventoryOverview data={state.data} locale={locale} />
      ) : (
        <div className="rounded-lg border border-border bg-card p-8 text-center text-muted-foreground">
          {t('deviceDetail.inventory.notCollected')}
        </div>
      )}
    </section>
  );
}
export function SecurityTab({ state, onRefresh }: { state: SecurityState; onRefresh: () => void }) {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const getStatusColor = (status: string): string => {
    const statusLower = status.toLowerCase();
    if (
      statusLower.includes('enabled') ||
      statusLower.includes('true') ||
      statusLower.includes('on') ||
      statusLower.includes('running') ||
      statusLower.includes('healthy')
    ) {
      return 'text-emerald-500';
    }
    if (statusLower.includes('disabled') || statusLower.includes('false') || statusLower.includes('off')) {
      return 'text-destructive';
    }
    if (statusLower.includes('warning') || statusLower.includes('partial')) {
      return 'text-yellow-500';
    }
    return 'text-muted-foreground';
  };

  const getStatusBadge = (status: string): string => {
    const statusLower = status.toLowerCase();
    if (
      statusLower.includes('enabled') ||
      statusLower.includes('true') ||
      statusLower.includes('on') ||
      statusLower.includes('running') ||
      statusLower.includes('healthy')
    ) {
      return 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20';
    }
    if (statusLower.includes('disabled') || statusLower.includes('false') || statusLower.includes('off')) {
      return 'bg-destructive/10 text-destructive border-destructive/20';
    }
    if (statusLower.includes('warning') || statusLower.includes('partial')) {
      return 'bg-yellow-500/10 text-yellow-500 border-yellow-500/20';
    }
    return 'bg-muted/10 text-muted-foreground border-muted/20';
  };

  const formatBooleanValue = (value: boolean | undefined): string => {
    if (value === undefined) {
      return t('deviceDetail.security.states.unknown', undefined, 'Unknown');
    }
    return value
      ? t('deviceDetail.security.states.enabled', undefined, 'Enabled')
      : t('deviceDetail.security.states.disabled', undefined, 'Disabled');
  };

  const renderFirewallCard = (key: string, firewall: Record<string, any>, title: string) => {
    const products = Array.isArray(firewall.products) ? firewall.products : [];
    const profiles = Array.isArray(firewall.profiles) ? firewall.profiles : [];

    return (
      <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-4">
        <div className="flex items-center justify-between">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          {firewall.error && <span className="text-xs text-destructive">{String(firewall.error)}</span>}
        </div>
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase text-muted-foreground">
            {t('deviceDetail.security.firewall.productsTitle', undefined, 'Registered products')}
          </p>
          {products.length ? (
            <div className="space-y-2">
              {products.map((product: any, idx: number) => (
                <div key={product.displayName ?? idx} className="flex items-center justify-between rounded-md bg-muted/30 px-3 py-2">
                  <span className="text-sm font-medium text-foreground">
                    {product.displayName ?? t('deviceDetail.security.firewall.unknownProduct', undefined, 'Unknown product')}
                  </span>
                  <span
                    className={`inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium ${getStatusBadge(String(product.enabled))}`}
                  >
                    <span className={`h-1.5 w-1.5 rounded-full ${product.enabled ? 'bg-emerald-500' : 'bg-destructive'}`} />
                    {formatBooleanValue(product.enabled)}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {t('deviceDetail.security.firewall.noProducts', undefined, 'No firewall products reported.')}
            </p>
          )}
        </div>
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase text-muted-foreground">
            {t('deviceDetail.security.firewall.profilesTitle', undefined, 'Firewall profiles')}
          </p>
          {profiles.length ? (
            <div className="space-y-2">
              {profiles.map((profile: any, idx: number) => (
                <div key={`${profile.profile ?? idx}`} className="flex items-center justify-between rounded-md bg-muted/30 px-3 py-2">
                  <span className="text-sm font-medium text-foreground">
                    {profile.profile ?? t('deviceDetail.security.firewall.unknownProfile', undefined, 'Unknown profile')}
                  </span>
                  <span
                    className={`inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium ${getStatusBadge(String(profile.enabled))}`}
                  >
                    <span className={`h-1.5 w-1.5 rounded-full ${profile.enabled ? 'bg-emerald-500' : 'bg-destructive'}`} />
                    {formatBooleanValue(profile.enabled)}
                  </span>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {t('deviceDetail.security.firewall.noProfiles', undefined, 'No firewall profiles reported.')}
            </p>
          )}
        </div>
      </div>
    );
  };

  const renderSecurityCenterCard = (key: string, center: Record<string, any>, title: string) => {
    const componentLabels: Record<string, string> = {
      antivirusHealthy: t('deviceDetail.security.securityCenter.components.antivirus', undefined, 'Antivirus'),
      firewallHealthy: t('deviceDetail.security.securityCenter.components.firewall', undefined, 'Firewall'),
      antiSpywareHealthy: t('deviceDetail.security.securityCenter.components.antiSpyware', undefined, 'Anti-spyware'),
    };
    const healthEntries = Object.entries(center.componentHealth ?? {});

    return (
      <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-3">
        <p className="text-sm font-semibold text-foreground">{title}</p>
        {healthEntries.length ? (
          <div className="space-y-2">
            {healthEntries.map(([componentKey, healthy]) => (
              <div key={componentKey} className="flex items-center justify-between rounded-md bg-muted/30 px-3 py-2">
                <span className="text-sm font-medium text-foreground">{componentLabels[componentKey] ?? formatKey(componentKey)}</span>
                <span className={`text-sm font-semibold ${healthy ? 'text-emerald-500' : 'text-destructive'}`}>
                  {healthy
                    ? t('deviceDetail.security.securityCenter.healthy', undefined, 'Healthy')
                    : t('deviceDetail.security.securityCenter.unhealthy', undefined, 'Needs attention')}
                </span>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            {t('deviceDetail.security.securityCenter.noData', undefined, 'No security center signals reported.')}
          </p>
        )}
        {center.error && (
          <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">{String(center.error)}</p>
        )}
      </div>
    );
  };

  const renderEncryptionCard = (key: string, encryption: Record<string, any>, title: string) => {
    const volumes = Array.isArray(encryption.bitlockerVolumes) ? encryption.bitlockerVolumes : [];
    return (
      <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-3">
        <p className="text-sm font-semibold text-foreground">{title}</p>
        {encryption.error && (
          <p className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">{String(encryption.error)}</p>
        )}
        {volumes.length ? (
          <div className="space-y-2">
            {volumes.map((volume: any, idx: number) => (
              <div key={`${volume.driveLetter ?? idx}`} className="space-y-1 rounded-md bg-muted/30 p-3 text-sm">
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">{t('deviceDetail.security.encryption.driveLabel', undefined, 'Drive')}</span>
                  <span className="font-medium text-foreground">{volume.driveLetter ?? '-'}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">
                    {t('deviceDetail.security.encryption.protectionLabel', undefined, 'Protection')}
                  </span>
                  <span className="font-medium text-foreground">{volume.protectionStatus ?? '-'}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">
                    {t('deviceDetail.security.encryption.conversionLabel', undefined, 'Conversion')}
                  </span>
                  <span className="font-medium text-foreground">{volume.conversionStatus ?? '-'}</span>
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-muted-foreground">{t('deviceDetail.security.encryption.methodLabel', undefined, 'Method')}</span>
                  <span className="font-medium text-foreground">{volume.encryptionMethod ?? '-'}</span>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">
            {t('deviceDetail.security.encryption.noVolumes', undefined, 'No BitLocker volumes reported.')}
          </p>
        )}
      </div>
    );
  };

  const renderSecurityCard = (key: string, value: unknown): React.ReactNode => {
    const cardTitles: Record<string, string> = {
      antivirus: t('deviceDetail.security.cards.antivirus', undefined, 'Antivirus'),
      firewall: t('deviceDetail.security.cards.firewall', undefined, 'Firewall'),
      defender: t('deviceDetail.security.cards.defender', undefined, 'Defender'),
      uac: t('deviceDetail.security.cards.uac', undefined, 'UAC'),
      encryption: t('deviceDetail.security.cards.encryption', undefined, 'Encryption'),
      securityCenter: t('deviceDetail.security.cards.securityCenter', undefined, 'Security Center'),
      lastSecurityScan: t('deviceDetail.security.cards.lastScan', undefined, 'Last Scan'),
      riskScore: t('deviceDetail.security.cards.riskScore', undefined, 'Risk Score'),
      timestamp: t('deviceDetail.security.cards.timestamp', undefined, 'Generated at'),
    };

    const title = cardTitles[key] || formatKey(key);
    const normalizedKey = key.toLowerCase();

    if (normalizedKey === 'firewall' && typeof value === 'object' && value !== null) {
      return renderFirewallCard(key, value as Record<string, any>, title);
    }

    if (normalizedKey === 'securitycenter' && typeof value === 'object' && value !== null) {
      return renderSecurityCenterCard(key, value as Record<string, any>, title);
    }

    if (normalizedKey === 'encryption' && typeof value === 'object' && value !== null) {
      return renderEncryptionCard(key, value as Record<string, any>, title);
    }

    if (normalizedKey === 'timestamp' && typeof value === 'string') {
      const dateValue = new Date(value);
      return (
        <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-2">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          <p className="text-sm text-muted-foreground">
            {dateValue.toString() === 'Invalid Date' ? value : dateValue.toLocaleString(locale)}
          </p>
        </div>
      );
    }

    if (Array.isArray(value)) {
      return (
        <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-3">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          {value.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              {t('deviceDetail.security.states.notInstalled', undefined, 'Not installed')}
            </p>
          ) : (
            <div className="space-y-2">
              {value.map((item: any, idx: number) => (
                <div key={idx} className="rounded-md bg-muted/30 p-3 space-y-1">
                  {item.displayName && <p className="text-sm font-medium text-foreground">{item.displayName}</p>}
                  {item.enabled !== undefined && (
                    <span
                      className={`inline-flex items-center gap-1.5 rounded-full border px-2 py-0.5 text-xs font-medium ${getStatusBadge(String(item.enabled))}`}
                    >
                      <span className={`h-1.5 w-1.5 rounded-full ${item.enabled ? 'bg-emerald-500' : 'bg-destructive'}`} />
                      {formatBooleanValue(item.enabled)}
                    </span>
                  )}
                  {item.upToDate !== undefined && (
                    <p className="text-xs text-muted-foreground">
                      {t('deviceDetail.security.upToDate', undefined, 'Up to date')}:{' '}
                      {item.upToDate
                        ? t('common.yes', undefined, 'Yes')
                        : t('common.no', undefined, 'No')}
                    </p>
                  )}
                  {item.productState !== undefined && (
                    <p className="text-xs text-muted-foreground">State: {item.productState}</p>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      );
    }

    if (typeof value === 'object' && value !== null) {
      const obj = value as Record<string, any>;
      return (
        <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-3">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          <div className="space-y-2">
            {Object.entries(obj).map(([subKey, subValue]) => {
              const label = formatKey(subKey);
              if (typeof subValue === 'boolean') {
                return (
                  <div key={subKey} className="flex items-center justify-between text-sm">
                    <span className="text-muted-foreground">{label}</span>
                    <span className={`font-medium ${getStatusColor(String(subValue))}`}>{formatBooleanValue(subValue)}</span>
                  </div>
                );
              }
              if (typeof subValue === 'object' && subValue !== null) {
                return (
                  <div key={subKey} className="space-y-1 text-sm">
                    <span className="text-muted-foreground">{label}</span>
                    <pre className="whitespace-pre-wrap rounded-md bg-muted/30 p-2 text-xs text-foreground">
                      {JSON.stringify(subValue, null, 2)}
                    </pre>
                  </div>
                );
              }
              return (
                <div key={subKey} className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">{label}</span>
                  <span className="font-medium text-foreground">{String(subValue ?? '-')}</span>
                </div>
              );
            })}
          </div>
        </div>
      );
    }

    const displayValue =
      value === null || value === undefined
        ? t('deviceDetail.security.states.unknown', undefined, 'Unknown')
        : String(value);

    if (key === 'riskScore' && typeof value === 'number') {
      const riskColor = value > 70 ? 'text-destructive' : value > 40 ? 'text-yellow-500' : 'text-emerald-500';
      const riskBg = value > 70 ? 'bg-destructive/10' : value > 40 ? 'bg-yellow-500/10' : 'bg-emerald-500/10';
      return (
        <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-2">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          <div className={`${riskBg} rounded-md p-3 text-center`}>
            <p className={`text-3xl font-bold ${riskColor}`}>{value}</p>
            <p className="mt-1 text-xs text-muted-foreground">
              {value > 70
                ? t('deviceDetail.security.risk.high', undefined, 'High Risk')
                : value > 40
                  ? t('deviceDetail.security.risk.medium', undefined, 'Medium Risk')
                  : t('deviceDetail.security.risk.low', undefined, 'Low Risk')}
            </p>
          </div>
        </div>
      );
    }

    if (key.toLowerCase().includes('scan') || key.toLowerCase().includes('date') || key.toLowerCase().includes('time')) {
      return (
        <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-2">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          <p className="text-sm text-muted-foreground">{displayValue}</p>
        </div>
      );
    }

    return (
      <div key={key} className="rounded-lg border border-border bg-card p-4 space-y-2">
        <p className="text-sm font-semibold text-foreground">{title}</p>
        <span className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-sm font-medium ${getStatusBadge(displayValue)}`}>
          <span
            className={`h-2 w-2 rounded-full ${
              displayValue.toLowerCase().includes('enabled') || displayValue.toLowerCase().includes('true') ? 'bg-emerald-500' : 'bg-destructive'
            }`}
          />
          {displayValue}
        </span>
      </div>
    );
  };

  const renderSnapshotCards = (): React.ReactNode => {
    if (!state.snapshot) {
      return null;
    }
    const snapshot = state.snapshot as Record<string, unknown>;
    const priority = ['securityCenter', 'antivirus', 'firewall', 'defender', 'uac', 'encryption', 'lastSecurityScan', 'riskScore', 'timestamp'];
    const orderedKeys = [...new Set([...priority, ...Object.keys(snapshot)])];

    return orderedKeys
      .filter((key) => snapshot[key] !== undefined && snapshot[key] !== null)
      .map((key) => renderSecurityCard(key, snapshot[key]));
  };

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.security.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.security.description')}</p>
        </div>
        <button
          onClick={onRefresh}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('deviceDetail.security.refresh')}
        </button>
      </div>

      {state.loading ? (
        <div className="flex h-40 items-center justify-center text-muted-foreground">
          <RefreshCw className="mr-3 h-4 w-4 animate-spin" />
          {t('deviceDetail.security.loading')}
        </div>
      ) : state.error ? (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">{state.error}</div>
      ) : state.snapshot ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{renderSnapshotCards()}</div>
      ) : (
        <p className="text-sm text-muted-foreground">{t('deviceDetail.security.empty')}</p>
      )}
    </section>
  );
}

export function EventLogsTab({
  state,
  onRefresh,
  deviceId,
}: {
  state: EventLogState;
  onRefresh: () => void;
  deviceId: string;
}) {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';
  const [monitorForm, setMonitorForm] = useState({ logName: 'Application', monitorId: '' });
  const [clearLogName, setClearLogName] = useState('Application');
  const [actionState, setActionState] = useState<{ busy: boolean; message?: string; error?: string }>({ busy: false });

  const runEventAction = async (action: () => Promise<unknown>) => {
    setActionState({ busy: true, message: undefined, error: undefined });
    try {
      await action();
      setActionState({ busy: false, message: t('deviceDetail.eventLogs.actionSuccess') });
    } catch (error) {
      setActionState({
        busy: false,
        error: toErrorMessage(error, t('deviceDetail.eventLogs.actionError')),
      });
    }
  };

  const handleStartMonitor = async () => {
    if (!monitorForm.logName.trim()) {
      setActionState({ busy: false, error: t('deviceDetail.eventLogs.logNameRequired') });
      return;
    }
    const monitorId = monitorForm.monitorId || `${monitorForm.logName}-${Date.now()}`;
    await runEventAction(() =>
      eventLogsService.startMonitor(deviceId, { logName: monitorForm.logName, monitorId }),
    );
    setMonitorForm((prev) => ({ ...prev, monitorId }));
  };

  const handleStopMonitor = async () => {
    if (!monitorForm.monitorId) {
      setActionState({ busy: false, error: t('deviceDetail.eventLogs.monitorIdRequired') });
      return;
    }
    await runEventAction(() => eventLogsService.stopMonitor(deviceId, { monitorId: monitorForm.monitorId }));
  };

  const handleClearLog = async () => {
    if (!clearLogName.trim()) {
      setActionState({ busy: false, error: t('deviceDetail.eventLogs.logNameRequired') });
      return;
    }
    await runEventAction(() => eventLogsService.clearLog(deviceId, { logName: clearLogName }));
  };

  const formatTimestamp = (timestamp: string | undefined | null): string => {
    if (!timestamp) return '-';
    try {
      const date = new Date(timestamp);
      if (isNaN(date.getTime())) return '-';
      return date.toLocaleString(locale);
    } catch {
      return '-';
    }
  };

  // Ensure items is always an array to prevent "items.map is not a function" error
  const items = Array.isArray(state.items) ? state.items : [];

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.eventLogs.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.eventLogs.description')}</p>
        </div>
        <button
          onClick={onRefresh}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('deviceDetail.eventLogs.refresh')}
        </button>
      </div>
      <div className="overflow-hidden rounded-xl border border-border bg-card">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-border text-sm">
            <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">{t('deviceDetail.eventLogs.columns.timestamp')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.eventLogs.columns.level')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.eventLogs.columns.source')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.eventLogs.columns.message')}</th>
              </tr>
            </thead>
            <tbody>
              {state.loading ? (
                <tr>
                  <td colSpan={4} className="px-4 py-6 text-center text-muted-foreground">
                    <div className="flex items-center justify-center gap-2">
                      <RefreshCw className="h-4 w-4 animate-spin" />
                      {t('deviceDetail.eventLogs.loading')}
                    </div>
                  </td>
                </tr>
              ) : state.error ? (
                <tr>
                  <td colSpan={4} className="px-4 py-6">
                    <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
                      {state.error}
                    </div>
                  </td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={4} className="px-4 py-6 text-center text-muted-foreground">
                    {t('deviceDetail.eventLogs.empty')}
                  </td>
                </tr>
              ) : (
                items.map((entry, index) => (
                  <tr key={`${entry.eventId}-${index}`} className="border-t border-border/70 hover:bg-muted/30 transition">
                    <td className="px-4 py-3 text-xs text-muted-foreground whitespace-nowrap">
                      {formatTimestamp(entry.loggedAt)}
                    </td>
                    <td className="px-4 py-3 text-xs font-medium text-foreground">
                      <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                        entry.level?.toLowerCase() === 'error' || entry.level?.toLowerCase() === 'critical'
                          ? 'bg-destructive/10 text-destructive'
                          : entry.level?.toLowerCase() === 'warning'
                          ? 'bg-yellow-500/10 text-yellow-600'
                          : 'bg-muted text-foreground'
                      }`}>
                        {entry.level ?? '-'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">{entry.source ?? '-'}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground max-w-md truncate" title={entry.message || '-'}>
                      {entry.message ?? '-'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
      <div className="rounded-xl border border-border bg-card p-5">
        <div className="mb-4">
          <h3 className="text-base font-semibold">{t('deviceDetail.eventLogs.monitorTitle')}</h3>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.eventLogs.monitorDescription')}</p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-3">
            <label className="text-xs font-medium uppercase text-muted-foreground">
              {t('deviceDetail.eventLogs.logNameLabel')}
            </label>
            <input
              value={monitorForm.logName}
              onChange={(event) => setMonitorForm((prev) => ({ ...prev, logName: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder="Application"
            />
            <label className="text-xs font-medium uppercase text-muted-foreground">
              {t('deviceDetail.eventLogs.monitorIdLabel')}
            </label>
            <input
              value={monitorForm.monitorId}
              onChange={(event) => setMonitorForm((prev) => ({ ...prev, monitorId: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder={t('deviceDetail.eventLogs.monitorIdPlaceholder')}
            />
            <div className="flex gap-2">
              <button
                onClick={handleStartMonitor}
                disabled={actionState.busy}
                className="inline-flex flex-1 items-center justify-center rounded-lg bg-primary px-3 py-2 text-xs font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
              >
                {t('deviceDetail.eventLogs.startMonitor')}
              </button>
              <button
                onClick={handleStopMonitor}
                disabled={actionState.busy}
                className="inline-flex flex-1 items-center justify-center rounded-lg border border-border px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
              >
                {t('deviceDetail.eventLogs.stopMonitor')}
              </button>
            </div>
          </div>
          <div className="space-y-3">
            <label className="text-xs font-medium uppercase text-muted-foreground">
              {t('deviceDetail.eventLogs.clearLogLabel')}
            </label>
            <input
              value={clearLogName}
              onChange={(event) => setClearLogName(event.target.value)}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder={t('deviceDetail.eventLogs.clearLogPlaceholder')}
            />
            <button
              onClick={handleClearLog}
              disabled={actionState.busy}
              className="inline-flex w-full items-center justify-center rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/60 disabled:opacity-60"
            >
              {t('deviceDetail.eventLogs.clearLog')}
            </button>
          </div>
        </div>
        {actionState.message && (
          <div className="mt-4 rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-3 text-sm text-emerald-600">
            {actionState.message}
          </div>
        )}
        {actionState.error && (
          <div className="mt-4 rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
            {actionState.error}
          </div>
        )}
      </div>
    </section>
  );
}
export function FilesTab({ deviceId }: { deviceId: string }) {
  const { t } = useTranslation();
  const [currentPath, setCurrentPath] = useState('');
  const [entries, setEntries] = useState<Array<{ name: string; type: 'file' | 'directory'; size?: number }>>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [zipForm, setZipForm] = useState({ source: '', destination: '' });
  const [unzipForm, setUnzipForm] = useState({ source: '', destination: '' });
  const [urlToOpen, setUrlToOpen] = useState('');
  const [wolMac, setWolMac] = useState('');
  const [actionState, setActionState] = useState<{ busy: boolean; message?: string; error?: string }>({ busy: false });

  const load = async (path: string) => {
    setLoading(true);
    setError(null);
    try {
      const response = await remoteOpsService.listDirectory(deviceId, path);
      if (response.success && Array.isArray(response.data)) {
        setEntries(
          response.data.map((item) => ({
            name: item.name,
            type: item.type === 'directory' ? 'directory' : 'file',
            size: item.size,
          })),
        );
      } else {
        setEntries([]);
        setError(response.error ?? t('files.error'));
      }
    } catch (err) {
      setEntries([]);
      setError(toErrorMessage(err, t('files.error')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (deviceId) {
      load(currentPath);
    }
  }, [deviceId, currentPath]);

  const goBack = () => {
    if (!currentPath) return;
    const parts = currentPath.split('\\').filter(Boolean);
    parts.pop();
    setCurrentPath(parts.join('\\'));
  };

  const navigateToEntry = (entry: { name: string; type: "file" | "directory" }) => {
    if (entry.type !== 'directory') return;
    if (!currentPath) {
      setCurrentPath(entry.name);
    } else {
      const normalized = currentPath.replace(/\\$/, '');
      setCurrentPath(`${normalized}\\${entry.name}`);
    }
  };
  const runAction = async (action: () => Promise<unknown>) => {
    setActionState({ busy: true });
    try {
      await action();
      setActionState({ busy: false, message: t('deviceDetail.files.actionSuccess') });
    } catch (err) {
      setActionState({ busy: false, error: toErrorMessage(err, t('deviceDetail.files.actionError')) });
    }
  };

  const handleZip = async () => {
    if (!zipForm.source || !zipForm.destination) {
      setActionState({ busy: false, error: t('deviceDetail.files.validationPaths') });
      return;
    }
    await runAction(() => remoteOpsService.zip(deviceId, zipForm.source, zipForm.destination));
  };

  const handleUnzip = async () => {
    if (!unzipForm.source || !unzipForm.destination) {
      setActionState({ busy: false, error: t('deviceDetail.files.validationPaths') });
      return;
    }
    await runAction(() => remoteOpsService.unzip(deviceId, unzipForm.source, unzipForm.destination));
  };

  const handleOpenUrl = async () => {
    if (!urlToOpen) {
      setActionState({ busy: false, error: t('deviceDetail.files.validationUrl') });
      return;
    }
    await runAction(() => remoteOpsService.openUrl(deviceId, urlToOpen));
  };

  const handleWakeOnLan = async () => {
    if (!wolMac) {
      setActionState({ busy: false, error: t('deviceDetail.files.validationMac') });
      return;
    }
    await runAction(() => remoteOpsService.wakeOnLan(deviceId, wolMac));
  };

  return (
    <div className="rounded-xl border border-border bg-card p-6">
      <div className="mb-4">
        <h3 className="text-lg font-semibold">{t('files.title')}</h3>
        <p className="text-sm text-muted-foreground">{t('deviceDetail.files.description')}</p>
      </div>
      
      <div className="mb-4 flex gap-2">
        {currentPath && (
          <button
            onClick={goBack}
            className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm hover:bg-secondary/60"
            title={t('files.goBack')}
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
        )}
        <input
          value={currentPath || t('files.drives')}
          onChange={(event) => setCurrentPath(event.target.value)}
          className="flex-1 rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
          placeholder={t('files.pathPlaceholder')}
        />
      </div>

      {loading ? (
        <div className="flex h-40 items-center justify-center gap-3 text-muted-foreground">
          <RefreshCw className="h-4 w-4 animate-spin" />
          {t('files.loading')}
        </div>
      ) : error ? (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      ) : (
        <ul className="max-h-96 space-y-2 overflow-y-auto text-sm">
          {entries.length === 0 ? (
            <li className="py-8 text-center text-muted-foreground">{t('deviceDetail.files.empty')}</li>
          ) : (
            entries.map((entry) => (
              <li key={entry.name} className="flex items-center justify-between rounded-lg border border-border/60 bg-background/40 px-4 py-3">
                <div className="flex items-center gap-3">
                  {entry.type === 'directory' ? <Folder className="h-4 w-4" /> : <File className="h-4 w-4" />}
                  <button
                    onClick={() => navigateToEntry(entry)}
                    className="text-foreground hover:text-primary"
                  >
                    {entry.name}
                  </button>
                </div>
                <div className="flex items-center gap-2">
                  {entry.type === 'file' && entry.size !== undefined && (
                    <span className="text-xs text-muted-foreground">
                      {(entry.size / 1024).toFixed(1)} KB
                    </span>
                  )}
                </div>
              </li>
            ))
          )}
        </ul>
      )}
      <div className="mt-6 space-y-4 rounded-xl border border-border bg-card p-4">
        <div>
          <h4 className="text-base font-semibold">{t('deviceDetail.files.advancedTitle')}</h4>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.files.advancedDescription')}</p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-3">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.files.zipTitle')}
            </label>
            <input
              value={zipForm.source}
              onChange={(event) => setZipForm((prev) => ({ ...prev, source: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder={t('deviceDetail.files.sourcePlaceholder')}
            />
            <input
              value={zipForm.destination}
              onChange={(event) => setZipForm((prev) => ({ ...prev, destination: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder={t('deviceDetail.files.destinationPlaceholder')}
            />
            <button
              onClick={handleZip}
              disabled={actionState.busy}
              className="inline-flex w-full items-center justify-center rounded-lg bg-primary px-3 py-2 text-xs font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
            >
              {t('deviceDetail.files.zipAction')}
            </button>
          </div>
          <div className="space-y-3">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.files.unzipTitle')}
            </label>
            <input
              value={unzipForm.source}
              onChange={(event) => setUnzipForm((prev) => ({ ...prev, source: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder={t('deviceDetail.files.sourceZipPlaceholder')}
            />
            <input
              value={unzipForm.destination}
              onChange={(event) => setUnzipForm((prev) => ({ ...prev, destination: event.target.value }))}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder={t('deviceDetail.files.destinationPlaceholder')}
            />
            <button
              onClick={handleUnzip}
              disabled={actionState.busy}
              className="inline-flex w-full items-center justify-center rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/60 disabled:opacity-60"
            >
              {t('deviceDetail.files.unzipAction')}
            </button>
          </div>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-3">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.files.openUrlTitle')}
            </label>
            <input
              value={urlToOpen}
              onChange={(event) => setUrlToOpen(event.target.value)}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder="https://example.com"
            />
            <button
              onClick={handleOpenUrl}
              disabled={actionState.busy}
              className="inline-flex w-full items-center justify-center rounded-lg border border-border px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
            >
              {t('deviceDetail.files.openUrlAction')}
            </button>
          </div>
          <div className="space-y-3">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.files.wolTitle')}
            </label>
            <input
              value={wolMac}
              onChange={(event) => setWolMac(event.target.value)}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder="00-11-22-33-44-55"
            />
            <button
              onClick={handleWakeOnLan}
              disabled={actionState.busy}
              className="inline-flex w-full items-center justify-center rounded-lg border border-border px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
            >
              {t('deviceDetail.files.wakeOnLanAction')}
            </button>
          </div>
        </div>
        {actionState.message && (
          <p className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600">
            {actionState.message}
          </p>
        )}
        {actionState.error && (
          <p className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {actionState.error}
          </p>
        )}
      </div>
    </div>
  );
}

export function PerformanceTab({ state, onRefresh, deviceId }: { state: PerformanceState; onRefresh: () => void; deviceId: string }) {
  const { t } = useTranslation();

  return (
    <section className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.performance.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.performance.description')}</p>
        </div>
        <button
          onClick={onRefresh}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('deviceDetail.performance.refresh')}
        </button>
      </div>
      {state.loading ? (
        <div className="flex h-32 items-center justify-center gap-3 text-muted-foreground">
          <RefreshCw className="h-4 w-4 animate-spin" />
          {t('deviceDetail.performance.loading')}
        </div>
      ) : state.metrics ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <MetricCard title={t('deviceDetail.performance.labels.cpu')} value={`${state.metrics.cpuUsage.toFixed(1)} %`} icon={Cpu} />
          <MetricCard title={t('deviceDetail.performance.labels.memory')} value={`${state.metrics.memoryUsage.toFixed(1)} %`} icon={HardDrive} />
          <MetricCard title={t('deviceDetail.performance.labels.disk')} value={`${state.metrics.diskUsage.toFixed(1)} %`} icon={Archive} />
          <MetricCard
            title={t('deviceDetail.performance.labels.uptime')}
            value={formatUptime(state.metrics.uptimeSeconds)}
            icon={Activity}
          />
          <MetricCard title={t('deviceDetail.performance.labels.deviceId')} value={deviceId} icon={Server} />
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t('deviceDetail.performance.metricsUnavailable')}</p>
      )}
      {state.error && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
          {state.error}
        </div>
      )}
    </section>
  );
}

export function RemoteDesktopTab({
  control,
  canvasRef,
  quality,
  fps,
  showSettings,
  isRecording,
  isFullscreen,
  onQualityChange,
  onFpsChange,
  onConnect,
  onDisconnect,
  onToggleSettings,
  onToggleRecording,
  onToggleFullscreen,
  onOpenWorkspace,
  onMouseMove,
  onMouseButton,
  onKeyEvent,
  onCapture,
  clipboard,
  onClipboardChange,
  onClipboardRefresh,
  onClipboardApply,
  onSendShortcut,
  onPowerAction,
}: {
  control: RemoteControlState;
  canvasRef: RefObject<HTMLCanvasElement | null>;
  quality: QualityLevel;
  fps: number;
  showSettings: boolean;
  isRecording: boolean;
  isFullscreen: boolean;
  onQualityChange: (value: QualityLevel) => void;
  onFpsChange: (value: number) => void;
  onConnect: () => void;
  onDisconnect: () => void;
  onToggleSettings: () => void;
  onToggleRecording: () => void;
  onToggleFullscreen: () => void;
  onOpenWorkspace: () => void;
  onMouseMove: (coords: { x: number; y: number }) => void;
  onMouseButton: (button: number, action: 'down' | 'up' | 'click') => void;
  onKeyEvent: (keyCode: number, action: 'down' | 'up') => void;
  onCapture: () => void;
  clipboard: RemoteClipboardState;
  onClipboardChange: (value: string) => void;
  onClipboardRefresh: () => void;
  onClipboardApply: () => void;
  onSendShortcut: (shortcut: RemoteShortcutId) => void;
  onPowerAction: (action: 'restart' | 'shutdown' | 'sleep') => void;
}) {
  const { t } = useTranslation();
  const session = control.session;
  const isConnected = Boolean(session);
  const disabled = control.busy;
  const remoteWidth = session?.width ?? DEFAULT_REMOTE_RESOLUTION.width;
  const remoteHeight = session?.height ?? DEFAULT_REMOTE_RESOLUTION.height;

  const mapPointer = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session) return null;
    const rect = event.currentTarget.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return null;
    const scaleX = session.width / rect.width;
    const scaleY = session.height / rect.height;
    const x = Math.min(session.width - 1, Math.max(0, Math.round((event.clientX - rect.left) * scaleX)));
    const y = Math.min(session.height - 1, Math.max(0, Math.round((event.clientY - rect.top) * scaleY)));
    return { x, y };
  };

  const handleMouseMove = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session) return;
    const coords = mapPointer(event);
    if (coords) {
      onMouseMove(coords);
    }
  };

  const handleMouseDown = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session) return;
    event.preventDefault();
    event.currentTarget.focus();
    onMouseButton(event.button, 'down');
  };

  const handleMouseUp = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session) return;
    event.preventDefault();
    onMouseButton(event.button, 'up');
  };

  const handleMouseClick = (event: React.MouseEvent<HTMLCanvasElement>) => {
    if (!session) return;
    event.preventDefault();
    onMouseButton(event.button, 'click');
  };

  const handleKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (!session) return;
    event.preventDefault();
    onKeyEvent(event.nativeEvent.keyCode, 'down');
  };

  const handleKeyUp = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (!session) return;
    event.preventDefault();
    onKeyEvent(event.nativeEvent.keyCode, 'up');
  };

  return (
    <section className="space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">{t('desktop.title')}</h2>
            <p className="text-sm text-muted-foreground">{t('desktop.description')}</p>
          </div>
        <div className="flex flex-wrap gap-2">
          {isConnected ? (
            <>
              <button
                onClick={onCapture}
                className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                title={t('desktop.capture')}
                disabled={disabled}
              >
                <Camera className="h-4 w-4" />
              </button>
              <button
                onClick={onToggleRecording}
                className={`rounded-lg p-2 transition ${
                  isRecording ? 'bg-destructive text-white' : 'border border-border bg-secondary/40 text-muted-foreground hover:bg-secondary/70'
                }`}
                title={isRecording ? t('desktop.recordingStop') : t('desktop.recordingStart')}
                disabled={disabled || !canvasRef.current}
              >
                {isRecording ? <Square className="h-4 w-4" /> : <Video className="h-4 w-4" />}
              </button>
              <button
                onClick={onToggleFullscreen}
                className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                title={isFullscreen ? t('desktop.fullscreenExit') : t('desktop.fullscreenEnter')}
                disabled={!canvasRef.current}
              >
                {isFullscreen ? <Minimize className="h-4 w-4" /> : <Maximize className="h-4 w-4" />}
              </button>
              <button
                onClick={onToggleSettings}
                className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                title={t('desktop.settings')}
              >
                <Settings className="h-4 w-4" />
              </button>
              <button
                onClick={onDisconnect}
                disabled={disabled}
                className="inline-flex items-center gap-2 rounded-lg bg-destructive px-4 py-2 text-sm font-medium text-white transition hover:bg-destructive/90 disabled:cursor-not-allowed disabled:opacity-70"
              >
                <RefreshCw className="h-4 w-4" />
                {t('desktop.disconnect')}
              </button>
            </>
          ) : (
            <button
              onClick={onConnect}
              disabled={disabled}
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-70"
            >
              <Play className="h-4 w-4" />
              {disabled ? t('desktop.connecting') : t('desktop.connect')}
            </button>
          )}
          <button
            onClick={onOpenWorkspace}
            disabled={!isConnected}
            className="inline-flex items-center gap-2 rounded-lg border border-primary/40 px-4 py-2 text-sm font-medium text-primary transition hover:bg-primary/10 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <ExternalLink className="h-4 w-4" />
            {t('deviceDetail.remoteDesktop.launch')}
          </button>
        </div>
      </div>

      {control.error && (
        <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          <AlertCircle className="h-4 w-4" />
          <span>{control.error}</span>
        </div>
      )}

      {showSettings && isConnected && (
        <div className="grid gap-4 rounded-xl border border-border bg-background/50 p-4 shadow-sm md:grid-cols-3">
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('desktop.quality')}</label>
            <select
              value={quality}
              onChange={(event) => onQualityChange(event.target.value as QualityLevel)}
              className="w-full rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
              disabled={disabled}
            >
              <option value="low">{t('desktop.qualityOptions.low')}</option>
              <option value="medium">{t('desktop.qualityOptions.medium')}</option>
              <option value="high">{t('desktop.qualityOptions.high')}</option>
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('desktop.fps')}</label>
            <select
              value={fps}
              onChange={(event) => onFpsChange(Number(event.target.value))}
              className="w-full rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
              disabled={disabled}
            >
              {[10, 15, 24, 30].map((value) => (
                <option key={value} value={value}>
                  {value} FPS
                </option>
              ))}
            </select>
          </div>
          <div className="self-end text-xs text-muted-foreground">{t('desktop.reconnectHint')}</div>
        </div>
      )}

      <div
        className="rounded-xl border border-border bg-card"
        tabIndex={0}
        onKeyDown={handleKeyDown}
        onKeyUp={handleKeyUp}
      >
        {isConnected ? (
          <canvas
            ref={canvasRef}
            className="h-[600px] w-full bg-black"
            onMouseMove={handleMouseMove}
            onMouseDown={handleMouseDown}
            onMouseUp={handleMouseUp}
            onClick={handleMouseClick}
          />
        ) : (
          <div className="flex h-[600px] flex-col items-center justify-center gap-4 text-muted-foreground">
            <Monitor className="h-20 w-20" />
            <div className="text-center">
              <p className="text-lg font-medium text-foreground">{t('desktop.waiting')}</p>
              <p className="text-sm">{t('desktop.waitingDescription')}</p>
            </div>
            <button
              onClick={onConnect}
              disabled={disabled}
              className="inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-70"
            >
              <Play className="h-4 w-4" />
              {disabled ? t('desktop.connecting') : t('desktop.start')}
            </button>
          </div>
        )}
      </div>
      {isConnected && (
        <p className="px-1 text-xs text-muted-foreground">{`${remoteWidth} x ${remoteHeight}`} px</p>
      )}
      <div className="space-y-4 rounded-xl border border-border bg-card/80 p-4 shadow-sm">
        <div>
          <p className="text-sm font-semibold text-foreground">{t('deviceDetail.remoteDesktop.clipboardTitle')}</p>
          <p className="text-xs text-muted-foreground">{t('deviceDetail.remoteDesktop.clipboardDescription')}</p>
        </div>
        <div className="flex gap-2">
          <button
            onClick={onClipboardRefresh}
            disabled={clipboard.loading}
            className="inline-flex items-center gap-1 rounded-lg border border-border bg-secondary/30 px-3 py-1.5 text-xs font-semibold text-muted-foreground transition hover:bg-secondary/60 disabled:opacity-60"
          >
            <RefreshCw className={`h-3.5 w-3.5 ${clipboard.loading ? 'animate-spin' : ''}`} />
            {t('deviceDetail.remoteDesktop.clipboardRefresh')}
          </button>
          <button
            onClick={onClipboardApply}
            disabled={clipboard.syncing}
            className="inline-flex items-center gap-1 rounded-lg bg-primary px-3 py-1.5 text-xs font-semibold text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
          >
            <Send className={`h-3.5 w-3.5 ${clipboard.syncing ? 'animate-pulse' : ''}`} />
            {t('deviceDetail.remoteDesktop.clipboardApply')}
          </button>
        </div>
        <textarea
          className="min-h-[120px] w-full rounded-lg border border-border bg-background px-3 py-2 text-sm font-mono text-foreground focus:outline-none focus:ring-2 focus:ring-primary/40"
          value={clipboard.value}
          onChange={(event) => onClipboardChange(event.target.value)}
          placeholder={t('deviceDetail.remoteDesktop.clipboardPlaceholder')}
        />
        {clipboard.error && <p className="mt-2 text-xs text-destructive">{clipboard.error}</p>}
      </div>
      <div className="rounded-xl border border-border bg-card/80 p-4 shadow-sm">
        <div>
          <p className="text-sm font-semibold text-foreground">{t('deviceDetail.remoteDesktop.shortcutsTitle')}</p>
          <p className="text-xs text-muted-foreground">{t('deviceDetail.remoteDesktop.shortcutsDescription')}</p>
        </div>
        <div className="mt-3 grid gap-2 sm:grid-cols-3">
          <button
            onClick={() => onSendShortcut('ctrlAltDel')}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
          >
            {t('deviceDetail.remoteDesktop.shortcutCtrlAltDel')}
          </button>
          <button
            onClick={() => onSendShortcut('ctrlEsc')}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
          >
            {t('deviceDetail.remoteDesktop.shortcutCtrlEsc')}
          </button>
          <button
            onClick={() => onSendShortcut('ctrlShiftEsc')}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
          >
            {t('deviceDetail.remoteDesktop.shortcutCtrlShiftEsc')}
          </button>
        </div>
      </div>
      <div className="space-y-4 rounded-xl border border-border bg-card/80 p-4 shadow-sm">
        <div>
          <p className="text-sm font-semibold text-foreground">{t('deviceDetail.remoteDesktop.powerTitle')}</p>
          <p className="text-xs text-muted-foreground">{t('deviceDetail.remoteDesktop.powerDescription')}</p>
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          <button
            onClick={() => onPowerAction('restart')}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
          >
            {t('deviceDetail.remoteDesktop.powerRestart')}
          </button>
          <button
            onClick={() => onPowerAction('shutdown')}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
          >
            {t('deviceDetail.remoteDesktop.powerShutdown')}
          </button>
          <button
            onClick={() => onPowerAction('sleep')}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-xs font-semibold text-foreground transition hover:bg-secondary/60"
          >
            {t('deviceDetail.remoteDesktop.powerSleep')}
          </button>
        </div>
      </div>
    </section>
  );
}
export function TerminalTab({
  state,
  shellOptions,
  onCommandChange,
  onShellChange,
  onExecute,
  onClear,
  onOpenStandalone,
}: {
  state: TerminalState;
  shellOptions: Array<{ value: string; label: string }>;
  onCommandChange: (value: string) => void;
  onShellChange: (value: string) => void;
  onExecute: () => void;
  onClear: () => void;
  onOpenStandalone: () => void;
}) {
  const { t } = useTranslation();
  const disableRun = state.isRunning || state.command.trim().length === 0;

  return (
    <div className="space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.terminal.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.terminal.description')}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <select
            value={state.shell}
            onChange={(event) => onShellChange(event.target.value)}
            className="rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
          >
            {shellOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <button
            onClick={onOpenStandalone}
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70"
          >
            <TerminalIcon className="h-4 w-4" />
            {t('deviceDetail.terminal.open')}
          </button>
        </div>
      </div>

      <div className="space-y-3 rounded-xl border border-border bg-background/40 p-4">
        <label className="text-xs font-semibold uppercase text-muted-foreground">
          {t('deviceDetail.terminal.commandLabel')}
        </label>
        <div className="flex flex-col gap-3 md:flex-row">
          <input
            value={state.command}
            onChange={(event) => onCommandChange(event.target.value)}
            onKeyDown={(event) => event.key === 'Enter' && !disableRun && onExecute()}
            className="flex-1 rounded-lg border border-border bg-secondary/20 px-3 py-2 font-mono text-sm"
            placeholder={t('deviceDetail.terminal.inputPlaceholder')}
          />
          <button
            onClick={onExecute}
            disabled={disableRun}
            className="inline-flex items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-50"
          >
            <Send className="h-4 w-4" />
            {state.isRunning ? t('deviceDetail.terminal.running') : t('deviceDetail.terminal.run')}
          </button>
        </div>
        {state.error && (
          <p className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {state.error}
          </p>
        )}
      </div>

      <div className="space-y-3 rounded-xl border border-border bg-background/40 p-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-base font-semibold">{t('deviceDetail.terminal.outputTitle')}</h3>
            <p className="text-sm text-muted-foreground">{t('deviceDetail.terminal.outputDescription')}</p>
          </div>
          {state.outputs.length > 0 && (
            <button
              onClick={onClear}
              className="text-xs font-semibold uppercase text-muted-foreground transition hover:text-foreground"
            >
              {t('deviceDetail.terminal.clearOutput')}
            </button>
          )}
        </div>
        <div className="h-72 overflow-y-auto rounded-lg border border-border bg-card">
          {state.outputs.length === 0 ? (
            <p className="p-4 text-sm text-muted-foreground">{t('deviceDetail.terminal.outputEmpty')}</p>
          ) : (
            <ul className="divide-y divide-border text-sm">
              {state.outputs.map((entry) => (
                <li key={entry.id} className="p-4">
                  <div className="text-xs text-muted-foreground">
                    [{entry.time}] $ {entry.cmd}
                  </div>
                  <pre
                    className={`mt-2 whitespace-pre-wrap ${
                      entry.status === 'success' ? 'text-foreground' : 'text-destructive'
                    }`}
                  >
                    {entry.result}
                  </pre>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

export function SoftwareTab({
  state,
  onRefresh,
  onUninstall,
}: {
  state: SoftwareState;
  onRefresh: () => void;
  onUninstall: (productName: string) => void;
}) {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  return (
    <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.software.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.software.description')}</p>
        </div>
        <button
          onClick={onRefresh}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('deviceDetail.software.refresh')}
        </button>
      </div>
      {state.message && (
        <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600">
          {state.message}
        </div>
      )}
      {state.error && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {state.error}
        </div>
      )}
      {state.loading ? (
        <div className="flex h-48 items-center justify-center gap-2 text-muted-foreground">
          <RefreshCw className="h-4 w-4 animate-spin" />
          {t('deviceDetail.software.loading')}
        </div>
      ) : state.items.length === 0 ? (
        <p className="py-10 text-center text-sm text-muted-foreground">{t('deviceDetail.software.empty')}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-border text-sm">
            <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left">{t('deviceDetail.software.columns.name')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.software.columns.version')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.software.columns.publisher')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.software.columns.size')}</th>
                <th className="px-4 py-3 text-left">{t('deviceDetail.software.columns.installDate')}</th>
                <th className="px-4 py-3 text-right">{t('deviceDetail.software.columns.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {state.items.map((item) => {
                const uninstalling = state.actionTarget === item.name;
                return (
                  <tr key={`${item.name}-${item.version}`} className="border-t border-border/70">
                    <td className="px-4 py-3 font-medium text-foreground">{item.name}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">{item.version ?? '-'}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">{item.publisher ?? '-'}</td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      {item.sizeMb ? `${item.sizeMb.toFixed(1)} MB` : '-'}
                    </td>
                    <td className="px-4 py-3 text-xs text-muted-foreground">
                      {item.installDate ? new Date(item.installDate).toLocaleDateString(locale) : '-'}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <button
                        onClick={() => onUninstall(item.name)}
                        disabled={uninstalling}
                        className="inline-flex items-center gap-2 rounded-lg border border-destructive/40 px-3 py-1.5 text-xs font-medium text-destructive transition hover:bg-destructive/10 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {uninstalling ? t('deviceDetail.software.uninstalling') : t('deviceDetail.software.uninstall')}
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

export function PatchesTab({
  state,
  form,
  onFormChange,
  onRefresh,
  onInstallPending,
  onSchedulePatch,
}: {
  state: PatchState;
  form: PatchFormState;
  onFormChange: (patch: Partial<PatchFormState>) => void;
  onRefresh: () => void;
  onInstallPending: () => void;
  onSchedulePatch: () => void;
}) {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const renderPatchRow = (patch: PatchState['installed'][number], index: number) => (
    <tr key={`${patch.kbNumber}-${index}`} className="border-t border-border/60">
      <td className="px-4 py-3 text-sm font-medium text-foreground">{patch.kbNumber ?? '-'}</td>
      <td className="px-4 py-3 text-sm text-muted-foreground">{patch.title ?? patch.description ?? '-'}</td>
      <td className="px-4 py-3 text-xs text-muted-foreground">
        {patch.installDate ? new Date(patch.installDate).toLocaleDateString(locale) : '-'}
      </td>
    </tr>
  );

  const renderUpdateRow = (update: PatchState['pending'][number], index: number) => (
    <tr key={`${update.kbNumber}-${index}`} className="border-t border-border/60">
      <td className="px-4 py-3 text-sm font-medium text-foreground">{update.kbNumber ?? '-'}</td>
      <td className="px-4 py-3 text-sm text-muted-foreground">{update.title ?? update.description ?? '-'}</td>
      <td className="px-4 py-3 text-xs text-muted-foreground">{update.severity ?? '-'}</td>
      <td className="px-4 py-3 text-xs text-muted-foreground">
        {update.sizeMb ? `${update.sizeMb.toFixed(1)} MB` : '-'}
      </td>
    </tr>
  );

  return (
    <section className="space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.patches.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.patches.description')}</p>
        </div>
        <button
          onClick={onRefresh}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('deviceDetail.patches.refresh')}
        </button>
      </div>

      {state.message && (
        <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600">
          {state.message}
        </div>
      )}
      {state.error && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {state.error}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="rounded-xl border border-border bg-background/40">
          <div className="flex items-center justify-between border-b border-border px-4 py-3">
            <div>
              <h3 className="text-sm font-semibold">{t('deviceDetail.patches.installedTitle')}</h3>
              <p className="text-xs text-muted-foreground">{t('deviceDetail.patches.installedDescription')}</p>
            </div>
            <span className="text-xs text-muted-foreground">{state.installed.length}</span>
          </div>
          {state.loading ? (
            <div className="flex h-32 items-center justify-center gap-2 text-muted-foreground">
              <RefreshCw className="h-4 w-4 animate-spin" />
              {t('deviceDetail.patches.loading')}
            </div>
          ) : state.installed.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-muted-foreground">
              {t('deviceDetail.patches.installedEmpty')}
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-border text-sm">
                <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.kb')}</th>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.title')}</th>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.installDate')}</th>
                  </tr>
                </thead>
                <tbody>{state.installed.map(renderPatchRow)}</tbody>
              </table>
            </div>
          )}
        </div>

        <div className="rounded-xl border border-border bg-background/40">
          <div className="flex items-center justify-between border-b border-border px-4 py-3">
            <div>
              <h3 className="text-sm font-semibold">{t('deviceDetail.patches.pendingTitle')}</h3>
              <p className="text-xs text-muted-foreground">{t('deviceDetail.patches.pendingDescription')}</p>
            </div>
            <span className="text-xs text-muted-foreground">{state.pending.length}</span>
          </div>
          {state.loading ? (
            <div className="flex h-32 items-center justify-center gap-2 text-muted-foreground">
              <RefreshCw className="h-4 w-4 animate-spin" />
              {t('deviceDetail.patches.loading')}
            </div>
          ) : state.pending.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-muted-foreground">
              {t('deviceDetail.patches.pendingEmpty')}
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-border text-sm">
                <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.kb')}</th>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.title')}</th>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.severity')}</th>
                    <th className="px-4 py-3 text-left">{t('deviceDetail.patches.columns.size')}</th>
                  </tr>
                </thead>
                <tbody>{state.pending.map(renderUpdateRow)}</tbody>
              </table>
            </div>
          )}
          <div className="border-t border-border px-4 py-3">
            <button
              onClick={onInstallPending}
              disabled={state.busy || state.pending.length === 0}
              className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {state.busy ? t('deviceDetail.patches.installing') : t('deviceDetail.patches.installPending')}
            </button>
          </div>
        </div>
      </div>

      <div className="rounded-xl border border-border bg-background/40 p-4">
        <h3 className="text-sm font-semibold">{t('deviceDetail.patches.scheduleTitle')}</h3>
        <p className="text-xs text-muted-foreground">{t('deviceDetail.patches.scheduleDescription')}</p>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.patches.patchUrl')}
            </label>
            <input
              value={form.patchUrl}
              onChange={(event) => onFormChange({ patchUrl: event.target.value })}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              placeholder="https://updates.example.com/patch.msu"
              disabled={state.busy}
            />
          </div>
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.patches.scheduleTime')}
            </label>
            <input
              type="datetime-local"
              value={form.scheduledTime}
              onChange={(event) => onFormChange({ scheduledTime: event.target.value })}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
              disabled={state.busy}
            />
          </div>
        </div>
        <div className="mt-4 flex justify-end">
          <button
            onClick={onSchedulePatch}
            disabled={state.busy}
            className="inline-flex items-center gap-2 rounded-lg border border-border px-4 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {state.busy ? t('deviceDetail.patches.scheduling') : t('deviceDetail.patches.scheduleAction')}
          </button>
        </div>
      </div>
    </section>
  );
}

export function MessagingTab({
  state,
  onChange,
  onSend,
  disabled,
}: {
  state: MessagingFormState;
  onChange: (patch: Partial<MessagingFormState>) => void;
  onSend: () => void;
  disabled?: boolean;
}) {
  const { t } = useTranslation();
  const actionOptions = useMemo(
    () => [
      { value: 'agentmsg', label: t('deviceDetail.messaging.actions.agentmsg') },
      { value: 'messagebox', label: t('deviceDetail.messaging.actions.messagebox') },
      { value: 'notify', label: t('deviceDetail.messaging.actions.notify') },
      { value: 'toast', label: t('deviceDetail.messaging.actions.toast') },
      { value: 'chat', label: t('deviceDetail.messaging.actions.chat') },
    ],
    [t],
  );
  const canSend = !disabled && !state.sending && state.message.trim().length > 0;

  return (
    <section className="space-y-6 rounded-xl border border-border bg-card p-6 shadow-sm">
      <div>
        <h2 className="text-lg font-semibold">{t('deviceDetail.messaging.title')}</h2>
        <p className="text-sm text-muted-foreground">{t('deviceDetail.messaging.description')}</p>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <label className="text-xs font-semibold uppercase text-muted-foreground">
            {t('deviceDetail.messaging.fields.action')}
          </label>
          <select
            value={state.action}
            onChange={(event) => onChange({ action: event.target.value as MessagingFormState['action'] })}
            className="w-full rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
          >
            {actionOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-2">
          <label className="text-xs font-semibold uppercase text-muted-foreground">
            {t('deviceDetail.messaging.fields.duration')}
          </label>
          <input
            type="number"
            value={state.duration}
            onChange={(event) => onChange({ duration: Number(event.target.value) || 0 })}
            className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
          />
        </div>
        <div className="space-y-2">
          <label className="text-xs font-semibold uppercase text-muted-foreground">
            {t('deviceDetail.messaging.fields.title')}
          </label>
          <input
            value={state.title}
            onChange={(event) => onChange({ title: event.target.value })}
            className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
            placeholder={t('deviceDetail.messaging.titlePlaceholder')}
          />
        </div>
      </div>

      <div className="space-y-2">
        <label className="text-xs font-semibold uppercase text-muted-foreground">
          {t('deviceDetail.messaging.fields.message')}
        </label>
        <textarea
          value={state.message}
          onChange={(event) => onChange({ message: event.target.value })}
          className="h-32 w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
          placeholder={t('deviceDetail.messaging.messagePlaceholder')}
        />
      </div>

      {state.feedback && (
        <p className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600">
          {state.feedback}
        </p>
      )}
      {state.error && (
        <p className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {state.error}
        </p>
      )}

      <div className="flex justify-end">
        <button
          onClick={onSend}
          disabled={!canSend}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
        >
          <Send className="h-4 w-4" />
          {state.sending ? t('deviceDetail.messaging.sending') : t('deviceDetail.messaging.send')}
        </button>
      </div>
    </section>
  );
}

export function MaintenanceTab({
  state,
  onChange,
  onUpdate,
  onReinstall,
  onCollectLogs,
  onShowPrivacy,
  onHidePrivacy,
}: {
  state: MaintenanceFormState;
  onChange: (patch: Partial<MaintenanceFormState>) => void;
  onUpdate: () => void;
  onReinstall: () => void;
  onCollectLogs: () => void;
  onShowPrivacy: () => void;
  onHidePrivacy: () => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.maintenance.updateTitle')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.maintenance.updateDescription')}</p>
        </div>
        <div className="grid gap-4 md:grid-cols-3">
          <input
            value={state.version}
            onChange={(event) => onChange({ version: event.target.value })}
            className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
            placeholder={t('deviceDetail.maintenance.versionPlaceholder')}
          />
          <input
            value={state.channel}
            onChange={(event) => onChange({ channel: event.target.value })}
            className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
            placeholder={t('deviceDetail.maintenance.channelPlaceholder')}
          />
          <label className="flex items-center gap-2 text-sm text-muted-foreground">
            <input
              type="checkbox"
              checked={state.force}
              onChange={(event) => onChange({ force: event.target.checked })}
              className="rounded border-border text-primary focus:ring-primary"
            />
            {t('deviceDetail.maintenance.forceLabel')}
          </label>
        </div>
        <button
          onClick={onUpdate}
          disabled={state.busy}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
        >
          {t('deviceDetail.maintenance.updateAction')}
        </button>
      </section>

      <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.maintenance.reinstallTitle')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.maintenance.reinstallDescription')}</p>
        </div>
        <input
          value={state.installerUrl}
          onChange={(event) => onChange({ installerUrl: event.target.value })}
          className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
          placeholder={t('deviceDetail.maintenance.installerPlaceholder')}
        />
        <label className="flex items-center gap-2 text-sm text-muted-foreground">
          <input
            type="checkbox"
            checked={state.preserveConfig}
            onChange={(event) => onChange({ preserveConfig: event.target.checked })}
            className="rounded border-border text-primary focus:ring-primary"
          />
          {t('deviceDetail.maintenance.preserveLabel')}
        </label>
        <button
          onClick={onReinstall}
          disabled={state.busy}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
        >
          {t('deviceDetail.maintenance.reinstallAction')}
        </button>
      </section>

      <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.maintenance.logsTitle')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.maintenance.logsDescription')}</p>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div className="space-y-2">
            <label className="text-xs font-semibold uppercase text-muted-foreground">
              {t('deviceDetail.maintenance.tailLabel')}
            </label>
            <input
              type="number"
              value={state.tailLines}
              onChange={(event) => onChange({ tailLines: Number(event.target.value) || 0 })}
              className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
            />
          </div>
          <label className="mt-6 flex items-center gap-2 text-sm text-muted-foreground md:mt-0">
            <input
              type="checkbox"
              checked={state.includeDiagnostics}
              onChange={(event) => onChange({ includeDiagnostics: event.target.checked })}
              className="rounded border-border text-primary focus:ring-primary"
            />
            {t('deviceDetail.maintenance.includeDiagnostics')}
          </label>
        </div>
        <button
          onClick={onCollectLogs}
          disabled={state.busy}
          className="inline-flex items-center gap-2 rounded-lg border border-border px-4 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
        >
          {t('deviceDetail.maintenance.collectLogs')}
        </button>
        {state.logResult && (
          <pre className="h-32 overflow-y-auto rounded-lg border border-border bg-secondary/20 p-3 text-xs">{state.logResult}</pre>
        )}
      </section>

      <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.maintenance.privacyTitle')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.maintenance.privacyDescription')}</p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={onShowPrivacy}
            disabled={state.busy}
            className="inline-flex flex-1 items-center justify-center rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
          >
            {t('deviceDetail.maintenance.showPrivacy')}
          </button>
          <button
            onClick={onHidePrivacy}
            disabled={state.busy}
            className="inline-flex flex-1 items-center justify-center rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
          >
            {t('deviceDetail.maintenance.hidePrivacy')}
          </button>
        </div>
      </section>

      {state.message && (
        <p className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600">{state.message}</p>
      )}
      {state.error && (
        <p className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{state.error}</p>
      )}
    </div>
  );
}

export function ScriptsTab({
  state,
  form,
  onFormChange,
  onDeploy,
  onReload,
  onRefresh,
  onRemove,
}: {
  state: ScriptsState;
  form: ScriptFormState;
  onFormChange: (patch: Partial<ScriptFormState>) => void;
  onDeploy: () => void;
  onReload: () => void;
  onRefresh: () => void;
  onRemove: (name: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="space-y-6">
      <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="text-lg font-semibold">{t('deviceDetail.scripts.title')}</h2>
            <p className="text-sm text-muted-foreground">{t('deviceDetail.scripts.description')}</p>
          </div>
          <div className="flex gap-2">
            <button
              onClick={onRefresh}
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70"
            >
              <RefreshCw className="h-4 w-4" />
              {t('deviceDetail.scripts.refresh')}
            </button>
            <button
              onClick={onReload}
              disabled={form.busy}
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-xs font-medium text-muted-foreground transition hover:bg-secondary/70 disabled:opacity-60"
            >
              {t('deviceDetail.scripts.reload')}
            </button>
          </div>
        </div>
        {state.error && (
          <p className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">{state.error}</p>
        )}
        {state.loading ? (
          <div className="flex h-32 items-center justify-center gap-3 text-muted-foreground">
            <RefreshCw className="h-4 w-4 animate-spin" />
            {t('deviceDetail.scripts.loading')}
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <h3 className="text-sm font-medium">{t('deviceDetail.scripts.scriptsTitle')}</h3>
              {state.scripts.length === 0 ? (
                <p className="py-4 text-sm text-muted-foreground">{t('deviceDetail.scripts.empty')}</p>
              ) : (
                <ul className="mt-2 space-y-2">
                  {state.scripts.map((script) => (
                    <li key={script} className="flex items-center justify-between rounded-lg border border-border/60 bg-background/40 px-3 py-2 text-sm">
                      <span>{script}</span>
                      <button
                        onClick={() => onRemove(script)}
                        className="text-xs text-destructive transition hover:underline"
                      >
                        {t('deviceDetail.scripts.remove')}
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div>
              <h3 className="text-sm font-medium">{t('deviceDetail.scripts.handlersTitle')}</h3>
              {state.handlers.length === 0 ? (
                <p className="py-4 text-sm text-muted-foreground">{t('deviceDetail.scripts.emptyHandlers')}</p>
              ) : (
                <ul className="mt-2 space-y-2 text-sm text-muted-foreground">
                  {state.handlers.map((handler) => (
                    <li key={handler} className="rounded-lg border border-border/60 bg-background/40 px-3 py-2">
                      {handler}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        )}
      </section>

      <section className="space-y-4 rounded-xl border border-border bg-card p-6 shadow-sm">
        <div>
          <h2 className="text-lg font-semibold">{t('deviceDetail.scripts.deployTitle')}</h2>
          <p className="text-sm text-muted-foreground">{t('deviceDetail.scripts.description')}</p>
        </div>
        <input
          value={form.name}
          onChange={(event) => onFormChange({ name: event.target.value })}
          className="w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 text-sm"
          placeholder={t('deviceDetail.scripts.namePlaceholder')}
        />
        <textarea
          value={form.code}
          onChange={(event) => onFormChange({ code: event.target.value })}
          className="h-40 w-full rounded-lg border border-border bg-secondary/30 px-3 py-2 font-mono text-sm"
          placeholder={t('deviceDetail.scripts.codePlaceholder')}
        />
        <div className="flex justify-end">
          <button
            onClick={onDeploy}
            disabled={form.busy || !form.code.trim()}
            className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:opacity-60"
          >
            {form.busy ? t('deviceDetail.scripts.deploying') : t('deviceDetail.scripts.deploy')}
          </button>
        </div>
        {form.feedback && (
          <p className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-3 py-2 text-sm text-emerald-600">
            {form.feedback}
          </p>
        )}
        {form.error && (
          <p className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
            {form.error}
          </p>
        )}
      </section>
    </div>
  );
}

export function formatKey(key: string): string {
  return key
    .replace(/([A-Z])/g, ' $1')
    .replace(/_/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^\w/, (c) => c.toUpperCase());
}

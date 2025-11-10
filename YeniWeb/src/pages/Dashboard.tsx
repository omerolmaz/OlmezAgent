import { useEffect, useMemo, useState } from 'react';
import {
  RefreshCw,
  Monitor,
  Activity,
  Users,
  ShieldCheck,
  ArrowRight,
  Plug,
  Laptop,
  Shield,
  TerminalSquare,
  MonitorSmartphone,
  Code2,
  Package,
  ShieldCheck as ShieldCheckOutline,
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { dashboardService } from '../services/dashboard.service';
import type { Device } from '../types/device.types';
import { useTranslation } from '../hooks/useTranslation';

type DashboardStats = {
  totalDevices: number;
  onlineDevices: number;
  warningCount: number;
  activeSessions: number;
};

export default function Dashboard() {
  const navigate = useNavigate();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [recentDevices, setRecentDevices] = useState<Device[]>([]);
  const [loading, setLoading] = useState(true);
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const load = async () => {
    setLoading(true);
    try {
      const [newStats, devices] = await Promise.all([dashboardService.getStats(), dashboardService.getRecentDevices()]);
      setStats(newStats);
      setRecentDevices(devices);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  return (
    <div className="space-y-6 p-6">
      <section className="rounded-3xl border border-border bg-gradient-to-r from-primary/10 via-primary/5 to-transparent p-6 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-xs uppercase tracking-wide text-primary">{t('dashboard.subtitle')}</p>
            <h1 className="mt-2 text-3xl font-semibold text-foreground">{t('dashboard.title')}</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {t('dashboard.description', undefined, 'Monitor device health, sessions, and response readiness.')}
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <button
              onClick={load}
              className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-4 py-2 text-sm font-medium text-muted-foreground transition hover:bg-background"
            >
              <RefreshCw className="h-4 w-4" />
              {t('common.refresh')}
            </button>
            <button className="inline-flex items-center gap-2 rounded-full bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90">
              {t('dashboard.quickAction', undefined, 'Open Command Center')}
              <ArrowRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      </section>

      <StatsGrid stats={stats} loading={loading} />

      <QuickActions navigate={navigate} />

      <section className="grid gap-6 xl:grid-cols-3">
        <Card title={t('dashboard.recentDevices')}>
          <DeviceList devices={recentDevices} loading={loading} />
        </Card>
        <Card title={t('dashboard.liveActivity')}>
          <ActivityHighlights stats={stats} loading={loading} />
        </Card>
        <Card title={t('dashboard.healthCard')}>
          <HealthSummary stats={stats} loading={loading} locale={locale} />
        </Card>
      </section>
    </div>
  );
}

function StatsGrid({ stats, loading }: { stats: DashboardStats | null; loading: boolean }) {
  const { t } = useTranslation();
  const items = useMemo(
    () => [
      { label: t('dashboard.stats.totalDevices'), value: stats?.totalDevices ?? 0, icon: Monitor },
      { label: t('dashboard.stats.onlineDevices'), value: stats?.onlineDevices ?? 0, icon: Activity },
      { label: t('dashboard.stats.warningCount'), value: stats?.warningCount ?? 0, icon: ShieldCheck },
      { label: t('dashboard.stats.activeSessions'), value: stats?.activeSessions ?? 0, icon: Users },
    ],
    [stats, t],
  );

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {items.map((item) => (
        <div key={item.label} className="rounded-2xl border border-border bg-card/80 p-4 shadow-sm backdrop-blur">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-xs uppercase tracking-wide text-muted-foreground">{item.label}</p>
              <p className="mt-2 text-3xl font-bold text-foreground">{loading ? '—' : item.value}</p>
            </div>
            <div className="rounded-full bg-primary/10 p-3">
              <item.icon className="h-5 w-5 text-primary" />
            </div>
          </div>
          {!loading && (
            <div className="mt-3 text-xs text-muted-foreground">
              {t('dashboard.updatedAt', { date: new Date().toLocaleTimeString() }, 'Updated moments ago')}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function Card({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="flex h-full flex-col rounded-2xl border border-border bg-card/80 p-5 shadow-sm">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-foreground">{title}</h2>
      </div>
      <div className="flex-1 overflow-auto text-sm text-muted-foreground">{children}</div>
    </div>
  );
}

function DeviceList({ devices, loading }: { devices: Device[]; loading: boolean }) {
  const { t } = useTranslation();

  if (loading) {
    return <div className="flex h-40 items-center justify-center">{t('common.loading')}</div>;
  }

  if (devices.length === 0) {
    return <p className="text-sm text-muted-foreground">{t('dashboard.noDevices')}</p>;
  }

  return (
    <ul className="space-y-3">
      {devices.map((device) => (
        <li key={device.id} className="rounded-xl border border-border/60 bg-background/40 p-4">
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <p className="font-medium text-foreground">{device.hostname}</p>
              <p className="text-xs text-muted-foreground">
                {(device.ipAddress ?? t('common.noData'))} • {(device.osVersion ?? t('common.noData'))}
              </p>
            </div>
            <span
              className={`flex h-8 w-8 items-center justify-center rounded-full text-xs font-semibold ${
                device.status === 'Connected'
                  ? 'bg-emerald-500/15 text-emerald-600'
                  : device.status === 'Connecting'
                    ? 'bg-blue-500/15 text-blue-600'
                    : 'bg-muted text-muted-foreground'
              }`}
            >
              {device.status.charAt(0)}
            </span>
          </div>
        </li>
      ))}
    </ul>
  );
}

function ActivityHighlights({ stats, loading }: { stats: DashboardStats | null; loading: boolean }) {
  const { t } = useTranslation();
  const items = [
    {
      label: t('dashboard.activity.sessions', undefined, 'Active sessions'),
      value: stats?.activeSessions ?? 0,
      icon: Plug,
    },
    {
      label: t('dashboard.activity.devices', undefined, 'Online devices'),
      value: stats?.onlineDevices ?? 0,
      icon: Laptop,
    },
    {
      label: t('dashboard.activity.security', undefined, 'Security alerts'),
      value: stats?.warningCount ?? 0,
      icon: Shield,
    },
    {
      label: t('dashboard.activity.commands', undefined, 'Command executions'),
      value: (stats?.onlineDevices ?? 0) - (stats?.warningCount ?? 0),
      icon: TerminalSquare,
    },
  ];

  return (
    <ul className="space-y-3">
      {items.map((item) => (
        <li key={item.label} className="flex items-center justify-between rounded-xl border border-border/60 bg-background/40 p-3">
          <div className="flex items-center gap-3">
            <div className="rounded-xl bg-secondary/50 p-2">
              <item.icon className="h-4 w-4 text-primary" />
            </div>
            <div>
              <p className="text-sm font-medium text-foreground">{item.label}</p>
              <p className="text-xs text-muted-foreground">
                {loading ? t('common.loading') : t('dashboard.activity.updated', undefined, 'Updated moments ago')}
              </p>
            </div>
          </div>
          <span className="text-2xl font-semibold text-foreground">{loading ? '—' : item.value}</span>
        </li>
      ))}
    </ul>
  );
}

function HealthSummary({ stats, loading, locale }: { stats: DashboardStats | null; loading: boolean; locale: string }) {
  const { t } = useTranslation();
  const healthItems = [
    {
      label: t('dashboard.health.onlineRatio', undefined, 'Online ratio'),
      value:
        stats && stats.totalDevices > 0 ? `${Math.round((stats.onlineDevices / stats.totalDevices) * 100)}%` : '—',
      detail: t('dashboard.health.onlineDetail', undefined, 'Devices responding to heartbeat'),
    },
    {
      label: t('dashboard.health.alertWindow', undefined, 'Alerts window'),
      value: stats ? stats.warningCount : '—',
      detail: t('dashboard.health.alertDetail', undefined, 'Endpoints requiring attention'),
    },
    {
      label: t('dashboard.health.sessionWindow', undefined, 'Sessions window'),
      value: stats ? stats.activeSessions : '—',
      detail: t('dashboard.health.sessionDetail', undefined, 'Desktop or terminal sessions open now'),
    },
  ];

  return (
    <div className="space-y-4">
      {healthItems.map((item) => (
        <div key={item.label} className="rounded-xl border border-border/60 bg-background/40 p-4">
          <p className="text-xs uppercase text-muted-foreground">{item.label}</p>
          <p className="mt-2 text-2xl font-semibold text-foreground">{loading ? '—' : item.value}</p>
          <p className="text-xs text-muted-foreground">{item.detail}</p>
        </div>
      ))}
      <p className="text-xs text-muted-foreground">
        {t('dashboard.updatedAt', { date: new Date().toLocaleString(locale) })}
      </p>
    </div>
  );
}

function QuickActions({ navigate }: { navigate: ReturnType<typeof useNavigate> }) {
  const { t } = useTranslation();
  const actions = [
    {
      title: t('dashboard.shortcuts.remoteDesktop.title'),
      description: t('dashboard.shortcuts.remoteDesktop.description'),
      icon: MonitorSmartphone,
      cta: t('dashboard.shortcuts.remoteDesktop.cta'),
      onClick: () => navigate('/devices'),
    },
    {
      title: t('dashboard.shortcuts.terminal.title'),
      description: t('dashboard.shortcuts.terminal.description'),
      icon: Code2,
      cta: t('dashboard.shortcuts.terminal.cta'),
      onClick: () => navigate('/commands'),
    },
    {
      title: t('dashboard.shortcuts.software.title'),
      description: t('dashboard.shortcuts.software.description'),
      icon: Package,
      cta: t('dashboard.shortcuts.software.cta'),
      onClick: () => navigate('/bulk-operations'),
    },
    {
      title: t('dashboard.shortcuts.patches.title'),
      description: t('dashboard.shortcuts.patches.description'),
      icon: ShieldCheckOutline,
      cta: t('dashboard.shortcuts.patches.cta'),
      onClick: () => navigate('/deployment'),
    },
  ];

  return (
    <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
      {actions.map((action) => {
        const Icon = action.icon;
        return (
          <div key={action.title} className="rounded-2xl border border-border bg-card/80 p-4 shadow-sm">
            <div className="flex items-center gap-3">
              <div className="rounded-xl bg-primary/10 p-3">
                <Icon className="h-5 w-5 text-primary" />
              </div>
              <div>
                <h3 className="text-sm font-semibold text-foreground">{action.title}</h3>
                <p className="text-xs text-muted-foreground">{action.description}</p>
              </div>
            </div>
            <button
              onClick={action.onClick}
              className="mt-4 inline-flex items-center gap-2 rounded-full bg-secondary/40 px-3 py-1.5 text-xs font-medium text-foreground transition hover:bg-secondary/60"
            >
              {action.cta}
              <ArrowRight className="h-3 w-3" />
            </button>
          </div>
        );
      })}
    </section>
  );
}

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Search,
  RefreshCw,
  Monitor,
  Terminal as TerminalIcon,
  FolderOpen,
  Info,
  BarChart3,
  Shield,
  Clock,
  Zap,
  Package2,
  CheckSquare,
  Square,
  Trash2,
} from 'lucide-react';
import { deviceService } from '../services/device.service';
import { commandService } from '../services/command.service';
import type { Device } from '../types/device.types';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';

type StatusFilter = 'all' | 'connected' | 'disconnected' | 'error';
const STATUS_COLORS: Record<Device['status'], string> = {
  Connected: 'bg-emerald-500',
  Connecting: 'bg-blue-400',
  Disconnected: 'bg-muted-foreground/40',
  Reconnecting: 'bg-amber-500',
  Error: 'bg-destructive',
};

export default function Devices() {
  const navigate = useNavigate();
  const [devices, setDevices] = useState<Device[]>([]);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [contextMenu, setContextMenu] = useState<{ x: number; y: number; device: Device } | null>(null);
  const contextRef = useRef<HTMLDivElement | null>(null);
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const statusLabels = useMemo<Record<Device['status'], string>>(
    () => ({
      Connected: t('devices.statusLabels.Connected'),
      Connecting: t('devices.statusLabels.Connecting'),
      Disconnected: t('devices.statusLabels.Disconnected'),
      Reconnecting: t('devices.statusLabels.Reconnecting'),
      Error: t('devices.statusLabels.Error'),
    }),
    [t],
  );

  const filteredDevices = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return devices.filter((device) => {
      const matchesSearch = normalizedSearch
        ? [device.hostname, device.ipAddress ?? '', device.domain ?? '', device.osVersion ?? '', device.agentVersion ?? '']
            .join(' ')
            .toLowerCase()
            .includes(normalizedSearch)
        : true;
      const matchesStatus =
        statusFilter === 'all'
          ? true
          : statusFilter === 'connected'
            ? device.status === 'Connected'
            : statusFilter === 'disconnected'
              ? device.status === 'Disconnected'
              : device.status === 'Error';
      return matchesSearch && matchesStatus;
    });
  }, [devices, search, statusFilter]);

  const loadDevices = useCallback(async () => {
    try {
      const [list, active] = await Promise.all([
        deviceService.getDevices(),
        commandService.getActiveConnections(),
      ]);
      const connected = new Set(active.deviceIds.map((id) => id.toLowerCase()));
      setDevices(
        list.map((device) => ({
          ...device,
          status: connected.has(device.id.toLowerCase()) ? 'Connected' : device.status,
        })),
      );
    } catch (error) {
      console.error('Failed to load devices', error);
    }
  }, []);

  useEffect(() => {
    loadDevices();
    const interval = setInterval(loadDevices, 30_000);
    return () => clearInterval(interval);
  }, [loadDevices]);

  useEffect(() => {
    const handleOutside = (event: MouseEvent) => {
      if (contextRef.current && !contextRef.current.contains(event.target as Node)) {
        setContextMenu(null);
      }
    };
    document.addEventListener('mousedown', handleOutside);
    return () => document.removeEventListener('mousedown', handleOutside);
  }, []);

  const toggleSelection = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleAll = () => {
    setSelected((prev) => {
      if (prev.size === filteredDevices.length) return new Set();
      return new Set(filteredDevices.map((device) => device.id));
    });
  };

  const handleDeleteDevice = useCallback(async (device: Device) => {
    const confirmMsg = t(
      'devices.deleteConfirm',
      { hostname: device.hostname },
      `Are you sure you want to delete ${device.hostname}?`,
    );
    if (!window.confirm(confirmMsg)) return;

    try {
      await deviceService.deleteDevice(device.id);
      setContextMenu(null);
      await loadDevices();
    } catch (error: unknown) {
      const message = toErrorMessage(error, t('devices.deleteError', undefined, 'Error deleting device'));
      alert(message);
    }
  }, [loadDevices, t]);

  const menuItems = useMemo(
    () => [
      { label: t('devices.context.overview'), icon: Info, route: (device: Device) => `/devices/${device.id}?tab=overview` },
      { label: t('devices.context.detail'), icon: BarChart3, route: (device: Device) => `/devices/${device.id}` },
      { label: t('devices.context.desktop'), icon: Monitor, route: (device: Device) => `/devices/${device.id}/desktop` },
      { label: t('devices.context.terminal'), icon: TerminalIcon, route: (device: Device) => `/devices/${device.id}/terminal` },
      { label: t('devices.context.files'), icon: FolderOpen, route: (device: Device) => `/devices/${device.id}/files` },
      { label: t('devices.context.inventory'), icon: Package2, route: (device: Device) => `/devices/${device.id}?tab=inventory` },
      { label: t('devices.context.security'), icon: Shield, route: (device: Device) => `/devices/${device.id}?tab=security` },
      { label: t('devices.context.eventlogs'), icon: Clock, route: (device: Device) => `/devices/${device.id}?tab=eventlogs` },
      { label: t('devices.context.sessions'), icon: Zap, route: (device: Device) => `/devices/${device.id}?tab=sessions` },
      { label: t('devices.context.delete'), icon: Trash2, action: handleDeleteDevice, isDanger: true },
    ],
    [handleDeleteDevice, t],
  );

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('devices.title')}</h1>
          <p className="text-sm text-muted-foreground">
            {t('devices.subtitle', {
              count: filteredDevices.length,
              online: devices.filter((device) => device.status === 'Connected').length,
            })}
          </p>
        </div>
        <button
          onClick={loadDevices}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90"
        >
          <RefreshCw className="h-4 w-4" />
          {t('common.refresh')}
        </button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div className="relative flex-1 min-w-[220px]">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={t('devices.searchPlaceholder')}
            className="w-full rounded-lg border border-border bg-secondary/40 pl-10 pr-4 py-2 text-sm"
          />
        </div>
        <select
          value={statusFilter}
          onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
          className="rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
        >
          <option value="all">{t('devices.filterAll')}</option>
          <option value="connected">{t('devices.filterOnline')}</option>
          <option value="disconnected">{t('devices.filterOffline')}</option>
          <option value="error">{t('devices.filterError')}</option>
        </select>
      </div>

      <div className="overflow-hidden rounded-xl border border-border bg-card">
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-border text-sm">
            <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 w-12">
                  <button onClick={toggleAll} className="text-muted-foreground hover:text-primary transition">
                    {selected.size === filteredDevices.length && filteredDevices.length > 0 ? (
                      <CheckSquare className="h-5 w-5" />
                    ) : (
                      <Square className="h-5 w-5" />
                    )}
                  </button>
                </th>
                <th className="px-4 py-3 text-left">{t('devices.table.status')}</th>
                <th className="px-4 py-3 text-left">{t('devices.table.hostname')}</th>
                <th className="px-4 py-3 text-left">{t('devices.table.ip')}</th>
                <th className="px-4 py-3 text-left">{t('devices.table.domain')}</th>
                <th className="px-4 py-3 text-left">{t('devices.table.os')}</th>
                <th className="px-4 py-3 text-left">{t('devices.table.agent')}</th>
                <th className="px-4 py-3 text-left">{t('devices.table.lastSeen')}</th>
              </tr>
            </thead>
            <tbody>
              {filteredDevices.length === 0 ? (
                <tr>
                  <td colSpan={8} className="px-4 py-6 text-center text-muted-foreground">
                    {t('devices.empty')}
                  </td>
                </tr>
              ) : (
                filteredDevices.map((device) => (
                  <tr
                    key={device.id}
                    onContextMenu={(event) => {
                      event.preventDefault();
                      setContextMenu({ x: event.clientX, y: event.clientY, device });
                    }}
                    className="border-t border-border/70 bg-card transition hover:bg-secondary/40"
                  >
                    <td className="px-4 py-3">
                      <button
                        onClick={(event) => {
                          event.stopPropagation();
                          toggleSelection(device.id);
                        }}
                        className="text-muted-foreground hover:text-primary transition"
                      >
                        {selected.has(device.id) ? <CheckSquare className="h-5 w-5" /> : <Square className="h-5 w-5" />}
                      </button>
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <span className={`h-2.5 w-2.5 rounded-full ${STATUS_COLORS[device.status]}`} />
                        <span className="text-xs font-medium text-muted-foreground">{statusLabels[device.status]}</span>
                      </div>
                    </td>
                    <td
                      className="px-4 py-3 font-medium text-foreground hover:text-primary cursor-pointer"
                      onClick={() => navigate(`/devices/${device.id}`)}
                    >
                      {device.hostname}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{device.ipAddress ?? t('devices.noIp')}</td>
                    <td className="px-4 py-3 text-muted-foreground">{device.domain ?? '-'}</td>
                    <td className="px-4 py-3 text-muted-foreground">{device.osVersion ?? t('devices.unknownOs')}</td>
                    <td className="px-4 py-3 text-muted-foreground">{device.agentVersion ?? '-'}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {device.lastSeenAt ? new Date(device.lastSeenAt).toLocaleString(locale) : '-'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {contextMenu && (
        <div
          ref={contextRef}
          style={{ top: contextMenu.y, left: contextMenu.x }}
          className="fixed z-50 min-w-[220px] rounded-lg border border-border bg-card py-2 shadow-2xl"
        >
          {menuItems.map((item) => (
            <button
              key={item.label}
              onClick={() => {
                if ('action' in item && item.action) {
                  item.action(contextMenu.device);
                } else if ('route' in item && item.route) {
                  navigate(item.route(contextMenu.device));
                  setContextMenu(null);
                }
              }}
              className={`flex w-full items-center gap-3 px-4 py-2 text-sm transition hover:bg-secondary/60 ${
                'isDanger' in item && item.isDanger ? 'text-destructive hover:bg-destructive/10' : 'text-foreground'
              }`}
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </button>
          ))}
        </div>
      )}

      {selected.size > 0 && (
        <div className="fixed bottom-6 left-1/2 z-40 flex -translate-x-1/2 items-center gap-3 rounded-xl border border-border bg-card px-6 py-4 shadow-lg">
          <span className="text-sm">{t('devices.selection.summary', { count: selected.size })}</span>
          <button
            onClick={() => navigate('/bulk-operations', { state: { selectedDevices: Array.from(selected) } })}
            className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90"
          >
            {t('devices.selection.runBulk')}
          </button>
          <button
            onClick={() => setSelected(new Set())}
            className="inline-flex items-center gap-2 rounded-lg border border-border px-4 py-2 text-sm text-muted-foreground transition hover:bg-secondary/70"
          >
            {t('devices.selection.clear')}
          </button>
        </div>
      )}
    </div>
  );
}





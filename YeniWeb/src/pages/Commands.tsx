import { useCallback, useEffect, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { deviceService } from '../services/device.service';
import { commandService } from '../services/command.service';
import type { Device } from '../types/device.types';
import type { CommandExecution } from '../types/command.types';
import { useTranslation } from '../hooks/useTranslation';

export default function Commands() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [selectedDevice, setSelectedDevice] = useState<string>('');
  const [commands, setCommands] = useState<CommandExecution[]>([]);
  const [loading, setLoading] = useState(false);
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const loadDevices = useCallback(async () => {
    const list = await deviceService.getDevices();
    setDevices(list);
    setSelectedDevice((current) => {
      if (current) {
        return current;
      }
      return list.length ? list[0].id : '';
    });
  }, []);

  const loadCommands = useCallback(async (deviceId: string) => {
    setLoading(true);
    try {
      const items = await commandService.getCommandsByDevice(deviceId);
      setCommands(items);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  useEffect(() => {
    if (selectedDevice) {
      void loadCommands(selectedDevice);
    }
  }, [loadCommands, selectedDevice]);

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('commands.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('commands.description')}</p>
        </div>
        <button
          onClick={() => selectedDevice && loadCommands(selectedDevice)}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('commands.refresh')}
        </button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div>
          <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('commands.device')}</label>
          <select
            value={selectedDevice}
            onChange={(event) => setSelectedDevice(event.target.value)}
            className="min-w-[240px] rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
          >
            {devices.map((device) => (
              <option key={device.id} value={device.id}>
                {device.hostname} ({device.ipAddress ?? t('devices.noIp')})
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-border bg-card">
        <table className="min-w-full divide-y divide-border text-sm">
          <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">{t('commands.table.command')}</th>
              <th className="px-4 py-3 text-left">{t('commands.table.status')}</th>
              <th className="px-4 py-3 text-left">{t('commands.table.result')}</th>
              <th className="px-4 py-3 text-left">{t('commands.table.started')}</th>
              <th className="px-4 py-3 text-left">{t('commands.table.finished')}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-muted-foreground">
                  {t('commands.loading')}
                </td>
              </tr>
            ) : commands.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-muted-foreground">
                  {t('commands.empty')}
                </td>
              </tr>
            ) : (
              commands.map((command) => (
                <tr key={command.id} className="border-t border-border/70">
                  <td className="px-4 py-3 font-medium text-foreground">{command.commandType}</td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    {t(`deviceDetail.commands.statusLabels.${command.status}`, undefined, command.status)}
                  </td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    <code className="block max-w-[320px] truncate font-mono text-xs">{command.result ?? '-'}</code>
                  </td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    {new Date(command.createdAt).toLocaleString(locale)}
                  </td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    {command.completedAt ? new Date(command.completedAt).toLocaleString(locale) : '-'}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}






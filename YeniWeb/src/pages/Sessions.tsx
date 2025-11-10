import { useCallback, useEffect, useState } from 'react';
import { RefreshCw, StopCircle } from 'lucide-react';
import { deviceService } from '../services/device.service';
import { sessionsService } from '../services/sessions.service';
import type { Device } from '../types/device.types';
import type { SessionSummary } from '../types/session.types';
import { useTranslation } from '../hooks/useTranslation';

export default function Sessions() {
  const [devices, setDevices] = useState<Device[]>([]);
  const [deviceId, setDeviceId] = useState('');
  const [sessions, setSessions] = useState<SessionSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const loadDevices = useCallback(async () => {
    const list = await deviceService.getDevices();
    setDevices(list);
    setDeviceId((current) => (current ? current : list.length ? list[0].id : ''));
  }, []);

  const loadSessions = useCallback(async (id: string) => {
    setLoading(true);
    try {
      const result = await sessionsService.getSessionsByDevice(id);
      setSessions(result);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadDevices();
  }, [loadDevices]);

  useEffect(() => {
    if (deviceId) {
      void loadSessions(deviceId);
    }
  }, [deviceId, loadSessions]);

  const handleEnd = useCallback(
    async (sessionId: string) => {
      await sessionsService.endSession(sessionId);
      if (deviceId) {
        await loadSessions(deviceId);
      }
    },
    [deviceId, loadSessions],
  );

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('sessions.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('sessions.description')}</p>
        </div>
        <button
          onClick={() => deviceId && loadSessions(deviceId)}
          className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm font-medium text-muted-foreground transition hover:bg-secondary/70"
        >
          <RefreshCw className="h-4 w-4" />
          {t('sessions.refresh')}
        </button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div>
          <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('sessions.device')}</label>
          <select
            value={deviceId}
            onChange={(event) => setDeviceId(event.target.value)}
            className="min-w-[240px] rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
          >
            {devices.map((device) => (
              <option key={device.id} value={device.id}>
                {device.hostname}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border border-border bg-card">
        <table className="min-w-full divide-y divide-border text-sm">
          <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">{t('sessions.type')}</th>
              <th className="px-4 py-3 text-left">{t('sessions.sessionId')}</th>
              <th className="px-4 py-3 text-left">{t('sessions.started')}</th>
              <th className="px-4 py-3 text-left">{t('sessions.lastActivity')}</th>
              <th className="px-4 py-3 text-left">{t('sessions.state')}</th>
              <th className="px-4 py-3 text-left">{t('sessions.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-muted-foreground">
                  {t('sessions.loading')}
                </td>
              </tr>
            ) : sessions.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-muted-foreground">
                  {t('sessions.empty')}
                </td>
              </tr>
            ) : (
              sessions.map((session) => (
                <tr key={session.id} className="border-t border-border/70">
                  <td className="px-4 py-3 font-medium text-foreground">{session.sessionType}</td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">{session.sessionId}</td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    {new Date(session.startedAt).toLocaleString(locale)}
                  </td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    {session.lastActivityAt ? new Date(session.lastActivityAt).toLocaleString(locale) : '-'}
                  </td>
                  <td className="px-4 py-3 text-xs text-muted-foreground">
                    {session.isActive ? t('sessions.stateActive') : t('sessions.stateClosed')}
                  </td>
                  <td className="px-4 py-3 text-right text-xs text-muted-foreground">
                    {session.isActive ? (
                      <button
                        onClick={() => handleEnd(session.id)}
                        className="inline-flex items-center gap-2 rounded-lg border border-border px-3 py-1 text-xs"
                      >
                        <StopCircle className="h-3 w-3" />
                        {t('sessions.end')}
                      </button>
                    ) : (
                      '-'
                    )}
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

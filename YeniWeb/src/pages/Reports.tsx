import { useEffect, useMemo, useState } from 'react';
import { FileText, RefreshCw } from 'lucide-react';
import { reportsService, type GeneratedReport, type ReportCategory } from '../services/reports.service';
import { deviceService } from '../services/device.service';
import type { Device } from '../types/device.types';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';

type ReportOption = {
  id: ReportCategory;
  label: string;
  description: string;
  requiresDevice: boolean;
};

export default function Reports() {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';
  const [category, setCategory] = useState<ReportCategory>('inventory');
  const [deviceId, setDeviceId] = useState('');
  const [devices, setDevices] = useState<Device[]>([]);
  const [reports, setReports] = useState<GeneratedReport[]>([]);
  const [loadingDevices, setLoadingDevices] = useState(false);
  const [loadingReport, setLoadingReport] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deviceError, setDeviceError] = useState<string | null>(null);

  const reportOptions: ReportOption[] = useMemo(
    () => [
      {
        id: 'inventory',
        label: t('reports.options.inventory.label'),
        description: t('reports.options.inventory.description'),
        requiresDevice: true,
      },
      {
        id: 'security',
        label: t('reports.options.security.label'),
        description: t('reports.options.security.description'),
        requiresDevice: true,
      },
      {
        id: 'updates',
        label: t('reports.options.updates.label'),
        description: t('reports.options.updates.description'),
        requiresDevice: true,
      },
      {
        id: 'operations',
        label: t('reports.options.operations.label'),
        description: t('reports.options.operations.description'),
        requiresDevice: true,
      },
      {
        id: 'devices',
        label: t('reports.options.devices.label'),
        description: t('reports.options.devices.description'),
        requiresDevice: false,
      },
    ],
    [t],
  );

  const optionLookup = useMemo(
    () =>
      reportOptions.reduce<Record<ReportCategory, ReportOption | undefined>>((acc, option) => {
        acc[option.id] = option;
        return acc;
      }, {} as Record<ReportCategory, ReportOption | undefined>),
    [reportOptions],
  );

  const requiresDevice = optionLookup[category]?.requiresDevice ?? false;

  useEffect(() => {
    const loadDevices = async () => {
      setLoadingDevices(true);
      setDeviceError(null);
      try {
        const list = await deviceService.getDevices();
        setDevices(list);
        if (list.length > 0) {
          setDeviceId((previous) => previous || list[0].id);
        }
      } catch (err: unknown) {
        setDeviceError(toErrorMessage(err, t('reports.deviceError')));
      } finally {
        setLoadingDevices(false);
      }
    };

    loadDevices();
  }, [t]);

  const handleGenerate = async () => {
    if (requiresDevice && !deviceId) {
      setError(t('reports.requiresDevice'));
      return;
    }

    setError(null);
    setLoadingReport(true);

    try {
      const report = await reportsService.generateReport(category, requiresDevice ? deviceId : undefined);
      setReports((prev) => [report, ...prev]);
    } catch (err: unknown) {
      setError(toErrorMessage(err, t('reports.deviceError')));
    } finally {
      setLoadingReport(false);
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-semibold">{t('reports.title')}</h1>
        <p className="text-sm text-muted-foreground">{t('reports.description')}</p>
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
          <h2 className="mb-4 flex items-center gap-2 text-sm font-semibold">
            <FileText className="h-4 w-4 text-primary" />
            {t('reports.configuration')}
          </h2>

          <div className="space-y-4 text-sm">
            <div>
              <label className="mb-2 block text-xs uppercase text-muted-foreground">{t('reports.typeLabel')}</label>
              <div className="space-y-2">
                {reportOptions.map((option) => (
                  <label
                    key={option.id}
                    className={`flex cursor-pointer items-start gap-3 rounded-lg border px-3 py-2 transition ${
                      category === option.id ? 'border-primary bg-primary/5' : 'border-border hover:bg-secondary/40'
                    }`}
                  >
                    <input
                      type="radio"
                      name="report-category"
                      value={option.id}
                      checked={category === option.id}
                      onChange={() => setCategory(option.id)}
                      className="mt-1"
                    />
                    <div>
                      <p className="font-medium text-foreground">{option.label}</p>
                      <p className="text-xs text-muted-foreground">{option.description}</p>
                    </div>
                  </label>
                ))}
              </div>
            </div>

            {requiresDevice && (
              <div>
                <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('reports.deviceLabel')}</label>
                <select
                  value={deviceId}
                  onChange={(event) => setDeviceId(event.target.value)}
                  className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2"
                  disabled={loadingDevices || !!deviceError}
                >
                  {devices.map((device) => (
                    <option key={device.id} value={device.id}>
                      {device.hostname} ({device.id})
                    </option>
                  ))}
                </select>
                {deviceError && <p className="mt-2 text-xs text-destructive">{deviceError}</p>}
              </div>
            )}

            {error && (
              <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-xs text-destructive">
                {error}
              </div>
            )}

            <button
              onClick={handleGenerate}
              disabled={loadingReport || (requiresDevice && !deviceId) || loadingDevices}
              className="inline-flex w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {loadingReport ? (
                <>
                  <RefreshCw className="h-4 w-4 animate-spin" />
                  {t('reports.generating')}
                </>
              ) : (
                <>
                  <FileText className="h-4 w-4" />
                  {t('reports.generate')}
                </>
              )}
            </button>
          </div>
        </div>

        <div className="lg:col-span-2 rounded-xl border border-border bg-card p-6 shadow-sm">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-sm font-semibold">{t('reports.historyTitle')}</h2>
            {reports.length > 0 && (
              <span className="text-xs text-muted-foreground">{t('reports.itemCount', { count: reports.length })}</span>
            )}
          </div>

          {reports.length === 0 ? (
            <div className="flex h-48 flex-col items-center justify-center gap-2 text-sm text-muted-foreground">
              <FileText className="h-6 w-6" />
              <p>{t('reports.historyEmpty')}</p>
            </div>
          ) : (
            <ul className="space-y-4">
              {reports.map((report, index) => {
                const optionLabel = optionLookup[report.category]?.label ?? report.category;

                return (
                  <li
                    key={`${report.category}-${report.generatedAt}-${index}`}
                    className="rounded-lg border border-border/60 bg-background/40 p-4 text-sm"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div>
                        <p className="font-semibold text-foreground">{report.title}</p>
                        <p className="text-xs text-muted-foreground">
                          {t('reports.generatedAt', {
                            category: optionLabel,
                            date: new Date(report.generatedAt).toLocaleString(locale),
                          })}
                        </p>
                      </div>
                      {report.sourceCommands && report.sourceCommands.length > 0 && (
                        <span className="rounded-full bg-secondary/60 px-2 py-1 text-xs text-muted-foreground">
                          {t('reports.commandRefs', { count: report.sourceCommands.length })}
                        </span>
                      )}
                    </div>
                    <pre className="mt-3 max-h-60 overflow-auto rounded-lg border border-border/60 bg-card/60 p-3 font-mono text-xs text-foreground">
                      {report.data ? JSON.stringify(report.data, null, 2) : t('reports.noData')}
                    </pre>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Download, Shield, Server, ExternalLink, Loader2, CheckCircle2, AlertCircle } from 'lucide-react';
import { agentInstallerService } from '../services/agentInstaller.service';
import { activeDirectoryService } from '../services/activeDirectory.service';
import type { ADDomainInfo } from '../types/activeDirectory.types';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';
import { formatDateTime } from '../utils/format';

function useDownload() {
  return useCallback((blob: Blob, filename: string) => {
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.append(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  }, []);
}

export default function Deployment() {
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';

  const [deviceName, setDeviceName] = useState('');
  const [groupName, setGroupName] = useState('');
  const [enrollmentKey, setEnrollmentKey] = useState('');

  const [installerLoading, setInstallerLoading] = useState(false);
  const [installerError, setInstallerError] = useState<string | null>(null);

  const [gpoLoading, setGpoLoading] = useState(false);
  const [gpoError, setGpoError] = useState<string | null>(null);

  const [adInfo, setAdInfo] = useState<ADDomainInfo | null>(null);
  const [adInfoError, setAdInfoError] = useState<string | null>(null);
  const [adStatus, setAdStatus] = useState<{ connected: boolean; message: string } | null>(null);
  const [adStatusError, setAdStatusError] = useState<string | null>(null);

  const download = useDownload();

  const domainName = useMemo(() => {
    if (adInfo?.name) return adInfo.name;
    if (adStatus?.connected) {
      return document.location.hostname;
    }
    return '';
  }, [adInfo, adStatus]);

  useEffect(() => {
    let cancelled = false;

    async function loadAdInfo() {
      try {
        const [status, info] = await Promise.allSettled([
          activeDirectoryService.testConnection(),
          activeDirectoryService.getDomainInfo(),
        ]);

        if (cancelled) return;

        if (status.status === 'fulfilled') {
          setAdStatus(status.value);
        } else {
          setAdStatusError(toErrorMessage(status.reason, t('deployment.ad.connectionError')));
        }

        if (info.status === 'fulfilled') {
          setAdInfo(info.value);
        } else {
          setAdInfoError(toErrorMessage(info.reason, t('deployment.ad.domainInfoError')));
        }
      } catch (error) {
        if (!cancelled) {
          setAdInfoError(toErrorMessage(error, t('deployment.ad.domainInfoError')));
        }
      }
    }

    void loadAdInfo();
    return () => {
      cancelled = true;
    };
  }, [t]);

  const handleDownloadInstaller = useCallback(async () => {
    setInstallerLoading(true);
    setInstallerError(null);
    try {
      const blob = await agentInstallerService.downloadWindowsInstaller({
        deviceName,
        groupName,
        enrollmentKey,
      });
      download(blob, `OlmezAgentInstaller_${deviceName || 'Default'}.zip`);
    } catch (error) {
      setInstallerError(toErrorMessage(error, t('deployment.installer.error')));
    } finally {
      setInstallerLoading(false);
    }
  }, [deviceName, download, enrollmentKey, groupName, t]);

  const handleDownloadGpo = useCallback(async () => {
    if (!domainName) {
      setGpoError(t('deployment.ad.domainRequired'));
      return;
    }
    setGpoLoading(true);
    setGpoError(null);
    try {
      const blob = await agentInstallerService.downloadGpoPackage({
        domainName,
        groupName,
      });
      download(blob, `OlmezAgent_GPO_${domainName}.zip`);
    } catch (error) {
      setGpoError(toErrorMessage(error, t('deployment.gpo.error')));
    } finally {
      setGpoLoading(false);
    }
  }, [domainName, download, groupName, t]);

  return (
    <div className="space-y-6 p-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">{t('deployment.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('deployment.subtitle')}</p>
        </div>
      </header>

      <section className="grid gap-6 lg:grid-cols-2">
        <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
          <div className="mb-4 flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 text-primary">
              <Download className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-foreground">{t('deployment.installer.title')}</h2>
              <p className="text-sm text-muted-foreground">{t('deployment.installer.description')}</p>
            </div>
          </div>

          <div className="space-y-4">
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('deployment.installer.deviceName')}
              </label>
              <input
                value={deviceName}
                onChange={(event) => setDeviceName(event.target.value)}
                placeholder={t('deployment.installer.devicePlaceholder')}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('deployment.installer.groupName')}
              </label>
              <input
                value={groupName}
                onChange={(event) => setGroupName(event.target.value)}
                placeholder={t('deployment.installer.groupPlaceholder')}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>

            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('deployment.installer.enrollmentKey')}
              </label>
              <input
                value={enrollmentKey}
                onChange={(event) => setEnrollmentKey(event.target.value)}
                placeholder={t('deployment.installer.enrollmentPlaceholder')}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>

            {installerError && (
              <InlineError message={installerError} />
            )}

            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={handleDownloadInstaller}
                disabled={installerLoading}
                className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {installerLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
                {installerLoading ? t('deployment.installer.downloading') : t('deployment.installer.download')}
              </button>
              <a
                href="https://wixtoolset.org/"
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center gap-2 text-xs text-muted-foreground hover:text-primary"
              >
                {t('deployment.installer.wix')}
                <ExternalLink className="h-3 w-3" />
              </a>
            </div>

            <InstructionList
              title={t('deployment.installer.prerequisites')}
              items={[
                t('deployment.installer.stepPrepare'),
                t('deployment.installer.stepCopy', undefined, 'Place AgentHost.exe under Server.Api/AgentInstallers'),
                t('deployment.installer.stepConfig'),
              ]}
            />
          </div>
        </div>

        <div className="rounded-xl border border-border bg-card p-6 shadow-sm">
          <div className="mb-4 flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 text-primary">
              <Shield className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-lg font-semibold text-foreground">{t('deployment.gpo.title')}</h2>
              <p className="text-sm text-muted-foreground">{t('deployment.gpo.description')}</p>
            </div>
          </div>

          <ActiveDirectoryStatus status={adStatus} error={adStatusError} />
          <ActiveDirectoryInfo info={adInfo} error={adInfoError} locale={locale} />

          <div className="mt-4 space-y-4">
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">
                {t('deployment.gpo.domain')}
              </label>
              <input
                value={domainName}
                onChange={(event) => setAdInfo((prev) => (prev ? { ...prev, name: event.target.value } : null))}
                placeholder="contoso.local"
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>

            {gpoError && <InlineError message={gpoError} />}

            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={handleDownloadGpo}
                disabled={gpoLoading}
                className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {gpoLoading ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
                {gpoLoading ? t('deployment.gpo.downloading') : t('deployment.gpo.download')}
              </button>
            </div>

            <InstructionList
              title={t('deployment.gpo.steps')}
              items={[
                t('deployment.gpo.stepCopy'),
                t('deployment.gpo.stepConsole'),
                t('deployment.gpo.stepAssignment'),
                t('deployment.gpo.stepLink'),
                t('deployment.gpo.stepVerify'),
              ]}
            />
          </div>
        </div>
      </section>

      <section className="rounded-xl border border-border bg-card p-6 shadow-sm">
        <div className="mb-4 flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Server className="h-5 w-5" />
          </div>
          <div>
            <h2 className="text-lg font-semibold text-foreground">{t('deployment.instructions.title')}</h2>
            <p className="text-sm text-muted-foreground">{t('deployment.instructions.subtitle')}</p>
          </div>
        </div>

        <InstructionList
          title={t('deployment.instructions.sectionAgent')}
          items={[
            t('deployment.instructions.agent1'),
            t('deployment.instructions.agent2'),
            t('deployment.instructions.agent3'),
          ]}
        />

        <InstructionList
          title={t('deployment.instructions.sectionAd')}
          items={[
            t('deployment.instructions.ad1'),
            t('deployment.instructions.ad2'),
            t('deployment.instructions.ad3'),
          ]}
        />
      </section>
    </div>
  );
}

function InstructionList({ title, items }: { title: string; items: string[] }) {
  return (
    <div>
      <h3 className="mb-2 text-sm font-semibold text-foreground">{title}</h3>
      <ul className="space-y-2 text-sm text-muted-foreground">
        {items.map((item) => (
          <li key={item} className="flex items-start gap-2">
            <CheckCircle2 className="mt-0.5 h-4 w-4 text-primary" />
            <span>{item}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}

function InlineError({ message }: { message: string }) {
  return (
    <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
      <AlertCircle className="h-3 w-3" />
      <span>{message}</span>
    </div>
  );
}

function ActiveDirectoryStatus({
  status,
  error,
}: {
  status: { connected: boolean; message: string } | null;
  error: string | null;
}) {
  const { t } = useTranslation();

  if (error) {
    return <InlineError message={error} />;
  }

  if (!status) {
    return null;
  }

  return (
    <div
      className={`mb-4 flex items-center gap-2 rounded-lg border px-3 py-2 text-xs ${
        status.connected
          ? 'border-emerald-500/40 bg-emerald-500/10 text-emerald-600'
          : 'border-destructive/40 bg-destructive/10 text-destructive'
      }`}
    >
      <Shield className="h-3 w-3" />
      <span>{status.connected ? status.message : t('deployment.ad.connectionError')}</span>
    </div>
  );
}

function ActiveDirectoryInfo({
  info,
  error,
  locale,
}: {
  info: ADDomainInfo | null;
  error: string | null;
  locale: string;
}) {
  const { t } = useTranslation();
  if (error) {
    return <InlineError message={error} />;
  }

  if (!info) {
    return (
      <p className="mb-4 text-xs text-muted-foreground">{t('deployment.ad.noInfo')}</p>
    );
  }

  return (
    <div className="mb-4 space-y-3 rounded-lg border border-border/60 bg-secondary/30 p-4">
      <div className="grid gap-2 text-xs text-muted-foreground md:grid-cols-2">
        <InfoRow label={t('deployment.ad.domainName')} value={info.name} />
        <InfoRow label={t('deployment.ad.forestName')} value={info.forestName} />
        <InfoRow label={t('deployment.ad.domainMode')} value={info.domainMode} />
        <InfoRow label={t('deployment.ad.pdcRole')} value={info.pdcRoleOwner} />
        <InfoRow label={t('deployment.ad.ridRole')} value={info.ridRoleOwner} />
        <InfoRow label={t('deployment.ad.infrastructureRole')} value={info.infrastructureRoleOwner} />
      </div>
      <div>
        <p className="mb-1 text-xs font-semibold text-foreground">{t('deployment.ad.controllers')}</p>
        <ul className="list-disc space-y-1 pl-4 text-xs text-muted-foreground">
          {info.domainControllers?.length ? (
            info.domainControllers.map((controller) => <li key={controller}>{controller}</li>)
          ) : (
            <li>{t('deployment.ad.noControllers')}</li>
          )}
        </ul>
      </div>
      <p className="text-[11px] text-muted-foreground">
        {t('deployment.ad.generatedAt', undefined, `Generated ${formatDateTime(new Date().toISOString(), locale)}`)}
      </p>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="flex items-center justify-between gap-2">
      <span className="font-medium text-foreground/80">{label}</span>
      <span>{value ?? '-'}</span>
    </div>
  );
}

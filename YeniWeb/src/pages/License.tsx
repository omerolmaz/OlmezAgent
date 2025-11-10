import { useTranslation } from '../hooks/useTranslation';

export default function License() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6 p-6">
      <h1 className="text-2xl font-semibold">{t('license.title')}</h1>
      <p className="text-sm text-muted-foreground">{t('license.description')}</p>
      <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
        <dl className="grid gap-4 md:grid-cols-2">
          <InfoRow label={t('license.edition')} value={t('license.enterprise')} />
          <InfoRow label={t('license.allowed')} value={t('license.unlimited')} />
          <InfoRow label={t('license.used')} value={t('license.unknown')} />
          <InfoRow label={t('license.expiry')} value={t('license.notConfigured')} />
        </dl>
      </div>
      <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
        <h2 className="text-sm font-semibold">{t('license.uploadTitle')}</h2>
        <p className="text-xs text-muted-foreground">{t('license.uploadHint')}</p>
      </div>
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase text-muted-foreground">{label}</dt>
      <dd className="text-sm font-medium text-foreground">{value}</dd>
    </div>
  );
}

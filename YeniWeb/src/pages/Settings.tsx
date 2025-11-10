import { useTranslation } from '../hooks/useTranslation';

export default function Settings() {
  const { t } = useTranslation();

  return (
    <div className="space-y-6 p-6">
      <h1 className="text-2xl font-semibold">{t('settingsPage.title')}</h1>
      <p className="text-sm text-muted-foreground">{t('settingsPage.description')}</p>
      <div className="grid gap-4 md:grid-cols-2">
        <Section title={t('settingsPage.sections.general')} hint={t('settingsPage.sections.generalHint')} />
        <Section title={t('settingsPage.sections.security')} hint={t('settingsPage.sections.securityHint')} />
        <Section title={t('settingsPage.sections.integrations')} hint={t('settingsPage.sections.integrationsHint')} />
        <Section title={t('settingsPage.sections.agent')} hint={t('settingsPage.sections.agentHint')} />
      </div>
    </div>
  );
}

function Section({ title, hint }: { title: string; hint: string }) {
  return (
    <div className="rounded-xl border border-border bg-card p-4 shadow-sm">
      <h2 className="text-sm font-semibold">{title}</h2>
      <p className="text-xs text-muted-foreground">{hint}</p>
    </div>
  );
}

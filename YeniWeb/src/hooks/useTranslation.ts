import { useCallback } from 'react';
import { translations, fallbackLanguage, type LanguageCode } from '../i18n/translations';
import { useUiStore } from '../stores/uiStore';

type TranslationValues = Record<string, string | number | undefined>;

function resolveKey(tree: unknown, path: string[]): unknown {
  return path.reduce<unknown>((acc, key) => {
    if (acc && typeof acc === 'object' && key in (acc as Record<string, unknown>)) {
      return (acc as Record<string, unknown>)[key];
    }
    return undefined;
  }, tree);
}

function format(template: string, values?: TranslationValues) {
  if (!values) return template;
  return template.replace(/{{(.*?)}}/g, (_, key) => {
    const value = values[key.trim()];
    return value !== undefined ? String(value) : '';
  });
}

export function useTranslation() {
  const language = useUiStore((state) => state.language);
  const setLanguage = useUiStore((state) => state.setLanguage);
  const toggleLanguage = useUiStore((state) => state.toggleLanguage);

  const t = useCallback(
    (key: string, values?: TranslationValues, fallback?: string) => {
      const segments = key.split('.');
      const primary = resolveKey(translations[language], segments);
      if (typeof primary === 'string') {
        return format(primary, values);
      }

      const backup = resolveKey(translations[fallbackLanguage], segments);
      if (typeof backup === 'string') {
        return format(backup, values);
      }

      return fallback ?? key;
    },
    [language],
  );

  return {
    t,
    language: language as LanguageCode,
    setLanguage,
    toggleLanguage,
  };
}

export default useTranslation;

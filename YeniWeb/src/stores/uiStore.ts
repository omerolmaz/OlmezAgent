import { create } from 'zustand';
import type { LanguageCode } from '../i18n/translations';

interface UiState {
  language: LanguageCode;
  setLanguage: (language: LanguageCode) => void;
  toggleLanguage: () => void;
}

export const useUiStore = create<UiState>((set, get) => ({
  language: 'tr',
  setLanguage: (language) => set({ language }),
  toggleLanguage: () => {
    const nextLanguage: LanguageCode = get().language === 'tr' ? 'en' : 'tr';
    set({ language: nextLanguage });
  },
}));

export default useUiStore;

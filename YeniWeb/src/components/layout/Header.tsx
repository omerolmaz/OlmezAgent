import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BellOff,
  ChevronDown,
  LogOut,
  Moon,
  Search,
  Settings,
  User,
} from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { useTranslation } from '../../hooks/useTranslation';

export default function Header() {
  const [showUserMenu, setShowUserMenu] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const { user, logout } = useAuthStore();
  const navigate = useNavigate();
  const { t, toggleLanguage } = useTranslation();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <header className="flex h-16 items-center justify-between border-b border-border bg-card px-6">
      <div className="flex-1 max-w-2xl">
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-foreground/40" />
          <input
            type="text"
            value={searchQuery}
            onChange={(event) => setSearchQuery(event.target.value)}
            placeholder={t('common.searchPlaceholder')}
            className="w-full rounded-lg border border-border bg-secondary px-10 py-2 text-sm text-foreground placeholder:text-foreground/50 focus:outline-none focus:ring-2 focus:ring-primary"
          />
        </div>
      </div>

      <div className="flex items-center gap-4">
        <button
          className="rounded-lg p-2 transition-colors hover:bg-secondary"
          title={t('common.toggleTheme')}
        >
          <Moon className="h-5 w-5 text-foreground/70" />
        </button>

        <button
          onClick={toggleLanguage}
          className="rounded-lg border border-border bg-secondary/60 px-3 py-2 text-xs font-medium text-foreground transition hover:bg-secondary"
        >
          {t('common.languageToggle')}
        </button>

        <div
          className="flex items-center gap-2 rounded-lg border border-border bg-secondary/60 px-3 py-2 text-xs text-muted-foreground"
          title={t('common.notifications')}
        >
          <BellOff className="h-4 w-4" />
          <span>{t('common.noNotifications')}</span>
        </div>

        <div className="relative">
          <button
            onClick={() => setShowUserMenu((prev) => !prev)}
            className="flex items-center gap-3 rounded-lg px-3 py-2 transition-colors hover:bg-secondary"
          >
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-primary-foreground">
              <User className="h-5 w-5" />
            </div>
            <div className="hidden text-left md:block">
              <p className="text-sm font-medium text-foreground">{user?.username ?? 'Administrator'}</p>
              <p className="text-xs text-muted-foreground">{user?.rights ?? t('common.fullAccess')}</p>
            </div>
            <ChevronDown className="h-4 w-4 text-foreground/60" />
          </button>

          {showUserMenu && (
            <>
              <div className="fixed inset-0 z-40" onClick={() => setShowUserMenu(false)} />
              <div className="absolute right-0 mt-2 w-56 rounded-xl border border-border bg-card shadow-xl z-50">
                <div className="border-b border-border p-4">
                  <p className="text-sm font-semibold text-foreground">{user?.username ?? 'Administrator'}</p>
                  <p className="text-xs text-muted-foreground">{user?.email ?? t('common.noData')}</p>
                </div>
                <div className="py-2">
                  <button
                    onClick={() => {
                      navigate('/settings');
                      setShowUserMenu(false);
                    }}
                    className="flex w-full items-center gap-3 px-4 py-2 text-left text-sm text-foreground transition-colors hover:bg-secondary"
                  >
                    <Settings className="h-4 w-4 text-foreground/70" />
                    {t('common.settings')}
                  </button>
                  <button
                    onClick={() => {
                      navigate('/users');
                      setShowUserMenu(false);
                    }}
                    className="flex w-full items-center gap-3 px-4 py-2 text-left text-sm text-foreground transition-colors hover:bg-secondary"
                  >
                    <User className="h-4 w-4 text-foreground/70" />
                    {t('common.profile')}
                  </button>
                </div>
                <div className="border-t border-border py-2">
                  <button
                    onClick={handleLogout}
                    className="flex w-full items-center gap-3 px-4 py-2 text-left text-sm font-medium text-destructive transition-colors hover:bg-destructive/10"
                  >
                    <LogOut className="h-4 w-4" />
                    {t('common.signOut')}
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </header>
  );
}

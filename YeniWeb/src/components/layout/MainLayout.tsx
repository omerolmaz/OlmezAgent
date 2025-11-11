import type { ReactNode } from 'react';
import { useMemo, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Laptop,
  Terminal,
  Plug,
  FileText,
  Package2,
  Shield,
  Layers3,
  Users,
  Settings,
  BadgeCheck,
  LogOut,
  User,
  Languages,
  Server,
  PanelLeft,
  PanelRight,
} from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { useTranslation } from '../../hooks/useTranslation';

interface NavigationItem {
  id: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  path: string;
}

interface MainLayoutProps {
  children: ReactNode;
}

export default function MainLayout({ children }: MainLayoutProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuthStore();
  const { t, language, toggleLanguage } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  const menuItems = useMemo<NavigationItem[]>(
    () => [
      { id: 'dashboard', label: t('menu.dashboard'), icon: LayoutDashboard, path: '/dashboard' },
      { id: 'devices', label: t('menu.devices'), icon: Laptop, path: '/devices' },
      { id: 'commands', label: t('menu.commands'), icon: Terminal, path: '/commands' },
      { id: 'sessions', label: t('menu.sessions'), icon: Plug, path: '/sessions' },
      { id: 'inventory', label: t('menu.inventory'), icon: Layers3, path: '/inventory' },
      { id: 'security', label: t('menu.security'), icon: Shield, path: '/security' },
      { id: 'reports', label: t('menu.reports'), icon: FileText, path: '/reports' },
      { id: 'bulk', label: t('menu.bulk'), icon: Package2, path: '/bulk-operations' },
      { id: 'users', label: t('menu.users'), icon: Users, path: '/users' },
      { id: 'settings', label: t('menu.settings'), icon: Settings, path: '/settings' },
      { id: 'license', label: t('menu.license'), icon: BadgeCheck, path: '/license' },
      { id: 'deployment', label: t('menu.deployment'), icon: Server, path: '/deployment' },
    ],
    [t],
  );

  const isActive = (path: string) => {
    if (path === '/devices') {
      return location.pathname === path || location.pathname.startsWith('/devices/');
    }
    return location.pathname === path || location.pathname.startsWith(`${path}/`);
  };

  const activeTitle = menuItems.find((item) => isActive(item.path))?.label ?? t('menu.brand');

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const toggleSidebar = () => setSidebarCollapsed((prev) => !prev);

  return (
    <div className="flex h-screen bg-background text-foreground overflow-hidden">
      <aside
        className={`flex flex-col border-r border-border bg-card transition-all duration-300 ${
          sidebarCollapsed ? 'w-20' : 'w-64'
        }`}
      >
        <div className="h-16 flex items-center justify-between border-b border-border px-4">
          <button
            type="button"
            onClick={toggleSidebar}
            className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
            aria-label="Toggle sidebar"
          >
            {sidebarCollapsed ? <PanelRight className="h-4 w-4" /> : <PanelLeft className="h-4 w-4" />}
          </button>
          {!sidebarCollapsed && (
            <h1 className="text-xl font-bold text-primary tracking-tight">{t('menu.brand')}</h1>
          )}
        </div>

        <nav className="flex-1 overflow-y-auto px-3 py-4 space-y-2">
          {menuItems.map((item) => {
            const Icon = item.icon;
            const active = isActive(item.path);

            return (
              <button
                key={item.id}
                onClick={() => navigate(item.path)}
                className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                  active
                    ? 'bg-primary text-primary-foreground shadow-sm'
                    : 'text-muted-foreground hover:text-foreground hover:bg-secondary'
                }`}
              >
                <Icon className="w-5 h-5 shrink-0" />
                {!sidebarCollapsed && <span className="truncate">{item.label}</span>}
              </button>
            );
          })}
        </nav>

        <div className="border-t border-border p-4">
          <div className="flex items-center gap-3 mb-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10">
              <User className="h-5 w-5 text-primary" />
            </div>
            {!sidebarCollapsed && (
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold">{user?.username ?? 'Administrator'}</p>
                <p className="truncate text-xs text-muted-foreground">{user?.rights ?? t('common.fullAccess')}</p>
              </div>
            )}
          </div>
          <button
            onClick={handleLogout}
            className="w-full inline-flex items-center justify-center gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm font-medium text-destructive transition hover:bg-destructive hover:text-white"
          >
            <LogOut className="h-4 w-4" />
            {!sidebarCollapsed && t('common.signOut')}
          </button>
        </div>
      </aside>

      <main className="flex-1 flex flex-col overflow-hidden">
        <header className="h-16 bg-card border-b border-border flex items-center justify-between px-6">
          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={toggleSidebar}
              className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70 lg:hidden"
              aria-label="Toggle sidebar"
            >
              {sidebarCollapsed ? <PanelRight className="h-4 w-4" /> : <PanelLeft className="h-4 w-4" />}
            </button>
            <div>
              <p className="text-xs uppercase text-muted-foreground">{t('menu.brand')}</p>
              <h2 className="text-xl font-semibold tracking-tight">{activeTitle}</h2>
            </div>
          </div>
          <div className="flex items-center gap-3 text-sm text-muted-foreground">
            <button
              type="button"
              onClick={toggleLanguage}
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-secondary/60 px-3 py-2 text-xs font-medium text-foreground transition hover:bg-secondary/80"
              aria-label={t('common.languageToggle')}
            >
              <Languages className="h-4 w-4 text-primary" />
              {t('common.languageToggle')}
            </button>
            {new Date().toLocaleDateString(locale, {
              weekday: 'long',
              year: 'numeric',
              month: 'short',
              day: 'numeric',
            })}
          </div>
        </header>

        <section className="flex-1 overflow-auto bg-muted/10">{children}</section>
      </main>
    </div>
  );
}


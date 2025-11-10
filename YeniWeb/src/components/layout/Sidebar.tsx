import { useMemo, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
  Laptop,
  Terminal,
  Plug,
  Package,
  Users,
  Shield,
  Archive,
  FileText,
  Settings,
  FileCode,
  Server,
  ChevronLeft,
  ChevronRight,
} from 'lucide-react';
import { useTranslation } from '../../hooks/useTranslation';

interface NavItem {
  id: string;
  label: string;
  icon: React.ElementType;
  path: string;
  badge?: number;
}

export default function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const { t } = useTranslation();

  const navItems = useMemo<NavItem[]>(
    () => [
      { id: 'dashboard', label: t('menu.dashboard'), icon: LayoutDashboard, path: '/dashboard' },
      { id: 'devices', label: t('menu.devices'), icon: Laptop, path: '/devices' },
      { id: 'commands', label: t('menu.commands'), icon: Terminal, path: '/commands' },
      { id: 'sessions', label: t('menu.sessions'), icon: Plug, path: '/sessions' },
      { id: 'inventory', label: t('menu.inventory'), icon: Archive, path: '/inventory' },
      { id: 'security', label: t('menu.security'), icon: Shield, path: '/security' },
      { id: 'reports', label: t('menu.reports'), icon: FileText, path: '/reports' },
      { id: 'bulk', label: t('menu.bulk'), icon: Package, path: '/bulk-operations' },
      { id: 'users', label: t('menu.users'), icon: Users, path: '/users' },
      { id: 'settings', label: t('menu.settings'), icon: Settings, path: '/settings' },
      { id: 'license', label: t('menu.license'), icon: FileCode, path: '/license' },
      { id: 'deployment', label: t('menu.deployment'), icon: Server, path: '/deployment' },
    ],
    [t],
  );

  const handleNavigation = (path: string) => {
    navigate(path);
  };

  return (
    <aside
      className={`bg-card border-r border-border transition-all duration-300 flex flex-col ${
        collapsed ? 'w-20' : 'w-72'
      }`}
    >
      {/* Logo */}
      <div className="h-16 flex items-center justify-between px-4 border-b border-border">
        {!collapsed && (
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary">
              <svg className="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 24 24">
                <path d="M12 2L2 7v10c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V7l-10-5z"/>
              </svg>
            </div>
            <div>
              <h1 className="font-bold text-lg text-foreground">{t('menu.brand')}</h1>
            </div>
          </div>
        )}
        <button
          onClick={() => setCollapsed(!collapsed)}
          className="p-2 hover:bg-secondary rounded-lg transition-colors"
          title={collapsed ? 'Expand' : 'Collapse'}
        >
          {collapsed ? (
            <ChevronRight className="w-5 h-5 text-foreground" />
          ) : (
            <ChevronLeft className="w-5 h-5 text-foreground" />
          )}
        </button>
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto scrollbar-thin py-4 px-2">
        <div className="space-y-1">
          {navItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.path || location.pathname.startsWith(item.path + '/');
            
            return (
              <button
                key={item.id}
                onClick={() => handleNavigation(item.path)}
                className={`w-full flex items-center gap-3 px-3 py-3 rounded-lg transition-all group relative ${
                  isActive
                    ? 'bg-primary text-white'
                    : 'text-foreground/70 hover:bg-secondary hover:text-foreground'
                }`}
                title={collapsed ? item.label : undefined}
              >
                <Icon className="w-5 h-5 flex-shrink-0" />
                {!collapsed && (
                  <span className="text-sm font-medium truncate">{item.label}</span>
                )}
                {!collapsed && item.badge && (
                  <span className="ml-auto bg-error text-white text-xs font-bold px-2 py-0.5 rounded-full">
                    {item.badge}
                  </span>
                )}
                
                {/* Tooltip for collapsed state */}
                {collapsed && (
                  <div className="absolute left-full ml-2 px-3 py-2 bg-secondary border border-border rounded-lg opacity-0 group-hover:opacity-100 pointer-events-none transition-opacity whitespace-nowrap z-50">
                    <span className="text-sm text-foreground">{item.label}</span>
                  </div>
                )}
              </button>
            );
          })}
        </div>
      </nav>
    </aside>
  );
}

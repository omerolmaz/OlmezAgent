import React, { useState, useEffect } from 'react';
import { RefreshCw, Download, Trash2, Package, Loader2, FileDown } from 'lucide-react';
import { inventoryService } from '../../services/inventory.service';
import type { InstalledSoftware } from '../../types/device.types';
import InstallSoftwareModal from './InstallSoftwareModal';
import UninstallSoftwareModal from './UninstallSoftwareModal';
import { useAuthStore } from '../../stores/authStore';

interface SoftwareWithActions extends InstalledSoftware {
  id: string;
}

interface SoftwareTabProps {
  deviceId: string;
}

const SoftwareTab: React.FC<SoftwareTabProps> = ({ deviceId }) => {
  const { user } = useAuthStore();
  const [software, setSoftware] = useState<SoftwareWithActions[]>([]);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [showInstallModal, setShowInstallModal] = useState(false);
  const [showUninstallModal, setShowUninstallModal] = useState(false);
  const [selectedSoftware, setSelectedSoftware] = useState<SoftwareWithActions | null>(null);
  const [searchTerm, setSearchTerm] = useState('');

  useEffect(() => {
    loadSoftware();
  }, [deviceId]);

  const loadSoftware = async () => {
    setLoading(true);
    try {
      const data = await inventoryService.getInstalledSoftware(deviceId);
      // Map to add id field based on name+version
      const mapped = (data || []).map(item => ({
        ...item,
        id: `${item.name}-${item.version || 'unknown'}`,
      }));
      setSoftware(mapped);
    } catch (error) {
      console.error('Error loading software:', error);
      setSoftware([]);
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = async () => {
    if (!user?.id) return;
    setRefreshing(true);
    try {
      await inventoryService.refreshInventory(deviceId, user.id);
      // Wait 2 seconds then reload
      setTimeout(loadSoftware, 2000);
    } catch (error) {
      console.error('Error refreshing software:', error);
    } finally {
      setRefreshing(false);
    }
  };

  const handleUninstall = (software: SoftwareWithActions) => {
    setSelectedSoftware(software);
    setShowUninstallModal(true);
  };

  const handleInstallSuccess = () => {
    setShowInstallModal(false);
    setTimeout(loadSoftware, 2000);
  };

  const handleUninstallSuccess = () => {
    setShowUninstallModal(false);
    setTimeout(loadSoftware, 2000);
  };

  const filteredSoftware = software.filter(s =>
    s.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    s.publisher?.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const formatBytes = (bytes?: number) => {
    if (!bytes) return '-';
    const mb = bytes / (1024 * 1024);
    if (mb < 1024) return `${mb.toFixed(2)} MB`;
    return `${(mb / 1024).toFixed(2)} GB`;
  };

  const exportToCSV = () => {
    const headers = ['Yazılım Adı', 'Versiyon', 'Yayıncı', 'Boyut', 'Kurulum Tarihi'];
    const rows = filteredSoftware.map(item => [
      item.name,
      item.version || '-',
      item.publisher || '-',
      formatBytes(item.sizeInBytes),
      item.installDate ? new Date(item.installDate).toLocaleDateString('tr-TR') : '-'
    ]);
    
    const csvContent = [
      headers.join(','),
      ...rows.map(row => row.map(cell => `"${cell}"`).join(','))
    ].join('\n');
    
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', `software-${deviceId}-${new Date().toISOString().split('T')[0]}.csv`);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="space-y-4">
      {/* Toolbar */}
      <div className="flex items-center justify-between gap-4">
        <div className="flex-1 max-w-md">
          <input
            type="text"
            placeholder="Yazılım ara..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full px-4 py-2 border border-border rounded-lg bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-primary transition"
          />
        </div>
        <div className="flex gap-3">
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            className="flex items-center px-4 py-2 text-sm font-medium border border-border rounded-lg bg-card text-foreground hover:bg-secondary/60 disabled:opacity-50 transition"
          >
            <RefreshCw className={`w-4 h-4 mr-2 ${refreshing ? 'animate-spin' : ''}`} />
            Yenile
          </button>
          <button
            onClick={exportToCSV}
            disabled={filteredSoftware.length === 0}
            className="flex items-center px-4 py-2 text-sm font-medium border border-border rounded-lg bg-card text-foreground hover:bg-secondary/60 disabled:opacity-50 transition"
          >
            <FileDown className="w-4 h-4 mr-2" />
            Export
          </button>
          <button
            onClick={() => setShowInstallModal(true)}
            className="flex items-center px-4 py-2 text-sm font-medium rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 transition"
          >
            <Download className="w-4 h-4 mr-2" />
            Yazılım Kur
          </button>
        </div>
      </div>

      {/* Software Table */}
      <div className="overflow-hidden rounded-xl border border-border bg-card">
        {loading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-8 h-8 animate-spin text-primary" />
          </div>
        ) : filteredSoftware.length === 0 ? (
          <div className="text-center py-12">
            <Package className="w-12 h-12 text-muted-foreground mx-auto mb-3" />
            <p className="text-muted-foreground">Kurulu yazılım bulunamadı</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-border text-sm">
              <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 text-left">Yazılım Adı</th>
                  <th className="px-4 py-3 text-left">Versiyon</th>
                  <th className="px-4 py-3 text-left">Yayıncı</th>
                  <th className="px-4 py-3 text-left">Boyut</th>
                  <th className="px-4 py-3 text-left">Kurulum Tarihi</th>
                  <th className="px-4 py-3 text-right">İşlemler</th>
                </tr>
              </thead>
              <tbody>
                {filteredSoftware.map((item) => (
                  <tr key={item.id} className="border-t border-border/70 bg-card transition hover:bg-secondary/40">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <Package className="w-4 h-4 text-muted-foreground" />
                        <span className="font-medium text-foreground">{item.name}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{item.version || '-'}</td>
                    <td className="px-4 py-3 text-muted-foreground">{item.publisher || '-'}</td>
                    <td className="px-4 py-3 text-muted-foreground">{formatBytes(item.sizeInBytes)}</td>
                    <td className="px-4 py-3 text-muted-foreground">{item.installDate ? new Date(item.installDate).toLocaleDateString('tr-TR') : '-'}</td>
                    <td className="px-4 py-3 text-right">
                      {item.uninstallString && (
                        <button
                          onClick={() => handleUninstall(item)}
                          className="inline-flex items-center justify-center p-1.5 rounded-lg text-destructive hover:bg-destructive/10 transition"
                          title="Kaldır"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Modals */}
      {showInstallModal && (
        <InstallSoftwareModal
          deviceId={deviceId}
          onClose={() => setShowInstallModal(false)}
          onSuccess={handleInstallSuccess}
        />
      )}

      {showUninstallModal && selectedSoftware && (
        <UninstallSoftwareModal
          deviceId={deviceId}
          software={selectedSoftware}
          onClose={() => setShowUninstallModal(false)}
          onSuccess={handleUninstallSuccess}
        />
      )}
    </div>
  );
};

export default SoftwareTab;


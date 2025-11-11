import React, { useState, useEffect } from 'react';
import { RefreshCw, Download, Trash2, Package, Loader2 } from 'lucide-react';
import InstallSoftwareModal from './InstallSoftwareModal';
import UninstallSoftwareModal from './UninstallSoftwareModal';

interface InstalledSoftware {
  id: string;
  name: string;
  version?: string;
  publisher?: string;
  installDate?: string;
  uninstallString?: string;
  size?: number;
}

interface PendingAction {
  id: string;
  actionType: string;
  status: string;
  details: string;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  output?: string;
  error?: string;
}

interface SoftwareTabProps {
  deviceId: string;
}

const SoftwareTab: React.FC<SoftwareTabProps> = ({ deviceId }) => {
  const [software, setSoftware] = useState<InstalledSoftware[]>([]);
  const [pendingActions, setPendingActions] = useState<PendingAction[]>([]);
  const [loading, setLoading] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [showInstallModal, setShowInstallModal] = useState(false);
  const [showUninstallModal, setShowUninstallModal] = useState(false);
  const [selectedSoftware, setSelectedSoftware] = useState<InstalledSoftware | null>(null);
  const [searchTerm, setSearchTerm] = useState('');

  useEffect(() => {
    loadSoftware();
    loadPendingActions();
    
    // Poll pending actions every 5 seconds
    const interval = setInterval(loadPendingActions, 5000);
    return () => clearInterval(interval);
  }, [deviceId]);

  const loadSoftware = async () => {
    setLoading(true);
    try {
      const response = await fetch(`/api/software/${deviceId}`, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
      });
      if (response.ok) {
        const data = await response.json();
        setSoftware(data);
      }
    } catch (error) {
      console.error('Error loading software:', error);
    } finally {
      setLoading(false);
    }
  };

  const loadPendingActions = async () => {
    try {
      const response = await fetch(`/api/software/${deviceId}/pending-actions`, {
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
      });
      if (response.ok) {
        const data = await response.json();
        setPendingActions(data);
      }
    } catch (error) {
      console.error('Error loading pending actions:', error);
    }
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    try {
      const response = await fetch(`/api/software/${deviceId}/refresh`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        }
      });
      if (response.ok) {
        // Wait 2 seconds then reload
        setTimeout(loadSoftware, 2000);
      }
    } catch (error) {
      console.error('Error refreshing software:', error);
    } finally {
      setRefreshing(false);
    }
  };

  const handleUninstall = (software: InstalledSoftware) => {
    setSelectedSoftware(software);
    setShowUninstallModal(true);
  };

  const handleInstallSuccess = () => {
    setShowInstallModal(false);
    loadPendingActions();
  };

  const handleUninstallSuccess = () => {
    setShowUninstallModal(false);
    loadPendingActions();
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

  const getStatusBadge = (status: string) => {
    const colors: Record<string, string> = {
      'Pending': 'bg-yellow-100 text-yellow-800',
      'Running': 'bg-blue-100 text-blue-800',
      'Completed': 'bg-green-100 text-green-800',
      'Failed': 'bg-red-100 text-red-800',
      'Timeout': 'bg-orange-100 text-orange-800',
      'Cancelled': 'bg-gray-100 text-gray-800'
    };
    return colors[status] || 'bg-gray-100 text-gray-800';
  };

  return (
    <div className="space-y-6">
      {/* Pending Actions */}
      {pendingActions.length > 0 && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
          <h3 className="text-lg font-semibold text-blue-900 mb-3 flex items-center">
            <Loader2 className="w-5 h-5 mr-2 animate-spin" />
            Bekleyen İşlemler ({pendingActions.length})
          </h3>
          <div className="space-y-2">
            {pendingActions.map(action => (
              <div key={action.id} className="bg-white rounded p-3 flex items-center justify-between">
                <div className="flex-1">
                  <div className="flex items-center space-x-2">
                    <span className={`px-2 py-1 rounded text-xs font-medium ${getStatusBadge(action.status)}`}>
                      {action.status}
                    </span>
                    <span className="text-sm font-medium text-gray-900">
                      {action.actionType}
                    </span>
                  </div>
                  {action.error && (
                    <p className="text-xs text-red-600 mt-1">{action.error}</p>
                  )}
                </div>
                <span className="text-xs text-gray-500">
                  {new Date(action.createdAt).toLocaleString('tr-TR')}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Toolbar */}
      <div className="flex items-center justify-between">
        <div className="flex-1 max-w-md">
          <input
            type="text"
            placeholder="Yazılım ara..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
          />
        </div>
        <div className="flex space-x-2">
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg flex items-center space-x-2 disabled:opacity-50"
          >
            <RefreshCw className={`w-4 h-4 ${refreshing ? 'animate-spin' : ''}`} />
            <span>Yenile</span>
          </button>
          <button
            onClick={() => setShowInstallModal(true)}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg flex items-center space-x-2"
          >
            <Download className="w-4 h-4" />
            <span>Yazılım Kur</span>
          </button>
        </div>
      </div>

      {/* Software Table */}
      <div className="bg-white rounded-lg shadow overflow-hidden">
        {loading ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
          </div>
        ) : filteredSoftware.length === 0 ? (
          <div className="text-center py-12">
            <Package className="w-12 h-12 text-gray-400 mx-auto mb-3" />
            <p className="text-gray-500">Kurulu yazılım bulunamadı</p>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Yazılım Adı
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Versiyon
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Yayıncı
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Boyut
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Kurulum Tarihi
                </th>
                <th className="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
                  İşlemler
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {filteredSoftware.map((item) => (
                <tr key={item.id} className="hover:bg-gray-50">
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-medium text-gray-900">{item.name}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm text-gray-500">{item.version || '-'}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm text-gray-500">{item.publisher || '-'}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm text-gray-500">{formatBytes(item.size)}</div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm text-gray-500">
                      {item.installDate ? new Date(item.installDate).toLocaleDateString('tr-TR') : '-'}
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                    {item.uninstallString && (
                      <button
                        onClick={() => handleUninstall(item)}
                        className="text-red-600 hover:text-red-900 inline-flex items-center space-x-1"
                      >
                        <Trash2 className="w-4 h-4" />
                        <span>Kaldır</span>
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
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

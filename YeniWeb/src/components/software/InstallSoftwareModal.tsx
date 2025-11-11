import React, { useState } from 'react';
import { X, Upload, Package, Search, Loader2 } from 'lucide-react';

interface InstallSoftwareModalProps {
  deviceId: string;
  onClose: () => void;
  onSuccess: () => void;
}

type TabType = 'local' | 'chocolatey';

interface ChocoPackage {
  name: string;
  version: string;
  description: string;
}

const InstallSoftwareModal: React.FC<InstallSoftwareModalProps> = ({ deviceId, onClose, onSuccess }) => {
  const [activeTab, setActiveTab] = useState<TabType>('local');
  const [loading, setLoading] = useState(false);
  
  // Local file state
  const [filePath, setFilePath] = useState('');
  const [arguments_, setArguments] = useState('');
  const [timeout, setTimeout] = useState(1800);
  
  // Chocolatey state
  const [packageName, setPackageName] = useState('');
  const [packageVersion, setPackageVersion] = useState('');
  const [searchResults, setSearchResults] = useState<ChocoPackage[]>([]);
  const [searching, setSearching] = useState(false);

  const handleLocalInstall = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    try {
      const response = await fetch(`/api/software/${deviceId}/install`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({
          filePath,
          arguments: arguments_,
          timeout,
          runAsUser: false
        })
      });

      if (response.ok) {
        onSuccess();
      } else {
        const error = await response.json();
        alert(`Hata: ${error.message || 'Kurulum başlatılamadı'}`);
      }
    } catch (error) {
      console.error('Install error:', error);
      alert('Kurulum sırasında bir hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  const handleChocoInstall = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    try {
      const response = await fetch(`/api/software/${deviceId}/install-choco`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({
          packageName,
          version: packageVersion || null,
          force: true
        })
      });

      if (response.ok) {
        onSuccess();
      } else {
        const error = await response.json();
        alert(`Hata: ${error.message || 'Chocolatey kurulumu başlatılamadı'}`);
      }
    } catch (error) {
      console.error('Chocolatey install error:', error);
      alert('Chocolatey kurulumu sırasında bir hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  const handleSearchChoco = async () => {
    if (!packageName.trim()) return;
    
    setSearching(true);
    try {
      // Simulated search - gerçek implementasyonda Chocolatey API kullanılabilir
      const mockResults: ChocoPackage[] = [
        { name: packageName, version: 'latest', description: `${packageName} paketi` },
        { name: `${packageName}-portable`, version: 'latest', description: `${packageName} portable sürüm` }
      ];
      setSearchResults(mockResults);
    } catch (error) {
      console.error('Search error:', error);
    } finally {
      setSearching(false);
    }
  };

  const selectPackage = (pkg: ChocoPackage) => {
    setPackageName(pkg.name);
    setPackageVersion(pkg.version === 'latest' ? '' : pkg.version);
    setSearchResults([]);
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-2xl max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b">
          <h2 className="text-xl font-semibold text-gray-900">Yazılım Kurulumu</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 transition-colors"
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Tabs */}
        <div className="flex border-b">
          <button
            onClick={() => setActiveTab('local')}
            className={`flex-1 px-6 py-3 font-medium transition-colors ${
              activeTab === 'local'
                ? 'text-blue-600 border-b-2 border-blue-600'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            <div className="flex items-center justify-center space-x-2">
              <Upload className="w-4 h-4" />
              <span>Yerel Dosya</span>
            </div>
          </button>
          <button
            onClick={() => setActiveTab('chocolatey')}
            className={`flex-1 px-6 py-3 font-medium transition-colors ${
              activeTab === 'chocolatey'
                ? 'text-blue-600 border-b-2 border-blue-600'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            <div className="flex items-center justify-center space-x-2">
              <Package className="w-4 h-4" />
              <span>Chocolatey</span>
            </div>
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto max-h-[60vh]">
          {activeTab === 'local' ? (
            <form onSubmit={handleLocalInstall} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Dosya Yolu <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={filePath}
                  onChange={(e) => setFilePath(e.target.value)}
                  placeholder="C:\Path\To\Setup.exe veya C:\Path\To\Setup.msi"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                  required
                />
                <p className="mt-1 text-xs text-gray-500">
                  Agent bilgisayarındaki tam dosya yolu (MSI veya EXE)
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Kurulum Argümanları
                </label>
                <input
                  type="text"
                  value={arguments_}
                  onChange={(e) => setArguments(e.target.value)}
                  placeholder="/S /silent veya /qn /norestart"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Boş bırakılırsa MSI için "/qn /norestart", EXE için "/S /silent" kullanılır
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Zaman Aşımı (saniye)
                </label>
                <input
                  type="number"
                  value={timeout}
                  onChange={(e) => setTimeout(parseInt(e.target.value))}
                  min="60"
                  max="3600"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Kurulum için maksimum bekleme süresi (varsayılan: 1800 saniye / 30 dakika)
                </p>
              </div>

              <div className="flex justify-end space-x-3 pt-4">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors"
                  disabled={loading}
                >
                  İptal
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50 flex items-center space-x-2"
                >
                  {loading && <Loader2 className="w-4 h-4 animate-spin" />}
                  <span>{loading ? 'Kuruluyor...' : 'Kur'}</span>
                </button>
              </div>
            </form>
          ) : (
            <form onSubmit={handleChocoInstall} className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Paket Adı <span className="text-red-500">*</span>
                </label>
                <div className="flex space-x-2">
                  <input
                    type="text"
                    value={packageName}
                    onChange={(e) => setPackageName(e.target.value)}
                    placeholder="googlechrome, firefox, 7zip, vscode..."
                    className="flex-1 px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                    required
                  />
                  <button
                    type="button"
                    onClick={handleSearchChoco}
                    disabled={searching}
                    className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded-lg flex items-center space-x-2 disabled:opacity-50"
                  >
                    {searching ? (
                      <Loader2 className="w-4 h-4 animate-spin" />
                    ) : (
                      <Search className="w-4 h-4" />
                    )}
                  </button>
                </div>
                <p className="mt-1 text-xs text-gray-500">
                  Chocolatey paket adı (örn: googlechrome, firefox, vscode)
                </p>
              </div>

              {searchResults.length > 0 && (
                <div className="border border-gray-200 rounded-lg divide-y">
                  {searchResults.map((pkg) => (
                    <button
                      key={pkg.name}
                      type="button"
                      onClick={() => selectPackage(pkg)}
                      className="w-full px-4 py-3 text-left hover:bg-gray-50 transition-colors"
                    >
                      <div className="font-medium text-gray-900">{pkg.name}</div>
                      <div className="text-sm text-gray-500">{pkg.description}</div>
                      <div className="text-xs text-gray-400 mt-1">Versiyon: {pkg.version}</div>
                    </button>
                  ))}
                </div>
              )}

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Versiyon (Opsiyonel)
                </label>
                <input
                  type="text"
                  value={packageVersion}
                  onChange={(e) => setPackageVersion(e.target.value)}
                  placeholder="Boş bırakılırsa en son versiyon kurulur"
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-transparent"
                />
                <p className="mt-1 text-xs text-gray-500">
                  Belirli bir versiyon kurmak için versiyon numarasını girin (örn: 1.2.3)
                </p>
              </div>

              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
                <p className="text-sm text-yellow-800">
                  <strong>Not:</strong> Chocolatey ilk kez kullanılıyorsa, sistem otomatik olarak Chocolatey'i kuracaktır. 
                  Bu işlem birkaç dakika sürebilir.
                </p>
              </div>

              <div className="flex justify-end space-x-3 pt-4">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition-colors"
                  disabled={loading}
                >
                  İptal
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50 flex items-center space-x-2"
                >
                  {loading && <Loader2 className="w-4 h-4 animate-spin" />}
                  <span>{loading ? 'Kuruluyor...' : 'Chocolatey ile Kur'}</span>
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
};

export default InstallSoftwareModal;

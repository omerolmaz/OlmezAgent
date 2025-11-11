import React, { useState, useEffect } from 'react';
import { X, Upload, Package, Loader2, ExternalLink } from 'lucide-react';

interface InstallSoftwareModalProps {
  deviceId: string;
  onClose: () => void;
  onSuccess: () => void;
}

type TabType = 'url' | 'local' | 'chocolatey';

interface ChocoPackage {
  name: string;
  version: string;
  description: string;
}

const InstallSoftwareModal: React.FC<InstallSoftwareModalProps> = ({ deviceId, onClose, onSuccess }) => {
  const [activeTab, setActiveTab] = useState<TabType>('url');
  const [loading, setLoading] = useState(false);
  
  // URL download state
  const [downloadUrl, setDownloadUrl] = useState('');
  const [urlFileName, setUrlFileName] = useState('');
  const [urlArgs, setUrlArgs] = useState('');
  const [urlTimeout, setUrlTimeout] = useState(1800);
  const [urlRunAsUser, setUrlRunAsUser] = useState(false);
  
  // Local file state
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [installArgs, setInstallArgs] = useState('');
  const [installTimeout, setInstallTimeout] = useState(1800);
  const [installRunAsUser, setInstallRunAsUser] = useState(false);
  
  // Chocolatey state
  const [chocoPackages, setChocoPackages] = useState<ChocoPackage[]>([]);
  const [filteredPackages, setFilteredPackages] = useState<ChocoPackage[]>([]);
  const [searching, setSearching] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');

  // Load Chocolatey packages on mount
  useEffect(() => {
    if (activeTab === 'chocolatey') {
      loadChocoPackages();
    }
  }, [activeTab]);

  // Filter packages based on search term
  useEffect(() => {
    if (searchTerm) {
      const filtered = chocoPackages.filter(pkg => 
        pkg.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        pkg.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
      setFilteredPackages(filtered);
    } else {
      setFilteredPackages(chocoPackages);
    }
  }, [searchTerm, chocoPackages]);

  const loadChocoPackages = async () => {
    try {
      setSearching(true);
      const response = await fetch('/data/chocos.json');
      
      if (response.ok) {
        const data = await response.json();
        // Assuming data is array or object with packages
        const packages = Array.isArray(data) ? data : Object.keys(data).map(name => ({
          name,
          version: 'latest',
          description: data[name]?.description || name
        }));
        setChocoPackages(packages);
        setFilteredPackages(packages);
      }
    } catch (error) {
      console.error('Error loading choco packages:', error);
    } finally {
      setSearching(false);
    }
  };

  // File upload handler
  const handleFileSelect = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      const extension = file.name.toLowerCase();
      if (extension.endsWith('.exe') || extension.endsWith('.msi')) {
        setUploadFile(file);
      } else {
        alert('Lütfen sadece .exe veya .msi dosyası seçin');
      }
    }
  };

  const uploadAndInstall = async () => {
    if (!uploadFile) {
      alert('Lütfen bir dosya seçin');
      return;
    }

    setUploading(true);
    setLoading(true);

    try {
      // 1. Önce dosyayı server'a yükle
      const formData = new FormData();
      formData.append('file', uploadFile);

      const uploadResponse = await fetch('/api/software/upload-file', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: formData
      });

      if (!uploadResponse.ok) {
        const error = await uploadResponse.json();
        throw new Error(error.message || 'Dosya yüklenemedi');
      }

      const uploadResult = await uploadResponse.json();
      const fileId = uploadResult.fileId;

      // 2. Sonra kurulum komutunu gönder
      const installResponse = await fetch(`/api/software/${deviceId}/install`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({
          fileId: fileId,
          fileName: uploadFile.name,
          arguments: installArgs,
          timeout: installTimeout,
          runAsUser: installRunAsUser
        })
      });

      if (!installResponse.ok) {
        const error = await installResponse.json();
        throw new Error(error.message || 'Kurulum başlatılamadı');
      }

      const result = await installResponse.json();
      alert(`Kurulum başlatıldı: ${result.message || 'Başarılı'}`);
      onClose();
      onSuccess();
    } catch (error: any) {
      alert(error.message || 'Kurulum başlatılamadı');
    } finally {
      setUploading(false);
      setLoading(false);
    }
  };

  const installFromUrl = async () => {
    if (!downloadUrl) {
      alert('Lütfen bir URL girin');
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(`/api/software/${deviceId}/install`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({
          downloadUrl: downloadUrl,
          fileName: urlFileName || undefined,
          arguments: urlArgs,
          timeout: urlTimeout,
          runAsUser: urlRunAsUser
        })
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Kurulum başlatılamadı');
      }

      const result = await response.json();
      alert(`Kurulum başlatıldı: ${result.message || 'Başarılı'}`);
      onClose();
      onSuccess();
    } catch (error: any) {
      alert(error.message || 'Kurulum başlatılamadı');
    } finally {
      setLoading(false);
    }
  };

  const handleChocoInstall = async (packageName: string) => {
    if (!packageName) return;
    
    setLoading(true);
    try {
      const response = await fetch(`/api/software/${deviceId}/install-choco`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({
          packageName: packageName,
          version: null,  // Always install latest version
          force: true
        })
      });

      if (response.ok) {
        const result = await response.json();
        alert(result.message || `${packageName} kurulumu başlatıldı`);
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

  const handleSearchChoco = (term: string) => {
    setSearchTerm(term);
  };

  const openChocoPage = (packageName: string) => {
    window.open(`https://chocolatey.org/packages/${packageName}`, '_blank');
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-hidden border border-border">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-border">
          <h2 className="text-xl font-semibold text-foreground">Yazılım Kurulumu</h2>
          <button
            onClick={onClose}
            className="text-muted-foreground hover:text-foreground transition-colors"
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Tabs */}
        <div className="flex border-b border-border">
          <button
            onClick={() => setActiveTab('url')}
            className={`flex-1 px-6 py-3 font-medium transition-colors ${
              activeTab === 'url'
                ? 'text-primary border-b-2 border-primary'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <div className="flex items-center justify-center gap-2">
              <ExternalLink className="w-4 h-4" />
              <span>URL'den İndir</span>
            </div>
          </button>
          <button
            onClick={() => setActiveTab('local')}
            className={`flex-1 px-6 py-3 font-medium transition-colors ${
              activeTab === 'local'
                ? 'text-primary border-b-2 border-primary'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <div className="flex items-center justify-center gap-2">
              <Upload className="w-4 h-4" />
              <span>Yerel Dosya</span>
            </div>
          </button>
          <button
            onClick={() => setActiveTab('chocolatey')}
            className={`flex-1 px-6 py-3 font-medium transition-colors ${
              activeTab === 'chocolatey'
                ? 'text-primary border-b-2 border-primary'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <div className="flex items-center justify-center gap-2">
              <Package className="w-4 h-4" />
              <span>Chocolatey</span>
            </div>
          </button>
        </div>

        {/* Content */}
        <div className="p-6 overflow-y-auto max-h-[60vh]">
          {activeTab === 'url' ? (
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  İndirme URL'i <span className="text-destructive">*</span>
                </label>
                <input
                  type="url"
                  value={downloadUrl}
                  onChange={(e) => setDownloadUrl(e.target.value)}
                  placeholder="https://example.com/software.exe"
                  className="w-full px-3 py-2 bg-background border border-input rounded-lg text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <p className="text-xs text-muted-foreground mt-1">
                  Sadece .exe veya .msi dosyaları desteklenir
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  Dosya Adı (Opsiyonel)
                </label>
                <input
                  type="text"
                  value={urlFileName}
                  onChange={(e) => setUrlFileName(e.target.value)}
                  placeholder="software.exe"
                  className="w-full px-3 py-2 bg-background border border-input rounded-lg text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <p className="text-xs text-muted-foreground mt-1">
                  Boş bırakılırsa URL'den otomatik algılanır
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  Kurulum Parametreleri (Opsiyonel)
                </label>
                <input
                  type="text"
                  value={urlArgs}
                  onChange={(e) => setUrlArgs(e.target.value)}
                  placeholder="/S /silent"
                  className="w-full px-3 py-2 bg-background border border-input rounded-lg text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                />
                <p className="text-xs text-muted-foreground mt-1">
                  Örnek: /S (sessiz kurulum), /VERYSILENT, /NORESTART
                </p>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-sm font-medium text-foreground mb-2">
                    Timeout (saniye)
                  </label>
                  <input
                    type="number"
                    value={urlTimeout}
                    onChange={(e) => setUrlTimeout(parseInt(e.target.value))}
                    className="w-full px-3 py-2 bg-background border border-input rounded-lg text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>
                <div className="flex items-end">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={urlRunAsUser}
                      onChange={(e) => setUrlRunAsUser(e.target.checked)}
                      className="rounded border-input text-primary focus:ring-ring"
                    />
                    <span className="text-sm text-foreground">Kullanıcı olarak çalıştır</span>
                  </label>
                </div>
              </div>

              <button
                onClick={installFromUrl}
                disabled={loading || !downloadUrl}
                className="w-full px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
              >
                {loading ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    <span>İndiriliyor ve Kuruluyor...</span>
                  </>
                ) : (
                  <>
                    <ExternalLink className="w-4 h-4" />
                    <span>İndir ve Kur</span>
                  </>
                )}
              </button>
            </div>
          ) : activeTab === 'local' ? (
            <div className="space-y-4">
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  Dosya Seçin <span className="text-destructive">*</span>
                </label>
                <div className="flex flex-col gap-4">
                  <div className="border-2 border-dashed border-border rounded-lg p-8 text-center hover:border-primary transition-colors">
                    <input
                      type="file"
                      id="fileUpload"
                      accept=".exe,.msi"
                      onChange={handleFileSelect}
                      className="hidden"
                    />
                    <label
                      htmlFor="fileUpload"
                      className="cursor-pointer flex flex-col items-center gap-3"
                    >
                      <Upload className="w-12 h-12 text-muted-foreground" />
                      <div>
                        <p className="text-foreground font-medium">
                          Dosya seçmek için tıklayın
                        </p>
                        <p className="text-sm text-muted-foreground mt-1">
                          Sadece .exe veya .msi dosyaları desteklenir
                        </p>
                      </div>
                    </label>
                  </div>

                  {uploadFile && (
                    <div className="flex items-center gap-3 p-4 bg-secondary/40 border border-border rounded-lg">
                      <Package className="w-5 h-5 text-primary" />
                      <div className="flex-1">
                        <p className="text-sm font-medium text-foreground">{uploadFile.name}</p>
                        <p className="text-xs text-muted-foreground">
                          {(uploadFile.size / 1024 / 1024).toFixed(2)} MB
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => {
                          setUploadFile(null);
                        }}
                        className="text-muted-foreground hover:text-destructive transition-colors"
                      >
                        <X className="w-5 h-5" />
                      </button>
                    </div>
                  )}
                </div>
              </div>

              {/* Installation Arguments */}
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  Kurulum Parametreleri (Opsiyonel)
                </label>
                <input
                  type="text"
                  value={installArgs}
                  onChange={(e) => setInstallArgs(e.target.value)}
                  placeholder="Örn: /S /silent veya /qn /norestart"
                  className="w-full px-3 py-2 border border-border rounded-lg bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-primary transition"
                />
                <p className="mt-1 text-xs text-muted-foreground">
                  MSI için: /qn /norestart | InnoSetup için: /VERYSILENT /NORESTART | NSIS için: /S
                </p>
              </div>

              {/* Timeout */}
              <div>
                <label className="block text-sm font-medium text-foreground mb-2">
                  Zaman Aşımı (saniye)
                </label>
                <input
                  type="number"
                  value={installTimeout}
                  onChange={(e) => setInstallTimeout(parseInt(e.target.value))}
                  min="60"
                  max="3600"
                  className="w-full px-3 py-2 border border-border rounded-lg bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-primary transition"
                />
              </div>

              {/* Run as User */}
              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  id="installRunAsUser"
                  checked={installRunAsUser}
                  onChange={(e) => setInstallRunAsUser(e.target.checked)}
                  className="w-4 h-4 text-primary border-border rounded focus:ring-2 focus:ring-primary transition"
                />
                <label htmlFor="installRunAsUser" className="text-sm text-foreground cursor-pointer">
                  Kullanıcı olarak çalıştır
                </label>
              </div>

              <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-4">
                <p className="text-sm text-blue-700 dark:text-blue-400">
                  <strong>TacticalRMM Yaklaşımı:</strong>
                  <br />• Parametreleri siz belirleyin (her yükleyici farklıdır)
                  <br />• Boş bırakırsanız, installer'ın varsayılan davranışı kullanılır
                  <br />• Registry'deki UninstallString gibi, sizin verdiğiniz komut aynen çalıştırılır
                </p>
              </div>

              <div className="flex justify-end gap-3 pt-4">
                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 border border-border rounded-lg text-muted-foreground hover:text-foreground hover:bg-secondary/60 transition"
                  disabled={uploading}
                >
                  İptal
                </button>
                <button
                  type="button"
                  onClick={uploadAndInstall}
                  disabled={!uploadFile || uploading}
                  className="px-4 py-2 bg-primary text-primary-foreground rounded-lg hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition flex items-center gap-2"
                >
                  {uploading ? (
                    <>
                      <Loader2 className="w-4 h-4 animate-spin" />
                      Yükleniyor...
                    </>
                  ) : (
                    <>
                      <Upload className="w-4 h-4" />
                      Yükle ve Kur
                    </>
                  )}
                </button>
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Search Box */}
              <div>
                <input
                  type="text"
                  value={searchTerm}
                  onChange={(e) => handleSearchChoco(e.target.value)}
                  placeholder="Paket ara... (örn: chrome, firefox, 7zip)"
                  className="w-full px-4 py-2 border border-border rounded-lg bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-primary transition"
                />
              </div>

              {/* Package Table */}
              <div className="border border-border rounded-lg overflow-hidden max-h-[400px] overflow-y-auto">
                {searching ? (
                  <div className="flex items-center justify-center py-12">
                    <Loader2 className="w-8 h-8 animate-spin text-primary" />
                  </div>
                ) : filteredPackages.length === 0 ? (
                  <div className="text-center py-12 text-muted-foreground">
                    {chocoPackages.length === 0 ? 'Paket listesi yükleniyor...' : 'Paket bulunamadı'}
                  </div>
                ) : (
                  <table className="min-w-full divide-y divide-border text-sm">
                    <thead className="bg-secondary/60 sticky top-0">
                      <tr>
                        <th className="px-4 py-3 text-left text-xs font-medium text-muted-foreground uppercase">
                          İşlem
                        </th>
                        <th className="px-4 py-3 text-left text-xs font-medium text-muted-foreground uppercase">
                          Paket Adı
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border bg-card">
                      {filteredPackages.map((pkg, index) => (
                        <tr key={`${pkg.name}-${index}`} className="hover:bg-secondary/40 transition">
                          <td className="px-4 py-3 whitespace-nowrap">
                            <button
                              type="button"
                              onClick={() => handleChocoInstall(pkg.name)}
                              disabled={loading}
                              className="p-1.5 text-primary hover:bg-primary/10 rounded-lg transition disabled:opacity-50"
                              title="Kur"
                            >
                              <Package className="w-4 h-4" />
                            </button>
                          </td>
                          <td className="px-4 py-3">
                            <button
                              type="button"
                              onClick={() => openChocoPage(pkg.name)}
                              className="text-left hover:underline text-foreground flex items-center gap-2"
                            >
                              <span className="font-medium">{pkg.name}</span>
                              <ExternalLink className="w-3 h-3 text-muted-foreground" />
                            </button>
                            {pkg.description && (
                              <p className="text-xs text-muted-foreground mt-1">{pkg.description}</p>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>

              <div className="bg-yellow-500/10 border border-yellow-500/30 rounded-lg p-4">
                <p className="text-sm text-yellow-700 dark:text-yellow-500">
                  <strong>Not:</strong> Chocolatey ilk kez kullanılıyorsa, sistem otomatik olarak Chocolatey'i kuracaktır. 
                  Bu işlem birkaç dakika sürebilir.
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default InstallSoftwareModal;


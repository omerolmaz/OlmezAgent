import React, { useState } from 'react';
import { X, AlertTriangle, Loader2 } from 'lucide-react';

interface UninstallSoftwareModalProps {
  deviceId: string;
  software: {
    id: string;
    name: string;
    version?: string;
    uninstallString?: string;
    quietUninstallString?: string;
  };
  onClose: () => void;
  onSuccess: () => void;
}

const UninstallSoftwareModal: React.FC<UninstallSoftwareModalProps> = ({ 
  deviceId, 
  software, 
  onClose, 
  onSuccess 
}) => {
  // Best Practice: QuietUninstallString varsa onu kullan (zaten silent), yoksa UninstallString
  const [command, setCommand] = useState(software.quietUninstallString || software.uninstallString || '');
  const [timeout, setTimeout] = useState(1800);
  const [runAsUser, setRunAsUser] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleUninstall = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    try {
      const response = await fetch(`/api/software/${deviceId}/uninstall`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({
          name: software.name,
          command: command,
          timeout: timeout,
          runAsUser: runAsUser
        })
      });

      if (response.ok) {
        onSuccess();
      } else {
        let errorMessage = 'Kaldırma işlemi başlatılamadı';
        try {
          const error = await response.json();
          errorMessage = error.message || errorMessage;
        } catch {
          errorMessage = `Server error: ${response.status} ${response.statusText}`;
        }
        alert(`Hata: ${errorMessage}`);
      }
    } catch (error) {
      console.error('Uninstall error:', error);
      alert('Kaldırma işlemi sırasında bir hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-xl shadow-2xl w-full max-w-2xl max-h-[90vh] overflow-hidden border border-border">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-border">
          <h2 className="text-xl font-semibold text-foreground">Yazılımı Kaldır</h2>
          <button
            onClick={onClose}
            className="text-muted-foreground hover:text-foreground transition-colors"
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Content */}
        <form onSubmit={handleUninstall} className="p-6 space-y-4">
          {/* Warning */}
          <div className="bg-destructive/10 border border-destructive/30 rounded-lg p-4 flex items-start gap-3">
            <AlertTriangle className="w-5 h-5 text-destructive flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="font-medium text-destructive">Dikkat!</h3>
              <p className="text-sm text-destructive/90 mt-1">
                <strong>{software.name}</strong> {software.version && `(${software.version})`} yazılımını kaldırmak üzeresiniz. 
                Bu işlem geri alınamaz.
              </p>
            </div>
          </div>

          {/* Software Info */}
          <div className="bg-secondary/40 rounded-lg p-4">
            <div className="space-y-2">
              <div>
                <span className="text-sm font-medium text-foreground">Yazılım Adı:</span>
                <span className="ml-2 text-sm text-muted-foreground">{software.name}</span>
              </div>
              {software.version && (
                <div>
                  <span className="text-sm font-medium text-foreground">Versiyon:</span>
                  <span className="ml-2 text-sm text-muted-foreground">{software.version}</span>
                </div>
              )}
            </div>
          </div>

          {/* Uninstall Command */}
          <div>
            <label className="block text-sm font-medium text-foreground mb-2">
              Kaldırma Komutu
            </label>
            <textarea
              value={command}
              onChange={(e) => setCommand(e.target.value)}
              rows={3}
              className="w-full px-3 py-2 border border-border rounded-lg bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-primary font-mono text-sm transition"
              required
            />
            <p className="mt-1 text-xs text-muted-foreground">
              Gerekirse komutu düzenleyebilirsiniz (örneğin /quiet veya /norestart parametreleri ekleyebilirsiniz)
            </p>
          </div>

          {/* Timeout */}
          <div>
            <label className="block text-sm font-medium text-foreground mb-2">
              Zaman Aşımı (saniye)
            </label>
            <input
              type="number"
              value={timeout}
              onChange={(e) => setTimeout(parseInt(e.target.value))}
              min="60"
              max="3600"
              className="w-full px-3 py-2 border border-border rounded-lg bg-background text-foreground focus:outline-none focus:ring-2 focus:ring-primary transition"
            />
            <p className="mt-1 text-xs text-muted-foreground">
              Kaldırma işlemi için maksimum bekleme süresi (varsayılan: 1800 saniye / 30 dakika)
            </p>
          </div>

          {/* Run as User */}
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="runAsUser"
              checked={runAsUser}
              onChange={(e) => setRunAsUser(e.target.checked)}
              className="w-4 h-4 text-primary border-border rounded focus:ring-2 focus:ring-primary transition"
            />
            <label htmlFor="runAsUser" className="text-sm text-foreground cursor-pointer">
              Kullanıcı olarak çalıştır
            </label>
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-3 pt-4 border-t border-border">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-foreground border border-border rounded-lg bg-card hover:bg-secondary/60 transition-colors"
              disabled={loading}
            >
              İptal
            </button>
            <button
              type="submit"
              disabled={loading}
              className="px-4 py-2 bg-destructive hover:bg-destructive/90 text-destructive-foreground rounded-lg transition-colors disabled:opacity-50 flex items-center gap-2"
            >
              {loading && <Loader2 className="w-4 h-4 animate-spin" />}
              <span>{loading ? 'Kaldırılıyor...' : 'Kaldır'}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default UninstallSoftwareModal;


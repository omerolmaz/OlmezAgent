import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Folder, File, Download, Upload, Trash2 } from 'lucide-react';
import { remoteOpsService } from '../services/remoteOps.service';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';

interface Entry {
  name: string;
  type: 'file' | 'directory';
  size?: number;
}

export default function Files() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [currentPath, setCurrentPath] = useState('');
  const [entries, setEntries] = useState<Entry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { t } = useTranslation();

  const load = async (path: string) => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const response = await remoteOpsService.listDirectory(id, path);
      if (response.success && response.data && Array.isArray(response.data)) {
        setEntries(
          response.data.map((item) => ({
            name: item.name,
            type: item.type === 'directory' ? 'directory' : 'file',
            size: item.size,
          })),
        );
      } else {
        setEntries([]);
        setError(response.error ?? t('files.error'));
      }
    } catch (err: unknown) {
      console.error('File listing failed', err);
      setEntries([]);
      setError(toErrorMessage(err, t('files.error')));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (id) {
      load(currentPath);
    }
  }, [id, currentPath]);

  const goBack = () => {
    if (!currentPath) return;
    
    // Eğer bir disk içindeysek (C:\something gibi), disklere dön
    const pathParts = currentPath.split('\\').filter(p => p);
    if (pathParts.length === 1) {
      // Disk seviyesindeyiz, root'a dön
      setCurrentPath('');
    } else {
      // Bir üst dizine çık
      pathParts.pop();
      setCurrentPath(pathParts.join('\\'));
    }
  };

  const navigateToEntry = (entry: Entry) => {
    if (entry.type === 'directory') {
      // Eğer tıklanan şey bir disk adı ise (C:\, D:\ gibi), direkt onu set et
      if (entry.name.match(/^[A-Z]:\\$/i)) {
        setCurrentPath(entry.name);
      } else if (!currentPath) {
        // Root seviyesindeyiz, disk seçimi yapılıyor
        setCurrentPath(entry.name);
      } else {
        // Normal dizin navigasyonu
        const normalized = currentPath.replace(/\\$/, '');
        setCurrentPath(`${normalized}\\${entry.name}`);
      }
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button onClick={() => navigate(`/devices/${id}`)} className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm">
            <ArrowLeft className="h-4 w-4" />
          </button>
          <div>
            <h1 className="text-2xl font-semibold">{t('files.title')}</h1>
            <p className="text-sm text-muted-foreground">{t('files.description')}</p>
          </div>
        </div>
        <button className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90">
          <Upload className="h-4 w-4" />
          {t('files.upload')}
        </button>
      </div>

      <div className="rounded-xl border border-border bg-card p-6">
        <div className="mb-4 flex gap-2">
          {currentPath && (
            <button
              onClick={goBack}
              className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm hover:bg-secondary/60"
              title={t('files.goBack')}
            >
              <ArrowLeft className="h-4 w-4" />
            </button>
          )}
          <input
            value={currentPath || t('files.drives')}
            onChange={(event) => setCurrentPath(event.target.value)}
            className="flex-1 rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
            placeholder={t('files.pathPlaceholder')}
          />
        </div>
        {loading ? (
          <div className="flex h-40 items-center justify-center gap-3 text-muted-foreground">
            <RefreshIndicator />
            {t('files.loading')}
          </div>
        ) : error ? (
          <div className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
            {error}
          </div>
        ) : (
          <ul className="space-y-2 text-sm">
            {entries.map((entry) => (
              <li key={entry.name} className="flex items-center justify-between rounded-lg border border-border/60 bg-background/40 px-4 py-3">
                <div className="flex items-center gap-3">
                  {entry.type === 'directory' ? <Folder className="h-4 w-4" /> : <File className="h-4 w-4" />}
                  <button
                    onClick={() => navigateToEntry(entry)}
                    className="text-foreground hover:text-primary"
                  >
                    {entry.name}
                  </button>
                </div>
                <div className="flex items-center gap-2">
                  {entry.type === 'file' && entry.size !== undefined && (
                    <span className="text-xs text-muted-foreground">
                      {t('files.size', { size: (entry.size / 1024).toFixed(1) })}
                    </span>
                  )}
                  {entry.type === 'file' && (
                    <button className="rounded-lg border border-border px-2 py-1 text-xs text-muted-foreground transition hover:bg-secondary/70">
                      <Download className="h-3 w-3" />
                    </button>
                  )}
                  <button className="rounded-lg border border-border/60 px-2 py-1 text-xs text-destructive transition hover:bg-destructive/10">
                    <Trash2 className="h-3 w-3" />
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function RefreshIndicator() {
  return <span className="h-4 w-4 animate-spin rounded-full border-2 border-border border-t-transparent" />;
}


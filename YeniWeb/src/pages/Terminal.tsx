import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Terminal as TerminalIcon, Send } from 'lucide-react';
import { commandService } from '../services/command.service';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';

interface TerminalEntry {
  cmd: string;
  result: string;
  time: string;
  status: 'success' | 'error';
}

export default function Terminal() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [shellType, setShellType] = useState('powershell');
  const [command, setCommand] = useState('');
  const [output, setOutput] = useState<TerminalEntry[]>([]);
  const [isRunning, setIsRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const { t, language } = useTranslation();
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB';
  const shellOptions = useMemo(
    () => [
      { value: 'powershell', label: t('terminal.shells.powershell') },
      { value: 'cmd', label: t('terminal.shells.cmd') },
      { value: 'admin-powershell', label: t('terminal.shells.adminPowershell') },
      { value: 'admin-cmd', label: t('terminal.shells.adminCmd') },
    ],
    [t],
  );

  const executeCommand = async () => {
    const trimmed = command.trim();
    if (!id || !trimmed || isRunning) return;

    const timestamp = new Date().toLocaleTimeString(locale);
    setIsRunning(true);
    setError(null);

    try {
      const response = await commandService.executeAndWait<string>({
        deviceId: id,
        commandType: 'execute',
        parameters: { command: trimmed, shell: shellType },
      });

      const payload = response.data ?? response.command?.result ?? '';
      const formatted =
        typeof payload === 'string' && payload.length > 0 ? payload : JSON.stringify(payload, null, 2);

      setOutput((prev) => [
        ...prev,
        {
          cmd: trimmed,
          result: response.success ? formatted : response.error ?? formatted ?? t('terminal.error'),
          time: timestamp,
          status: response.success ? 'success' : 'error',
        },
      ]);
    } catch (err: unknown) {
      const message = toErrorMessage(err, t('terminal.error'));
      setOutput((prev) => [
        ...prev,
        { cmd: trimmed, result: `${t('terminal.errorPrefix')} ${message}`, time: timestamp, status: 'error' },
      ]);
      setError(message);
    }

    setCommand('');
    setIsRunning(false);
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <button
            onClick={() => navigate('/devices')}
            className="rounded-lg border border-border bg-secondary/40 px-3 py-2 text-sm"
          >
            <ArrowLeft className="h-4 w-4" />
          </button>
          <h1 className="flex items-center gap-2 text-2xl font-semibold">
            <TerminalIcon className="h-6 w-6" />
            {t('terminal.title')}
          </h1>
        </div>
        <select
          value={shellType}
          onChange={(event) => setShellType(event.target.value)}
          className="rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
        >
          {shellOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      {error && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="h-[500px] overflow-y-auto rounded-xl border border-border bg-card p-4 font-mono text-sm">
        {output.map((item, index) => (
          <div key={`${item.time}-${index}`} className="mb-4">
            <div className="text-primary">
              <span className="text-muted-foreground">[{item.time}]</span> $ {item.cmd}
            </div>
            <pre
              className={`whitespace-pre-wrap ${
                item.status === 'success' ? 'text-foreground' : 'text-destructive'
              }`}
            >
              {item.result}
            </pre>
          </div>
        ))}
      </div>

      <div className="flex gap-2">
        <input
          value={command}
          onChange={(event) => setCommand(event.target.value)}
          onKeyDown={(event) => event.key === 'Enter' && executeCommand()}
          placeholder={t('terminal.placeholder')}
          className="flex-1 rounded-lg border border-border bg-secondary/40 px-4 py-2 font-mono text-sm"
        />
        <button
          onClick={executeCommand}
          disabled={isRunning}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-70"
        >
          <Send className="h-4 w-4" />
          {isRunning ? t('terminal.running') : t('terminal.run')}
        </button>
      </div>
    </div>
  );
}

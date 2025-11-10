import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogIn, AlertCircle } from 'lucide-react';
import { useAuthStore } from '../stores/authStore';
import { useTranslation } from '../hooks/useTranslation';

export default function Login() {
  const navigate = useNavigate();
  const { login, isLoading, error } = useAuthStore();
  const [form, setForm] = useState({ username: '', password: '', remember: false });
  const { t } = useTranslation();

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    try {
      await login({ username: form.username, password: form.password, rememberMe: form.remember });
      navigate('/dashboard');
    } catch (err) {
      console.error('Login failed', err);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <div className="w-full max-w-md space-y-6 rounded-xl border border-border bg-card p-8 shadow-xl">
        <div className="flex flex-col items-center gap-2">
          <LogIn className="h-10 w-10 text-primary" />
          <h1 className="text-2xl font-semibold">{t('login.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('login.description')}</p>
        </div>

        <form className="space-y-4" onSubmit={handleSubmit}>
          <div className="space-y-2">
            <label className="text-xs uppercase text-muted-foreground">{t('login.username')}</label>
            <input
              value={form.username}
              onChange={(event) => setForm({ ...form, username: event.target.value })}
              className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              autoComplete="username"
            />
          </div>

          <div className="space-y-2">
            <label className="text-xs uppercase text-muted-foreground">{t('login.password')}</label>
            <input
              type="password"
              value={form.password}
              onChange={(event) => setForm({ ...form, password: event.target.value })}
              className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              autoComplete="current-password"
            />
          </div>

          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <input
              id="remember"
              type="checkbox"
              checked={form.remember}
              onChange={(event) => setForm({ ...form, remember: event.target.checked })}
              className="h-4 w-4 rounded border-border"
            />
            <label htmlFor="remember">{t('login.remember')}</label>
          </div>

          {error && (
            <div className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
              <AlertCircle className="h-4 w-4" />
              <span>{error}</span>
            </div>
          )}

          <button
            type="submit"
            disabled={isLoading}
            className="flex w-full items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isLoading ? (
              <>
                <svg className="h-4 w-4 animate-spin" viewBox="0 0 24 24" />
                <span>{t('login.submitting')}</span>
              </>
            ) : (
              <>
                <LogIn className="h-4 w-4" />
                <span>{t('login.submit')}</span>
              </>
            )}
          </button>
        </form>
      </div>
    </div>
  );
}


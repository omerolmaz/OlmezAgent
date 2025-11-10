import { useCallback, useEffect, useMemo, useState } from 'react';
import { Users as UsersIcon, Plus, Trash2, Edit, ShieldCheck } from 'lucide-react';
import userService from '../services/user.service';
import type { CreateUserRequest, UpdateUserRequest, User, UserRight } from '../types/user.types';
import { useTranslation } from '../hooks/useTranslation';
import { toErrorMessage } from '../utils/error';

type FormMode = 'create' | 'edit';

interface UserFormState {
  username: string;
  email: string;
  password: string;
  rights: UserRight;
  isActive: boolean;
}

const EMPTY_FORM: UserFormState = {
  username: '',
  email: '',
  password: '',
  rights: 'ViewDevices',
  isActive: true,
};

export default function Users() {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [showModal, setShowModal] = useState(false);
  const [formMode, setFormMode] = useState<FormMode>('create');
  const [formData, setFormData] = useState<UserFormState>(EMPTY_FORM);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const { t } = useTranslation();

  const rightsOptions = useMemo(
    () => [
      { value: 'ViewDevices' as UserRight, label: t('users.rightOptions.viewDevices') },
      { value: 'ExecuteCommands' as UserRight, label: t('users.rightOptions.executeCommands') },
      { value: 'ManageDevices' as UserRight, label: t('users.rightOptions.manageDevices') },
      { value: 'All' as UserRight, label: t('users.rightOptions.all') },
    ],
    [t],
  );

  const rightsLabel = useCallback(
    (value: UserRight) => rightsOptions.find((option) => option.value === value)?.label ?? value,
    [rightsOptions],
  );

  const loadUsers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const list = await userService.list();
      setUsers(list);
    } catch (err: unknown) {
      setError(toErrorMessage(err, t('users.errorLoad')));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadUsers();
  }, [loadUsers]);

  const openCreateModal = () => {
    setFormMode('create');
    setFormData(EMPTY_FORM);
    setEditingUser(null);
    setFormError(null);
    setShowModal(true);
  };

  const openEditModal = (user: User) => {
    setFormMode('edit');
    setEditingUser(user);
    setFormData({
      username: user.username,
      email: user.email ?? '',
      password: '',
      rights: user.rights,
      isActive: user.isActive,
    });
    setFormError(null);
    setShowModal(true);
  };

  const handleSave = async () => {
    if (!formData.username.trim()) {
      setFormError(t('users.userRequired'));
      return;
    }
    if (formMode === 'create' && !formData.password.trim()) {
      setFormError(t('users.passwordRequired'));
      return;
    }

    setSaving(true);
    setFormError(null);

    const payload: CreateUserRequest | UpdateUserRequest = {
      username: formData.username.trim(),
      email: formData.email.trim() || undefined,
      rights: formData.rights,
      isActive: formData.isActive,
    };
    if (formData.password.trim()) {
      payload.password = formData.password.trim();
    }

    try {
      if (formMode === 'create') {
        await userService.create(payload as CreateUserRequest);
      } else if (editingUser) {
        await userService.update(editingUser.id, payload as UpdateUserRequest);
      }
      await loadUsers();
      setShowModal(false);
    } catch (err: unknown) {
      setFormError(toErrorMessage(err, t('users.errorSave')));
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (user: User) => {
    const confirmed = window.confirm(t('users.deleteConfirm', { username: user.username }));
    if (!confirmed) return;
    setDeletingId(user.id);
    try {
      await userService.remove(user.id);
      await loadUsers();
    } catch (err: unknown) {
      setError(toErrorMessage(err, t('users.errorDelete')));
    } finally {
      setDeletingId(null);
    }
  };

  const toggleActive = async (user: User) => {
    try {
      const updated = await userService.setActive(user.id, !user.isActive);
      setUsers((prev) => prev.map((item) => (item.id === updated.id ? updated : item)));
    } catch (err: unknown) {
      setError(toErrorMessage(err, t('users.errorStatus')));
    }
  };

  return (
    <div className="space-y-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">{t('users.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('users.count', { count: users.length })}</p>
        </div>
        <button
          onClick={openCreateModal}
          className="inline-flex items-center gap-2 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90"
        >
          <Plus className="h-4 w-4" />
          {t('users.create')}
        </button>
      </div>

      {error && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="overflow-hidden rounded-xl border border-border bg-card">
        <table className="min-w-full divide-y divide-border text-sm">
          <thead className="bg-secondary/60 text-xs uppercase tracking-wide text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left">{t('users.username')}</th>
              <th className="px-4 py-3 text-left">{t('users.email')}</th>
              <th className="px-4 py-3 text-left">{t('users.rights')}</th>
              <th className="px-4 py-3 text-left">{t('users.status')}</th>
              <th className="px-4 py-3 text-left">{t('common.actions')}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-muted-foreground">
                  {t('users.listLoading')}
                </td>
              </tr>
            ) : users.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-muted-foreground">
                  {t('users.listEmpty')}
                </td>
              </tr>
            ) : (
              users.map((user) => (
                <tr key={user.id} className="border-t border-border/70">
                  <td className="px-4 py-3 font-medium text-foreground">{user.username}</td>
                  <td className="px-4 py-3 text-muted-foreground">{user.email ?? '-'}</td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-medium ${
                        user.rights === 'All' ? 'bg-destructive/10 text-destructive' : 'bg-primary/10 text-primary'
                      }`}
                    >
                      <ShieldCheck className="h-3 w-3" />
                      {rightsLabel(user.rights)}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <button
                      onClick={() => toggleActive(user)}
                      className={`rounded-full px-2 py-1 text-xs font-medium ${
                        user.isActive ? 'bg-emerald-500/10 text-emerald-600' : 'bg-muted text-muted-foreground'
                      }`}
                    >
                      {user.isActive ? t('users.active') : t('users.disabled')}
                    </button>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <button
                        onClick={() => openEditModal(user)}
                        className="rounded-lg border border-border bg-secondary/40 p-2 text-muted-foreground transition hover:bg-secondary/70"
                        title={t('users.edit')}
                      >
                        <Edit className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => handleDelete(user)}
                        disabled={deletingId === user.id}
                        className="rounded-lg border border-border/60 p-2 text-destructive transition hover:bg-destructive/10 disabled:cursor-not-allowed disabled:opacity-60"
                        title={t('users.delete')}
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-full max-w-md space-y-4 rounded-xl border border-border bg-card p-6 shadow-xl">
            <div className="flex items-center gap-2">
              <UsersIcon className="h-5 w-5 text-primary" />
              <h2 className="text-xl font-semibold">
                {formMode === 'create' ? t('users.create') : t('users.edit') + ` ${editingUser?.username ?? ''}`}
              </h2>
            </div>

            {formError && (
              <div className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
                {formError}
              </div>
            )}

            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('users.username')}</label>
              <input
                value={formData.username}
                onChange={(event) => setFormData((prev) => ({ ...prev, username: event.target.value }))}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('users.email')}</label>
              <input
                type="email"
                value={formData.email}
                onChange={(event) => setFormData((prev) => ({ ...prev, email: event.target.value }))}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              />
            </div>
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('users.password')}</label>
              <input
                type="password"
                value={formData.password}
                onChange={(event) => setFormData((prev) => ({ ...prev, password: event.target.value }))}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
                placeholder={formMode === 'edit' ? t('users.passwordHint') : undefined}
              />
            </div>
            <div>
              <label className="mb-1 block text-xs uppercase text-muted-foreground">{t('users.rights')}</label>
              <select
                value={formData.rights}
                onChange={(event) => setFormData((prev) => ({ ...prev, rights: event.target.value as UserRight }))}
                className="w-full rounded-lg border border-border bg-secondary/40 px-4 py-2 text-sm"
              >
                {rightsOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <input
                id="user-active"
                type="checkbox"
                checked={formData.isActive}
                onChange={(event) => setFormData((prev) => ({ ...prev, isActive: event.target.checked }))}
                className="h-4 w-4 rounded border-border"
              />
              <label htmlFor="user-active">{t('users.active')}</label>
            </div>

            <div className="flex gap-2 pt-2">
              <button
                onClick={() => setShowModal(false)}
                className="flex-1 rounded-lg border border-border px-4 py-2 text-sm text-muted-foreground transition hover:bg-secondary/70"
                disabled={saving}
              >
                {t('users.cancel')}
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="flex-1 rounded-lg bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-70"
              >
                {saving ? t('users.saving') : t('users.save')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

import { useState } from "react";
import { Plus, Pencil, Trash2, BellRing } from "lucide-react";
import { remindersApi } from "../api/endpoints";
import { useApi } from "../hooks/useApi";
import { useSettings } from "../contexts/SettingsContext";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card, Button, EmptyState, Modal, Spinner } from "../components/ui";
import { ReminderFormModal } from "../components/forms";
import type { Reminder } from "../models";
import { formatDateOnly } from "../utils/present";
import { t, fmt } from "../utils/locale";

export function RemindersPage() {
  const { refresh, data, loading, error, setData } = useApi<Reminder[]>(() => remindersApi.list());
  const { settings } = useSettings();
  const lang = settings.language;
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Reminder | null>(null);
  const [deleting, setDeleting] = useState<Reminder | null>(null);
  const [busy, setBusy] = useState(false);

  const reminders = data ?? [];

  const handleDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await remindersApi.remove(deleting.id);
      setData((prev) => (prev ?? []).filter((r) => r.id !== deleting.id));
      setDeleting(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <PageShell
      title={t("reminders", lang)}
      actions={
        <Button onClick={() => { setEditing(null); setShowForm(true); }}>
          <Plus className="h-4 w-4" /> {t("newReminder", lang)}
        </Button>
      }
    >
      {error && <ErrorBanner message={error} />}

      {!data && loading ? (
        <LoadingBlock label={t("loading", lang)} />
      ) : reminders.length === 0 ? (
        <Card>
          <EmptyState
            icon={<BellRing className="h-7 w-7" />}
            title={t("noRemindersYet", lang)}
            hint={t("reminderHint", lang)}
          />
        </Card>
      ) : (
        <div className="space-y-2">
          {reminders.map((r) => (
            <Card key={r.id} className="flex items-center gap-3 p-3.5">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-50 text-amber-500 dark:bg-amber-900/30">
                <BellRing className="h-5 w-5" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-slate-700 dark:text-slate-200">{r.title}</p>
                {r.message && <p className="truncate text-xs text-slate-400">{r.message}</p>}
                <p className="mt-0.5 text-xs text-amber-600">
                  {formatDateOnly(r.reminderAt)} {t("at", lang)} {new Date(r.reminderAt).toTimeString().slice(0, 5)}
                </p>
              </div>
              <div className="flex shrink-0 gap-1">
                <button className="btn-ghost rounded-lg p-2 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("editReminder", lang)} onClick={() => { setEditing(r); setShowForm(true); }}>
                  <Pencil className="h-4 w-4" />
                </button>
                <button className="btn-ghost rounded-lg p-2 text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-900/20" aria-label={t("deleteReminder", lang)} onClick={() => setDeleting(r)}>
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </Card>
          ))}
        </div>
      )}

      <ReminderFormModal
        open={showForm}
        onClose={() => setShowForm(false)}
        reminder={editing}
        onSaved={() => { setEditing(null); void refresh(); }}
      />

      {deleting && (
        <Modal open={!!deleting} onClose={() => setDeleting(null)} title={t("deleteReminderTitle", lang)}>
          <p className="mb-4 text-sm text-slate-500">{fmt(t("deleteConfirm", lang), { title: deleting.title })}</p>
          <div className="flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setDeleting(null)}>{t("cancel", lang)}</Button>
            <Button variant="danger" onClick={() => void handleDelete()} disabled={busy}>
              {busy && <Spinner />} {t("delete", lang)}
            </Button>
          </div>
        </Modal>
      )}
    </PageShell>
  );
}
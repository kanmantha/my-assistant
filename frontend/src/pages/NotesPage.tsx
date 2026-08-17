import { useState } from "react";
import { Plus, Pencil, Trash2, Pin, PinOff } from "lucide-react";
import { notesApi } from "../api/endpoints";
import { useApi } from "../hooks/useApi";
import { useSettings } from "../contexts/SettingsContext";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card, Button, EmptyState, Modal, Spinner } from "../components/ui";
import { NoteFormModal } from "../components/forms";
import type { Note } from "../models";
import { formatDateOnly } from "../utils/present";
import { t, fmt } from "../utils/locale";

export function NotesPage() {
  const { refresh, data, loading, error, setData } = useApi<Note[]>(() => notesApi.list());
  const { settings } = useSettings();
  const lang = settings.language;
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Note | null>(null);
  const [deleting, setDeleting] = useState<Note | null>(null);
  const [busy, setBusy] = useState(false);

  const notes = data ?? [];
  const pinned = notes.filter((n) => n.isPinned);
  const rest = notes.filter((n) => !n.isPinned);

  const handleTogglePin = async (note: Note) => {
    try {
      const updated = await notesApi.update(note.id, { title: note.title, content: note.content, tags: note.tags, isPinned: !note.isPinned });
      setData((prev) => (prev ?? []).map((n) => (n.id === updated.id ? updated : n)));
    } catch {
      // ignore
    }
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await notesApi.remove(deleting.id);
      setData((prev) => (prev ?? []).filter((n) => n.id !== deleting.id));
      setDeleting(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <PageShell
      title={t("notes", lang)}
      actions={
        <Button onClick={() => { setEditing(null); setShowForm(true); }}>
          <Plus className="h-4 w-4" /> {t("newNote", lang)}
        </Button>
      }
    >
      {error && <ErrorBanner message={error} />}

      {!data && loading ? (
        <LoadingBlock label={t("loading", lang)} />
      ) : notes.length === 0 ? (
        <Card>
          <EmptyState
            icon={<Pin className="h-7 w-7" />}
            title={t("noNotesYet", lang)}
            hint={t("noteHint", lang)}
          />
        </Card>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[...pinned, ...rest].map((note) => (
            <Card key={note.id} className="flex flex-col p-4">
              <div className="mb-1 flex items-start justify-between gap-2">
                <p className="font-semibold text-slate-800 dark:text-slate-100">{note.title}</p>
                <button
                  className={`shrink-0 rounded-lg p-1.5 transition ${note.isPinned ? "text-brand-600" : "text-slate-300 hover:text-slate-500 dark:text-slate-600"}`}
                  onClick={() => void handleTogglePin(note)}
                  aria-label={note.isPinned ? t("unpinNote", lang) : t("pinNote", lang)}
                >
                  {note.isPinned ? <Pin className="h-4 w-4" /> : <PinOff className="h-4 w-4" />}
                </button>
              </div>
              <p className="flex-1 whitespace-pre-wrap text-sm text-slate-600 dark:text-slate-300">{note.content}</p>
              {note.tags.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {note.tags.map((tag) => (
                    <span key={tag} className="chip bg-slate-100 text-slate-500 dark:bg-slate-800 dark:text-slate-400">#{tag}</span>
                  ))}
                </div>
              )}
              <div className="mt-3 flex items-center justify-between border-t border-slate-100 pt-3 dark:border-slate-800">
                <span className="text-xs text-slate-400">{formatDateOnly(note.createdAt)}</span>
                <div className="flex gap-1">
                  <button className="btn-ghost rounded-lg p-2 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("editNote", lang)} onClick={() => { setEditing(note); setShowForm(true); }}>
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button className="btn-ghost rounded-lg p-2 text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-900/20" aria-label={t("deleteNote", lang)} onClick={() => setDeleting(note)}>
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}

      <NoteFormModal
        open={showForm}
        onClose={() => setShowForm(false)}
        note={editing}
        onSaved={() => { setEditing(null); void refresh(); }}
      />

      {deleting && (
        <Modal open={!!deleting} onClose={() => setDeleting(null)} title={t("deleteNoteTitle", lang)}>
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
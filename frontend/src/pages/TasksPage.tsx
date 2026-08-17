import { useState } from "react";
import { Plus, Pencil, Trash2, CheckCircle2 } from "lucide-react";
import { tasksApi } from "../api/endpoints";
import { useApi } from "../hooks/useApi";
import { useSettings } from "../contexts/SettingsContext";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card, Button, Badge, EmptyState, Select, Modal, Spinner } from "../components/ui";
import { TaskFormModal } from "../components/forms";
import type { Task } from "../models";
import { statusName, priorityName, statusBadgeColor, priorityBadgeColor, formatDateOnly } from "../utils/present";
import { t, fmt, taskStatusLabel, priorityLabel } from "../utils/locale";

const FILTER_VALUES = ["all", "0", "1", "2"];

export function TasksPage() {
  const { refresh, data, loading, error, setData } = useApi<Task[]>(() => tasksApi.list());
  const { settings } = useSettings();
  const lang = settings.language;
  const [filter, setFilter] = useState("all");
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Task | null>(null);
  const [deleting, setDeleting] = useState<Task | null>(null);
  const [busy, setBusy] = useState(false);

  const FILTER_LABELS: Record<string, string> = {
    all: t("all", lang),
    "0": taskStatusLabel(0, lang),
    "1": taskStatusLabel(1, lang),
    "2": taskStatusLabel(2, lang)
  };

  const filtered = (data ?? []).filter((t) => {
    if (filter === "all") return true;
    return String(t.status) === filter;
  });

  const handleCycleStatus = async (t: Task) => {
    const next = statusName(t.status) === "Completed" ? 0 : 2;
    const updated = await tasksApi.updateStatus(t.id, next);
    setData((prev) => (prev ?? []).map((item) => (item.id === updated.id ? updated : item)));
  };

  const handleDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await tasksApi.remove(deleting.id);
      setData((prev) => (prev ?? []).filter((t) => t.id !== deleting.id));
      setDeleting(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <PageShell
      title={t("tasks", lang)}
      actions={
        <Button onClick={() => { setEditing(null); setShowForm(true); }}>
          <Plus className="h-4 w-4" /> {t("newTask", lang)}
        </Button>
      }
    >
      {error && <ErrorBanner message={error} />}

      <div className="mb-4 flex max-w-xs">
        <Select value={filter} onChange={(e) => setFilter(e.target.value)} aria-label={t("filterAria", lang)}>
          {FILTER_VALUES.map((v) => (
            <option key={v} value={v}>{FILTER_LABELS[v]}</option>
          ))}
        </Select>
      </div>

      {!data && loading ? (
        <LoadingBlock label={t("loading", lang)} />
      ) : filtered.length === 0 ? (
        <Card>
          <EmptyState
            icon={<CheckCircle2 className="h-7 w-7" />}
            title={filter === "all" ? t("noTasksYet", lang) : t("noTasksInView", lang)}
            hint={t("taskHint", lang)}
          />
        </Card>
      ) : (
        <div className="space-y-2">
          {filtered.map((task) => (
            <Card key={task.id} className="flex items-center gap-3 p-3.5">
              <button
                onClick={() => void handleCycleStatus(task)}
                className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full border-2 ${statusName(task.status) === "Completed" ? "border-emerald-500 bg-emerald-500 text-white" : "border-slate-300 dark:border-slate-600"}`}
                aria-label={statusName(task.status) === "Completed" ? t("markIncomplete", lang) : t("markCompleted", lang)}
              >
                {statusName(task.status) === "Completed" && <CheckCircle2 className="h-4 w-4" />}
              </button>

              <div className="min-w-0 flex-1">
                <p className={`text-sm font-medium ${statusName(task.status) === "Completed" ? "line-through text-slate-400" : "text-slate-700 dark:text-slate-200"}`}>
                  {task.title}
                </p>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-400">
                  <Badge color={priorityBadgeColor(task.priority)}>{priorityLabel(task.priority, lang)}</Badge>
                  <Badge color={statusBadgeColor(task.status)}>{taskStatusLabel(task.status, lang)}</Badge>
                  {task.dueDate && <span>{formatDateOnly(task.dueDate)}</span>}
                  {task.category && <span>{task.category}</span>}
                </div>
              </div>

              <div className="flex shrink-0 gap-1">
                <button className="btn-ghost rounded-lg p-2 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("editTask", lang)} onClick={() => { setEditing(task); setShowForm(true); }}>
                  <Pencil className="h-4 w-4" />
                </button>
                <button className="btn-ghost rounded-lg p-2 text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-900/20" aria-label={t("deleteTask", lang)} onClick={() => setDeleting(task)}>
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </Card>
          ))}
        </div>
      )}

      <TaskFormModal
        open={showForm}
        onClose={() => setShowForm(false)}
        task={editing}
        onSaved={() => { setEditing(null); void refresh(); }}
      />

      {deleting && (
        <Modal open={!!deleting} onClose={() => setDeleting(null)} title={t("deleteTaskTitle", lang)}>
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
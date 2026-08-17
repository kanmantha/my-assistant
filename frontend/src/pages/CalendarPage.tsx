import { useState } from "react";
import { ChevronLeft, ChevronRight, Plus, Trash2, Pencil } from "lucide-react";
import { useSettings } from "../contexts/SettingsContext";
import { appointmentsApi } from "../api/endpoints";
import { useApi } from "../hooks/useApi";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card, Button, Modal, EmptyState, Spinner } from "../components/ui";
import { AppointmentFormModal } from "../components/forms";
import type { Appointment } from "../models";
import { monthMatrix, todayString } from "../utils/date";
import { apptStatusColor } from "../utils/present";
import { t, fmt, appointmentStatusLabel, dateLocale } from "../utils/locale";

const WEEKDAYS = ["S", "M", "T", "W", "T", "F", "S"];

export function CalendarPage() {
  const now = new Date();
  const { settings } = useSettings();
  const lang = settings.language;
  const [cursor, setCursor] = useState({ year: now.getFullYear(), month: now.getMonth() });
  const [selected, setSelected] = useState<string>(todayString());
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState<Appointment | null>(null);
  const [deleting, setDeleting] = useState<Appointment | null>(null);
  const [busy, setBusy] = useState(false);

  const monthStart = `${cursor.year}-${String(cursor.month + 1).padStart(2, "0")}-01`;
  const monthEnd = new Date(cursor.year, cursor.month + 1, 0).toISOString().slice(0, 10);
  const { refresh, data, loading, error, setData } = useApi<Appointment[]>(() => appointmentsApi.list(monthStart, monthEnd), [monthStart, monthEnd]);

  const weeks = monthMatrix(cursor.year, cursor.month);
  const today = todayString();
  const loc = dateLocale(lang);

  const handleDelete = async () => {
    if (!deleting) return;
    setBusy(true);
    try {
      await appointmentsApi.remove(deleting.id);
      setData((prev) => (prev ?? []).filter((a) => a.id !== deleting.id));
      setDeleting(null);
    } finally {
      setBusy(false);
    }
  };

  const dayAppointments = (day: Date | string) => {
    const key = typeof day === "string" ? day : day.toISOString().slice(0, 10);
    return (data ?? []).filter((a) => a.startDateTime.slice(0, 10) === key);
  };

  return (
    <PageShell title={t("calendar", lang)}>
      <div className="mb-4 flex items-center justify-between">
        <div className="flex items-center gap-1">
          <button className="btn-ghost rounded-lg p-2 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("prevMonth", lang)} onClick={() => setCursor((c) => (c.month === 0 ? { year: c.year - 1, month: 11 } : { ...c, month: c.month - 1 }))}>
            <ChevronLeft className="h-5 w-5" />
          </button>
          <h2 className="min-w-40 text-center text-lg font-semibold text-slate-800 dark:text-slate-100">
            {new Date(cursor.year, cursor.month, 1).toLocaleDateString(loc, { month: "long", year: "numeric" })}
          </h2>
          <button className="btn-ghost rounded-lg p-2 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("nextMonth", lang)} onClick={() => setCursor((c) => (c.month === 11 ? { year: c.year + 1, month: 0 } : { ...c, month: c.month + 1 }))}>
            <ChevronRight className="h-5 w-5" />
          </button>
        </div>
        <Button onClick={() => { setEditing(null); setShowForm(true); }}>
          <Pencil className="h-4 w-4" /> {t("newEvent", lang)}
        </Button>
      </div>

      {error && <ErrorBanner message={error} />}

      <div className="grid grid-cols-7 gap-1 rounded-2xl bg-white p-2 dark:bg-slate-900">
        {WEEKDAYS.map((d, i) => (
          <div key={i} className="pb-1 text-center text-xs font-bold text-slate-400">{d}</div>
        ))}
        {weeks.flat().map((day, i) => {
          const key = day.toISOString().slice(0, 10);
          const items = dayAppointments(key);
          const inMonth = day.getMonth() === cursor.month;
          const isToday = key === today;
          const isSelected = key === selected;
          return (
            <button
              key={i}
              onClick={() => setSelected(key)}
              className={`flex min-h-16 flex-col items-stretch gap-1 rounded-lg border p-1 text-left transition ${
                isSelected ? "border-brand-500 bg-brand-50 dark:bg-brand-900/30" : isToday ? "border-brand-300 bg-brand-50/40 dark:border-brand-800 dark:bg-slate-800" : "border-transparent hover:bg-slate-50 dark:hover:bg-slate-800"
              }`}
            >
              <span className={`text-xs font-semibold ${inMonth ? "text-slate-700 dark:text-slate-300" : "text-slate-300 dark:text-slate-600"} ${isToday ? "text-brand-600" : ""}`}>
                {day.getDate()}
              </span>
              <span className="flex flex-col gap-0.5">
                {items.slice(0, 2).map((a) => (
                  <span key={a.id} className="truncate rounded bg-emerald-100 px-1 text-[10px] font-medium text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-300">
                    {a.startDateTime.slice(11, 16)} {a.title}
                  </span>
                ))}
                {items.length > 2 && <span className="px-1 text-[10px] text-slate-400">{fmt(t("moreCount", lang), { count: items.length - 2 })}</span>}
              </span>
            </button>
          );
        })}
      </div>

      {/* Selected day */}
      <Card className="mt-4 p-4">
        <h3 className="mb-3 text-sm font-semibold text-slate-700 dark:text-slate-200">
          {new Date(selected + "T00:00").toLocaleDateString(loc, { weekday: "long", day: "numeric", month: "long" })}
        </h3>
        {loading ? <LoadingBlock label={t("loading", lang)} /> : dayAppointments(selected).length === 0 ? (
          <EmptyState icon={<span className="h-7 w-7">🗓️</span>} title={t("noAppointmentsThisDay", lang)} />
        ) : (
          <div className="space-y-2">
            {dayAppointments(selected).map((a) => (
              <div key={a.id} className="flex items-center justify-between gap-3 rounded-lg border border-slate-100 p-3 dark:border-slate-800">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-slate-700 dark:text-slate-200">{a.title}</p>
                  <p className="text-xs text-slate-400">
                    {a.startDateTime.slice(11, 16)} – {a.endDateTime?.slice(11, 16)} {a.location ? `· ${a.location}` : ""}
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <span className={`chip ${apptStatusColor(a.status)}`}>{appointmentStatusLabel(a.status, lang)}</span>
                  <button className="btn-ghost rounded-lg p-2 text-slate-400 hover:bg-slate-100 dark:hover:bg-slate-800" aria-label={t("editAppointment", lang)} onClick={() => { setEditing(a); setShowForm(true); }}>
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button className="btn-ghost rounded-lg p-2 text-rose-400 hover:bg-rose-50 dark:hover:bg-rose-900/20" aria-label={t("deleteAppointment", lang)} onClick={() => setDeleting(a)}>
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </Card>

      <AppointmentFormModal
        open={showForm}
        onClose={() => setShowForm(false)}
        appointment={editing}
        defaultDate={selected}
        onSaved={() => { setEditing(null); void refresh(); }}
      />

      {deleting && (
        <Modal open={!!deleting} onClose={() => setDeleting(null)} title={t("deleteAppointmentTitle", lang)}>
          <p className="mb-4 text-sm text-slate-500">{fmt(t("deleteConfirmPermanent", lang), { title: deleting.title })}</p>
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
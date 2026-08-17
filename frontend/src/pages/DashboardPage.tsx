import { Link } from "react-router-dom";
import { ListTodo, BellRing, StickyNote, Plus, ChevronRight } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { useSettings } from "../contexts/SettingsContext";
import { useAssistant } from "../contexts/AssistantContext";
import { useApi } from "../hooks/useApi";
import { dashboardApi } from "../api/endpoints";
import { AssistantPanel } from "../components/AssistantPanel";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card } from "../components/ui";
import { formatTime, getGreeting } from "../utils/date";
import { statusColor } from "../utils/present";
import { t, taskStatusLabel } from "../utils/locale";

export function DashboardPage() {
  const { user } = useAuth();
  const { settings } = useSettings();
  const assistant = useAssistant();
  const { data, loading, error, refresh } = useApi(() => dashboardApi.get(), []);
  const uiLang = settings.language.toLowerCase() === "hi" ? "hi" : settings.language.toLowerCase() === "te" ? "te" : "en";

  const greeting = getGreeting(new Date(), user?.displayName ?? "there", uiLang);

  const quick = [
    { to: "/notes", label: t("addNote", uiLang), icon: StickyNote },
    { to: "/tasks", label: t("addTask", uiLang), icon: ListTodo },
    { to: "/reminders", label: t("addReminder", uiLang), icon: BellRing },
    { to: "/calendar", label: t("addAppointment", uiLang), icon: Plus }
  ];

  const stats = [
    { label: t("today", uiLang) + " " + t("tasks", uiLang), value: data?.tasksToday ?? 0, color: "text-brand-600" },
    { label: t("today", uiLang) + " " + t("calendar", uiLang), value: data?.todayAppointments.length ?? 0, color: "text-emerald-600" },
    { label: t("reminders", uiLang), value: data?.upcomingReminders.length ?? 0, color: "text-amber-600" },
    { label: t("notes", uiLang), value: data?.recentNotes.length ?? 0, color: "text-sky-600" }
  ];

  return (
    <PageShell>
      <div className="mb-6 text-center">
        <h1 className="text-2xl font-bold text-slate-800 dark:text-slate-100">{greeting}</h1>
        <div className="mt-1 flex items-center justify-center gap-2 text-sm">
          <span className="inline-block h-2 w-2 rounded-full bg-emerald-500" />
          <span className="font-medium text-emerald-600 dark:text-emerald-400">
            {t("ready", uiLang)} · {assistant.wakeListening ? t("wakeActive", uiLang) : t("say", uiLang)}
          </span>
        </div>
      </div>

      {/* Assistant / orb */}
      <div className="glass-card mx-auto mb-8 max-w-2xl">
        <AssistantPanel />
      </div>

      {error && <ErrorBanner message={error} />}
      {loading && !data ? <LoadingBlock label={t("loading", uiLang)} /> : null}
      {data && (
        <>
          {/* Stats */}
          <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {stats.map((s, i) => (
              <Card key={i} className="p-4 text-center">
                <div className={`text-3xl font-bold ${s.color}`}>{s.value}</div>
                <div className="mt-1 text-xs font-medium text-slate-500">{s.label}</div>
              </Card>
            ))}
          </div>

          {/* Quick actions */}
          <div className="mb-6 grid grid-cols-2 gap-3 sm:grid-cols-4">
            {quick.map(({ to, label, icon: Icon }) => (
              <Link
                key={to}
                to={to}
                className="flex items-center gap-2 rounded-2xl border border-dashed border-brand-300 bg-brand-50/50 px-4 py-3 text-sm font-semibold text-brand-700 transition hover:border-brand-500 hover:bg-brand-100 dark:border-brand-800 dark:bg-brand-900/20 dark:text-brand-300 dark:hover:bg-brand-900/40"
              >
                <Icon className="h-4 w-4" />
                {label}
              </Link>
            ))}
          </div>

          {/* Sections */}
          <div className="grid gap-4 lg:grid-cols-2">
            {data.todayTasks.length > 0 && (
              <SectionCard title={t("tasks", uiLang)} href="/tasks">
                {data.todayTasks.map((task) => (
                  <div key={task.id} className="flex items-center justify-between gap-2 py-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{task.title}</p>
                      <p className="text-xs text-slate-400">{task.dueDate}{task.dueTime ? ` · ${task.dueTime}` : ""}</p>
                    </div>
                    <span className={`chip ${statusColor(task.status)}`}>{taskStatusLabel(task.status, uiLang)}</span>
                  </div>
                ))}
              </SectionCard>
            )}

            {data.todayAppointments.length > 0 && (
              <SectionCard title={t("calendar", uiLang)}>
                {data.todayAppointments.map((a) => (
                  <div key={a.id} className="flex items-center justify-between gap-2 py-2">
                    <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{a.title}</p>
                    <span className="text-xs font-semibold text-emerald-600">{formatTime(new Date(a.startDateTime))}</span>
                  </div>
                ))}
              </SectionCard>
            )}

            {data.upcomingReminders.length > 0 && (
              <SectionCard title={t("reminders", uiLang)}>
                {data.upcomingReminders.slice(0, 4).map((r) => (
                  <div key={r.id} className="flex items-center justify-between gap-2 py-2">
                    <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{r.title}</p>
                    <span className="shrink-0 text-xs text-amber-600">{formatTime(new Date(r.reminderAt))}</span>
                  </div>
                ))}
              </SectionCard>
            )}

            {data.recentNotes.length > 0 && (
              <SectionCard title={t("notes", uiLang)}>
                {data.recentNotes.map((n) => (
                  <div key={n.id} className="py-2">
                    <p className="text-sm font-medium text-slate-700 dark:text-slate-200">{n.title}</p>
                    <p className="line-clamp-1 text-xs text-slate-400">{n.content}</p>
                  </div>
                ))}
              </SectionCard>
            )}
          </div>
        </>
      )}
    </PageShell>
  );
}

function SectionCard({ title, children, href }: { title: string; children: React.ReactNode; href?: string }) {
  return (
    <Card className="p-4">
      <h2 className="mb-1 flex items-center gap-1 text-xs font-bold uppercase tracking-wide text-slate-400">
        {title}
        {href && (
          <Link to={href} className="font-semibold normal-case text-brand-600 hover:text-brand-700 dark:text-brand-400">
            <ChevronRight className="h-3 w-3" />
          </Link>
        )}
      </h2>
      <div>{children}</div>
    </Card>
  );
}
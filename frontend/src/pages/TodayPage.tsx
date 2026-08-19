import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ListTodo, BellRing, CalendarDays, Sparkles, ChevronRight } from "lucide-react";
import { useSettings } from "../contexts/SettingsContext";
import { useApi } from "../hooks/useApi";
import { dashboardApi, remindersApi } from "../api/endpoints";
import { PageShell, LoadingBlock, ErrorBanner } from "../components/PageShell";
import { Card } from "../components/ui";
import { formatTime, formatFullDate, toLocalDate, isSameDay } from "../utils/date";
import { statusColor } from "../utils/present";
import { t, taskStatusLabel } from "../utils/locale";

export function TodayPage() {
  const { settings } = useSettings();
  const uiLang = settings.language.toLowerCase() === "hi" ? "hi" : settings.language.toLowerCase() === "te" ? "te" : "en";
  const { data, loading, error } = useApi(() => dashboardApi.get(), []);
  const reminders = useApi(() => remindersApi.list(), []);

  const todayReminders = (reminders.data ?? []).filter((r) => {
    const d = toLocalDate(r.reminderAt);
    return d ? isSameDay(d, new Date()) : false;
  });

  return (
    <PageShell title={t("today", uiLang)}>
      <p className="-mt-2 mb-6 text-center text-sm text-slate-400">{formatFullDate(new Date().toISOString())}</p>

      {error && <ErrorBanner message={error} />}
      {reminders.error && <ErrorBanner message={reminders.error} />}
      {loading || reminders.loading ? <LoadingBlock label={t("loading", uiLang)} /> : null}

      {data && !loading && (
        <div className="grid gap-4 lg:grid-cols-2">
          <Section title={t("tasks", uiLang)} icon={ListTodo} href="/tasks" empty={data.todayTasks.length === 0} emptyText={t("noTasksInView", uiLang)}>
            {data.todayTasks.map((task) => (
              <div key={task.id} className="flex items-center justify-between gap-2 py-2">
                <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{task.title}</p>
                <div className="flex shrink-0 items-center gap-2">
                  {task.dueTime && <span className="text-xs text-slate-400">{task.dueTime}</span>}
                  <span className={`chip ${statusColor(task.status)}`}>{taskStatusLabel(task.status, uiLang)}</span>
                </div>
              </div>
            ))}
          </Section>

          <Section title={t("calendar", uiLang)} icon={CalendarDays} href="/calendar" empty={data.todayAppointments.length === 0} emptyText={t("noAppointmentsThisDay", uiLang)}>
            {data.todayAppointments.map((a) => (
              <div key={a.id} className="flex items-center justify-between gap-2 py-2">
                <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{a.title}</p>
                <span className="shrink-0 text-xs font-semibold text-emerald-600">{formatTime(new Date(a.startDateTime))}</span>
              </div>
            ))}
          </Section>

          <Section title={t("reminders", uiLang)} icon={BellRing} href="/reminders" empty={todayReminders.length === 0} emptyText={t("noRemindersYet", uiLang)}>
            {todayReminders.map((r) => (
              <div key={r.id} className="flex items-center justify-between gap-2 py-2">
                <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{r.title}</p>
                <span className="shrink-0 text-xs text-amber-600">{formatTime(new Date(r.reminderAt))}</span>
              </div>
            ))}
          </Section>

          <Card className="p-4">
            <h2 className="mb-2 flex items-center gap-1 text-xs font-bold uppercase tracking-wide text-slate-400">
              <Sparkles className="h-3.5 w-3.5" />
              {t("assistant", uiLang)}
            </h2>
            <p className="text-sm text-slate-600 dark:text-slate-300">{t("todayHint", uiLang)}</p>
          </Card>
        </div>
      )}
    </PageShell>
  );
}

function Section({
  title,
  icon: Icon,
  href,
  empty,
  emptyText,
  children
}: {
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  href: string;
  empty: boolean;
  emptyText: string;
  children: ReactNode;
}) {
  return (
    <Card className="p-4">
      <h2 className="mb-1 flex items-center gap-1 text-xs font-bold uppercase tracking-wide text-slate-400">
        <Icon className="h-3.5 w-3.5" />
        {title}
        <Link to={href} className="ml-auto font-semibold normal-case text-brand-600 hover:text-brand-700 dark:text-brand-400">
          <ChevronRight className="h-3 w-3" />
        </Link>
      </h2>
      {empty ? <p className="py-4 text-center text-sm text-slate-400">{emptyText}</p> : <div>{children}</div>}
    </Card>
  );
}
import { NavLink, Outlet } from "react-router-dom";
import { Home, ListTodo, Calendar, StickyNote, BellRing, Settings, Search, MessageSquare, LogOut, Sparkles, History, CalendarDays } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { useSettings } from "../contexts/SettingsContext";
import { useAssistant } from "../contexts/AssistantContext";
import { VoiceOverlay } from "./VoiceOverlay";
import { t } from "../utils/locale";

const DESKTOP_NAV = [
  { to: "/", icon: Home, key: "home" },
  { to: "/today", icon: CalendarDays, key: "today" },
  { to: "/tasks", icon: ListTodo, key: "tasks" },
  { to: "/calendar", icon: Calendar, key: "calendar" },
  { to: "/notes", icon: StickyNote, key: "notes" },
  { to: "/reminders", icon: BellRing, key: "reminders" },
  { to: "/search", icon: Search, key: "search" },
  { to: "/history", icon: History, key: "history" },
  { to: "/settings", icon: Settings, key: "settings" }
];

const MOBILE_NAV = [
  { to: "/", icon: Home, key: "home" },
  { to: "/today", icon: CalendarDays, key: "today" },
  { to: "/tasks", icon: ListTodo, key: "tasks" },
  { to: "/calendar", icon: Calendar, key: "calendar" },
  { to: "/notes", icon: StickyNote, key: "notes" },
  { to: "/settings", icon: Settings, key: "settings" }
];

export function AppLayout() {
  const { user, logout } = useAuth();
  const { settings } = useSettings();
  const assistant = useAssistant();

  const lang = settings.language;
  const uiLang = lang.toLowerCase() === "hi" ? "hi" : lang.toLowerCase() === "te" ? "te" : "en";
  const initials = user
    ? `${user.firstName?.[0] ?? ""}${user.lastName?.[0] ?? ""}`.toUpperCase() || "U"
    : "U";

  const handleLogout = () => {
    logout();
  };

  return (
    <div className="flex min-h-screen bg-slate-50 dark:bg-slate-950">
      {/* Desktop sidebar */}
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-20 flex-col items-center border-r border-slate-200 bg-white py-5 dark:border-slate-800 dark:bg-slate-900 lg:flex">
        <NavLink
          to="/"
          className="mb-8 flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-brand-500 to-brand-700 text-white shadow-lg"
          aria-label={t("appHome", uiLang)}
        >
          <Sparkles className="h-5 w-5" />
        </NavLink>

        <nav className="flex flex-1 flex-col items-center gap-1" aria-label={t("mainNav", uiLang)}>
          {DESKTOP_NAV.map(({ to, icon: Icon, key }) => (
            <NavLink
              key={to}
              to={to}
              end={to === "/"}
              title={t(key, uiLang)}
              className={({ isActive }) =>
                `flex h-11 w-11 items-center justify-center rounded-xl transition ${isActive ? "bg-brand-100 text-brand-600 dark:bg-brand-900/40 dark:text-brand-300" : "text-slate-400 hover:bg-slate-100 hover:text-slate-700 dark:hover:bg-slate-800 dark:hover:text-slate-200"}`
              }
            >
              <Icon className="h-5 w-5" />
            </NavLink>
          ))}
        </nav>

        <button
          onClick={handleLogout}
          title={t("logout", uiLang)}
          className="mb-2 flex h-11 w-11 items-center justify-center rounded-xl text-slate-400 transition hover:bg-rose-50 hover:text-rose-500"
        >
          <LogOut className="h-5 w-5" />
        </button>
        <div
          className="flex h-9 w-9 items-center justify-center rounded-full bg-gradient-to-br from-brand-400 to-brand-600 text-xs font-bold text-white"
          title={user?.displayName ?? t("userFallback", uiLang)}
        >
          {initials}
        </div>
      </aside>

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col lg:pl-20">
        <main className="flex-1 pb-24 lg:pb-8" role="main">
          <Outlet />
        </main>
      </div>

      {/* Mobile bottom nav */}
      <nav
        className="fixed inset-x-0 bottom-0 z-30 flex items-center justify-around border-t border-slate-200 bg-white/95 py-1.5 backdrop-blur dark:border-slate-800 dark:bg-slate-900/95 lg:hidden"
        aria-label={t("mobileNav", uiLang)}
      >
        {MOBILE_NAV.map(({ to, icon: Icon, key }) => (
          <NavLink
            key={to}
            to={to}
            end={to === "/"}
            className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}
          >
            <Icon className="h-5 w-5" />
            <span>{t(key, uiLang)}</span>
          </NavLink>
        ))}
      </nav>

      {/* Floating mic */}
      <button
        onClick={assistant.openVoiceOverlay}
        aria-label={t("openAssistant", uiLang)}
        className={`fixed bottom-20 right-4 z-30 flex h-16 w-16 items-center justify-center rounded-full bg-gradient-to-br text-white shadow-2xl transition-transform active:scale-90 lg:hidden ${
          assistant.status !== "idle"
            ? "from-rose-500 to-fuchsia-500"
            : "from-brand-500 to-brand-700"
        }`}
      >
        <Sparkles className="h-7 w-7" />
      </button>

      {/* Full-screen Siri-style voice overlay */}
      <VoiceOverlay />
    </div>
  );
}
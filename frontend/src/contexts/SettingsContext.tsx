import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { settingsApi } from "../api/endpoints";
import { tokenStore } from "../api/http";
import type { UpdateSettingsPayload, UserSettings } from "../models";

const DEFAULT_SETTINGS: UserSettings = {
  language: "en",
  autoDetectLanguage: true,
  speechSpeed: 1,
  voiceVolume: 1,
  muteAssistantVoice: false,
  wakeWordEnabled: true,
  wakeWord: "Assistant",
  notificationsEnabled: true,
  defaultReminderMinutes: 10,
  timeZone: "Asia/Kolkata",
  theme: "System",
  confirmationMode: false,
  reducedMotion: false,
  highContrast: false,
  fontScale: 100
};

interface SettingsContextValue {
  settings: UserSettings;
  loading: boolean;
  update: (patch: UpdateSettingsPayload) => Promise<UserSettings>;
  setLocal: (patch: Partial<UserSettings>) => void;
}

const UI_LANG_KEY = "myassistant.ui_lang";

function readSavedUiLang(): string | undefined {
  try {
    return localStorage.getItem(UI_LANG_KEY) ?? undefined;
  } catch {
    return undefined;
  }
}

function rememberUiLang(lang: string) {
  try {
    localStorage.setItem(UI_LANG_KEY, lang);
  } catch {
    // ignore
  }
}

const SettingsContext = createContext<SettingsContextValue | undefined>(undefined);

function resolveTheme(theme: string): "light" | "dark" {
  if (theme === "Light") return "light";
  if (theme === "Dark") return "dark";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

export function SettingsProvider({ children }: { children: ReactNode }) {
  const savedLang = readSavedUiLang();
  const [settings, setSettings] = useState<UserSettings>(() =>
    savedLang ? { ...DEFAULT_SETTINGS, language: savedLang } : DEFAULT_SETTINGS
  );
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!tokenStore.accessToken) {
      setLoading(false);
      return;
    }
    settingsApi
      .get()
      .then((s) => {
        setSettings(s);
        applyThemeAndAccessibility(s);
      })
      .catch(() => undefined)
      .finally(() => setLoading(false));
  }, []);

  const applyThemeAndAccessibility = (s: UserSettings) => {
    document.documentElement.setAttribute("data-theme", resolveTheme(s.theme));
    document.documentElement.classList.toggle("dark", resolveTheme(s.theme) === "dark");
    document.documentElement.classList.toggle("reduced-motion", s.reducedMotion);
    document.documentElement.style.fontSize = `${s.fontScale}%`;
    document.documentElement.classList.toggle("high-contrast", s.highContrast);
    document.documentElement.setAttribute("lang", s.language === "hi" ? "hi" : s.language === "te" ? "te" : "en");
  };

  const update = async (patch: UpdateSettingsPayload) => {
    const next = await settingsApi.update(patch);
    if (next.language) rememberUiLang(next.language);
    setSettings(next);
    applyThemeAndAccessibility(next);
    return next;
  };

  const setLocal = (patch: Partial<UserSettings>) => {
    if (patch.language) rememberUiLang(patch.language);
    setSettings((prev) => {
      const next = { ...prev, ...patch };
      applyThemeAndAccessibility(next);
      return next;
    });
  };

  const value = useMemo(() => ({ settings, loading, update, setLocal }), [settings, loading]);

  return <SettingsContext.Provider value={value}>{children}</SettingsContext.Provider>;
}

export function useSettings() {
  const ctx = useContext(SettingsContext);
  if (!ctx) {
    throw new Error("useSettings must be used within SettingsProvider");
  }
  return ctx;
}
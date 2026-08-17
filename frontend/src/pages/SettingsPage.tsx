import { useState } from "react";
import { useSettings } from "../contexts/SettingsContext";
import { useAuth } from "../contexts/AuthContext";
import { authApi } from "../api/endpoints";
import { PageShell } from "../components/PageShell";
import { Card, Button, Field, Input, Select, Switch, Spinner } from "../components/ui";
import { t } from "../utils/locale";

const LANGUAGES = [
  { label: "English", value: "en" },
  { label: "Hindi (हिंदी)", value: "hi" },
  { label: "Telugu (తెలుగు)", value: "te" }
];

export function SettingsPage() {
  const { settings, loading, update } = useSettings();
  const { user, refreshUser } = useAuth();
  const [firstName, setFirstName] = useState(user?.firstName ?? "");
  const [lastName, setLastName] = useState(user?.lastName ?? "");
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  const lang = settings.language;

  const THEMES = [
    { label: t("themeLight", lang), value: "Light" },
    { label: t("themeDark", lang), value: "Dark" },
    { label: t("themeSystem", lang), value: "System" }
  ];

  const apply = async (patch: Parameters<typeof update>[0]) => {
    setSaving(true);
    try {
      await update(patch);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } finally {
      setSaving(false);
    }
  };

  const saveProfile = async () => {
    setSaving(true);
    try {
      await authApi.updateProfile({ firstName, lastName });
      await refreshUser();
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <PageShell title={t("settings", lang)}>
        <div className="flex items-center justify-center py-16">
          <Spinner className="h-6 w-6" />
        </div>
      </PageShell>
    );
  }

  return (
    <PageShell title={t("settings", lang)}>
      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="p-5">
          <h2 className="mb-1 font-semibold text-slate-800 dark:text-slate-100">{t("appearanceSection", lang)}</h2>
          <p className="mb-4 text-sm text-slate-500">{t("appearanceSub", lang)}</p>
          <div className="space-y-4">
            <Field label={t("languageField", lang)}>
              <Select value={settings.language} onChange={(e) => void apply({ language: e.target.value })}>
                {LANGUAGES.map((l) => (
                  <option key={l.value} value={l.value}>{l.label}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("themeField", lang)}>
              <Select value={settings.theme} onChange={(e) => void apply({ theme: e.target.value })}>
                {THEMES.map((t) => (
                  <option key={t.value} value={t.value}>{t.label}</option>
                ))}
              </Select>
            </Field>
            <Switch
              checked={settings.reducedMotion}
              onChange={(v) => void apply({ reducedMotion: v })}
              label={t("reducedMotion", lang)}
            />
            <Switch
              checked={settings.highContrast}
              onChange={(v) => void apply({ highContrast: v })}
              label={t("highContrast", lang)}
            />
            <Field label={`${t("fontScale", lang)} (${settings.fontScale}%)`}>
              <input
                type="range"
                min={80}
                max={150}
                step={5}
                value={settings.fontScale}
                onChange={(e) => void apply({ fontScale: Number(e.target.value) })}
                className="w-full"
              />
            </Field>
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="mb-1 font-semibold text-slate-800 dark:text-slate-100">{t("assistantVoiceSection", lang)}</h2>
          <p className="mb-4 text-sm text-slate-500">{t("assistantVoiceSub", lang)}</p>
          <div className="space-y-4">
            <Switch
              checked={settings.wakeWordEnabled}
              onChange={(v) => void apply({ wakeWordEnabled: v })}
              label={t("enableWakeWord", lang)}
              description={t("wakeWordHint", lang)}
            />
            <Field label={t("wakeWordField", lang)}>
              <Input
                value={settings.wakeWord}
                disabled={!settings.wakeWordEnabled}
                onChange={(e) => void apply({ wakeWord: e.target.value })}
              />
            </Field>
            <Switch
              checked={settings.autoDetectLanguage}
              onChange={(v) => void apply({ autoDetectLanguage: v })}
              label={t("autoDetect", lang)}
            />
            <Switch
              checked={settings.muteAssistantVoice}
              onChange={(v) => void apply({ muteAssistantVoice: v })}
              label={t("muteVoice", lang)}
            />
            <Field label={t("speechSpeed", lang)}>
              <input
                type="range"
                min={0.5}
                max={2}
                step={0.1}
                value={settings.speechSpeed}
                onChange={(e) => void apply({ speechSpeed: Number(e.target.value) })}
                className="w-full"
              />
            </Field>
            <Field label={t("voiceVolume", lang)}>
              <input
                type="range"
                min={0}
                max={1}
                step={0.1}
                value={settings.voiceVolume}
                onChange={(e) => void apply({ voiceVolume: Number(e.target.value) })}
                className="w-full"
              />
            </Field>
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="mb-1 font-semibold text-slate-800 dark:text-slate-100">{t("notificationsSection", lang)}</h2>
          <p className="mb-4 text-sm text-slate-500">{t("notificationsSub", lang)}</p>
          <div className="space-y-4">
            <Switch
              checked={settings.notificationsEnabled}
              onChange={(v) => void apply({ notificationsEnabled: v })}
              label={t("enableNotifications", lang)}
            />
            <Field label={t("defaultReminderLabel", lang)}>
              <Input
                type="number"
                min={0}
                value={settings.defaultReminderMinutes}
                onChange={(e) => void apply({ defaultReminderMinutes: Number(e.target.value) })}
              />
            </Field>
            <Field label={t("timezoneField", lang)}>
              <Input value={settings.timeZone} onChange={(e) => void apply({ timeZone: e.target.value })} />
            </Field>
            <Switch
              checked={settings.confirmationMode}
              onChange={(v) => void apply({ confirmationMode: v })}
              label={t("confirmActions", lang)}
              description={t("confirmActionsHint", lang)}
            />
          </div>
        </Card>

        <Card className="p-5">
          <h2 className="mb-1 font-semibold text-slate-800 dark:text-slate-100">{t("accountSection", lang)}</h2>
          <p className="mb-4 text-sm text-slate-500">{user?.email}</p>
          <div className="space-y-4">
            <Field label={t("firstName", lang)}>
              <Input value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </Field>
            <Field label={t("lastName", lang)}>
              <Input value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </Field>
            <Button type="button" onClick={() => void saveProfile()} loading={saving}>
              {t("saveProfile", lang)}
            </Button>
          </div>
        </Card>
      </div>

      <div className="mt-4 flex items-center justify-end gap-3">
        {saving && <Spinner className="h-4 w-4" />}
        {saved && <span className="text-sm text-emerald-600">{t("saved", lang)}</span>}
      </div>
    </PageShell>
  );
}
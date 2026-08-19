import { useState, type FormEvent } from "react";
import { Send, Mic, MicOff, Volume2, VolumeX, AlertCircle, Maximize2 } from "lucide-react";
import { useAssistant, type AssistantStatus } from "../contexts/AssistantContext";
import { useSettings } from "../contexts/SettingsContext";
import { MicOrb } from "./MicOrb";
import { t, languageName } from "../utils/locale";
import { todayString } from "../utils/date";
import { Button } from "./ui";

const STATUS_TEXT: Record<AssistantStatus, string> = {
  idle: "idle",
  listening: "listening",
  processing: "processing",
  speaking: "speaking",
  error: "error",
  "wake-listening": "listening"
};

const CATEGORIES = [
  { key: "categoryWork", value: "Work" },
  { key: "categoryPersonal", value: "Personal" },
  { key: "categoryShopping", value: "Shopping" },
  { key: "categoryStudy", value: "Study" },
  { key: "categoryHealth", value: "Health" },
  { key: "categoryFinance", value: "Finance" },
  { key: "categoryTravel", value: "Travel" },
  { key: "categoryOther", value: "Other" }
];

const SECTIONS = [
  { key: "sectionNotes", value: "Notes" },
  { key: "sectionTasks", value: "Tasks" },
  { key: "sectionAppointments", value: "Appointments" },
  { key: "sectionReminders", value: "Reminders" }
];

type CaptureKind = "date" | "category" | "section" | "time";

export function AssistantPanel() {
  const assistant = useAssistant();
  const { settings } = useSettings();
  const [input, setInput] = useState("");

  const lang = settings.language;
  const uiLang = lang.toLowerCase() === "hi" ? "hi" : lang.toLowerCase() === "te" ? "te" : "en";

  const handleMic = () => {
    if (assistant.status === "listening") {
      assistant.stopListening();
    } else {
      void assistant.startListening();
    }
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!input.trim()) return;
    const value = input;
    setInput("");
    void assistant.sendText(value);
  };

  const statusKey = STATUS_TEXT[assistant.status];

  const captureTitle = (type: CaptureKind) =>
    type === "date"
      ? t("pickDate", uiLang)
      : type === "time"
        ? t("pickTime", uiLang)
        : type === "section"
          ? t("pickSection", uiLang)
          : t("pickCategory", uiLang);

  return (
    <div className="flex flex-col items-center px-4 py-6">
      {/* Status text */}
      <div className="mb-1 flex w-full items-center justify-between">
        <p className="text-sm font-semibold text-brand-700 dark:text-brand-300" aria-live="polite">
          {t(statusKey, uiLang)}
        </p>
        <button
          onClick={assistant.openVoiceOverlay}
          aria-label={t("fullscreen", uiLang)}
          title={t("fullscreen", uiLang)}
          className="btn-ghost rounded-lg p-2 text-slate-400 transition hover:bg-slate-100 hover:text-brand-600 dark:hover:bg-slate-800 dark:hover:text-brand-300"
        >
          <Maximize2 className="h-4 w-4" />
        </button>
      </div>
      <p className="mb-6 text-xs font-medium text-slate-400 dark:text-slate-500">
        {assistant.wakeListening ? `${t("wakeActive", uiLang)} · ` : ""}
        {t("say", uiLang)}
      </p>

      {/* Orb */}
      <div className="relative mb-6 flex flex-col items-center">
        <MicOrb
          status={assistant.status}
          onClick={handleMic}
          disabled={!assistant.micSupported}
          size="lg"
        />
      </div>

      {/* Error */}
      {assistant.errorMessage && (
        <div className="mb-4 flex items-start gap-2 rounded-xl bg-rose-50 px-3 py-2 text-xs text-rose-600 dark:bg-rose-900/20 dark:text-rose-300">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{assistant.errorMessage}</span>
        </div>
      )}

      {/* Confirmation prompt */}
      {assistant.confirmation && (
        <div className="glass-card mb-4 w-full max-w-md p-4 text-center">
          <p className="mb-3 text-sm font-medium text-slate-700 dark:text-slate-200">{assistant.confirmation.text}</p>
          <div className="flex justify-center gap-3">
            <Button onClick={() => void assistant.answerConfirmation(true)}>
              {t("yes", uiLang)}
            </Button>
            <Button variant="secondary" onClick={() => void assistant.answerConfirmation(false)}>
              {t("no", uiLang)}
            </Button>
          </div>
        </div>
      )}

      {/* Guided capture: date picker, time picker, section chips or category chips */}
      {assistant.capture && (
        <div className="glass-card mb-4 w-full max-w-md p-4">
          <p className="mb-3 text-center text-sm font-medium text-slate-700 dark:text-slate-200">
            {captureTitle(assistant.capture.type)}
          </p>
          {assistant.capture.type === "date" && (
            <div className="flex flex-col items-center gap-3">
              <input
                type="date"
                defaultValue={todayString()}
                className="input w-full"
                aria-label={t("pickDate", uiLang)}
                id="assistant-date-picker"
              />
              <div className="flex w-full gap-2">
                <Button
                  className="flex-1"
                  onClick={() => {
                    const el = document.getElementById("assistant-date-picker") as HTMLInputElement | null;
                    const value = el?.value ?? todayString();
                    void assistant.sendText(value);
                  }}
                >
                  {t("save", uiLang)}
                </Button>
                <Button variant="secondary" className="flex-1" onClick={() => void assistant.sendText("skip")}>
                  {t("skip", uiLang)}
                </Button>
              </div>
            </div>
          )}
          {assistant.capture.type === "time" && (
            <div className="flex flex-col items-center gap-3">
              <input
                type="time"
                defaultValue="09:00"
                className="input w-full"
                aria-label={t("pickTime", uiLang)}
                id="assistant-time-picker"
              />
              <div className="flex w-full gap-2">
                <Button
                  className="flex-1"
                  onClick={() => {
                    const el = document.getElementById("assistant-time-picker") as HTMLInputElement | null;
                    const value = el?.value ?? "09:00";
                    void assistant.sendText(value);
                  }}
                >
                  {t("save", uiLang)}
                </Button>
                <Button variant="secondary" className="flex-1" onClick={() => void assistant.sendText("skip")}>
                  {t("skip", uiLang)}
                </Button>
              </div>
            </div>
          )}
          {assistant.capture.type === "section" && (
            <div className="flex flex-wrap justify-center gap-2">
              {SECTIONS.map((s) => (
                <button
                  key={s.value}
                  className="rounded-full border border-brand-200 bg-brand-50 px-4 py-1.5 text-sm font-medium text-brand-700 transition hover:bg-brand-100 dark:border-brand-800 dark:bg-brand-900/30 dark:text-brand-300 dark:hover:bg-brand-900/60"
                  onClick={() => void assistant.sendText(s.value)}
                >
                  {t(s.key, uiLang)}
                </button>
              ))}
              <button
                className="rounded-full border border-slate-200 px-4 py-1.5 text-sm font-medium text-slate-500 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-400 dark:hover:bg-slate-800"
                onClick={() => void assistant.sendText("skip")}
              >
                {t("skip", uiLang)}
              </button>
            </div>
          )}
          {assistant.capture.type === "category" && (
            <div className="flex flex-wrap justify-center gap-2">
              {CATEGORIES.map((c) => (
                <button
                  key={c.value}
                  className="rounded-full border border-brand-200 bg-brand-50 px-4 py-1.5 text-sm font-medium text-brand-700 transition hover:bg-brand-100 dark:border-brand-800 dark:bg-brand-900/30 dark:text-brand-300 dark:hover:bg-brand-900/60"
                  onClick={() => void assistant.sendText(c.value)}
                >
                  {t(c.key, uiLang)}
                </button>
              ))}
              <button
                className="rounded-full border border-slate-200 px-4 py-1.5 text-sm font-medium text-slate-500 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-400 dark:hover:bg-slate-800"
                onClick={() => void assistant.sendText("skip")}
              >
                {t("skip", uiLang)}
              </button>
            </div>
          )}
        </div>
      )}

      {/* Recent conversation */}
      {assistant.messages.length > 0 && (
        <div className="mb-4 max-h-44 w-full max-w-md space-y-2 overflow-y-auto">
          {assistant.messages.slice(-6).map((m, i) => (
            <div
              key={i}
              className={`max-w-[85%] rounded-2xl px-3 py-2 text-sm ${
                m.role === "user"
                  ? "ml-auto bg-brand-600 text-white"
                  : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200"
              }`}
            >
              {m.text}
            </div>
          ))}
        </div>
      )}

      {/* Text input fallback */}
      <form onSubmit={handleSubmit} className="flex w-full max-w-md items-center gap-2">
        <input
          className="input flex-1"
          placeholder={t("typePlaceholder", uiLang)}
          value={input}
          onChange={(e) => setInput(e.target.value)}
          aria-label="Assistant command input"
        />
        <Button type="submit" aria-label="Send">
          <Send className="h-4 w-4" />
        </Button>
        <Button variant="ghost" type="button" aria-label="Microphone">
          {assistant.micSupported ? <Mic className="h-5 w-5" /> : <MicOff className="h-5 w-5" />}
        </Button>
      </form>

      <div className="mt-3 flex items-center gap-1 text-[11px] text-slate-400">
        {settings.muteAssistantVoice ? <VolumeX className="h-3 w-3" /> : <Volume2 className="h-3 w-3" />}
        {settings.muteAssistantVoice ? t("voiceMuted", uiLang) : languageName(settings.language)}
      </div>
    </div>
  );
}
import { useState, type FormEvent } from "react";
import { Send, Mic, MicOff, Volume2, VolumeX, AlertCircle } from "lucide-react";
import { useAssistant, type AssistantStatus } from "../contexts/AssistantContext";
import { useSettings } from "../contexts/SettingsContext";
import { MicOrb } from "./MicOrb";
import { t, languageName } from "../utils/locale";
import { Button } from "./ui";

const STATUS_TEXT: Record<AssistantStatus, string> = {
  idle: "idle",
  listening: "listening",
  processing: "processing",
  speaking: "speaking",
  error: "error",
  "wake-listening": "listening"
};

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

  return (
    <div className="flex flex-col items-center px-4 py-6">
      {/* Status text */}
      <p className="mb-1 text-sm font-semibold text-brand-700 dark:text-brand-300" aria-live="polite">
        {t(statusKey, uiLang)}
      </p>
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
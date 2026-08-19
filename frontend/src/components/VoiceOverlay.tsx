import { useEffect, useRef, useState } from "react";
import { X, Mic, MicOff, Volume2, VolumeX } from "lucide-react";
import { useAssistant, type AssistantStatus } from "../contexts/AssistantContext";
import { useSettings } from "../contexts/SettingsContext";
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

const ORB_COLORS: Record<AssistantStatus, string> = {
  idle: "from-brand-500 via-brand-400 to-sky-400",
  listening: "from-rose-500 via-pink-500 to-fuchsia-500",
  processing: "from-amber-400 via-orange-400 to-rose-400",
  speaking: "from-emerald-500 via-teal-400 to-sky-400",
  error: "from-rose-600 via-rose-500 to-red-400",
  "wake-listening": "from-brand-400 via-indigo-400 to-violet-400"
};

function Waveform({ active, speaking }: { active: boolean; speaking: boolean }) {
  const bars = Array.from({ length: 32 }, (_, i) => i);
  return (
    <div className="flex h-20 items-center justify-center gap-1" aria-hidden="true">
      {bars.map((i) => (
        <span
          key={i}
          className={`w-1.5 rounded-full transition-all duration-200 ${
            speaking ? "bg-emerald-300/90" : "bg-rose-300/90"
          }`}
          style={{
            height: active ? `${18 + ((i * 37) % 70)}px` : "8px",
            animation: active ? `orb-wave 1.1s ease-in-out ${(i % 7) * 0.12}s infinite` : "none"
          }}
        />
      ))}
      <style>{`
        @keyframes orb-wave {
          0%, 100% { transform: scaleY(0.35); }
          50% { transform: scaleY(1); }
        }
      `}</style>
    </div>
  );
}

function TranscriptBubble({ role, text }: { role: "user" | "assistant"; text: string }) {
  return (
    <div
      className={`max-w-[85%] rounded-2xl px-4 py-2 text-sm shadow-sm ${
        role === "user"
          ? "ml-auto rounded-br-md bg-brand-600 text-white"
          : "mr-auto rounded-bl-md bg-white/95 text-slate-700 dark:bg-slate-800/95 dark:text-slate-100"
      }`}
    >
      {text}
    </div>
  );
}

export function VoiceOverlay() {
  const assistant = useAssistant();
  const { settings } = useSettings();
  const [showTranscript, setShowTranscript] = useState(true);
  const transcriptRef = useRef<HTMLDivElement | null>(null);
  const lastStatusRef = useRef<AssistantStatus>(assistant.status);

  const lang = settings.language;
  const uiLang = lang.toLowerCase() === "hi" ? "hi" : lang.toLowerCase() === "te" ? "te" : "en";
  const statusKey = STATUS_TEXT[assistant.status];
  const listening = assistant.status === "listening" || assistant.status === "wake-listening";
  const animate = assistant.status !== "idle";

  // Auto-start listening the moment the overlay opens (voice-first, like Siri).
  useEffect(() => {
    if (assistant.voiceOverlayOpen && assistant.micSupported && assistant.status === "idle" && assistant.messages.length === 0) {
      void assistant.startListening();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [assistant.voiceOverlayOpen]);

  // Continuous dialog: after the assistant finishes speaking, listen again.
  useEffect(() => {
    if (!assistant.voiceOverlayOpen) return;
    const prev = lastStatusRef.current;
    lastStatusRef.current = assistant.status;
    const justStoppedSpeaking = prev === "speaking" && assistant.status === "idle";
    if (justStoppedSpeaking && assistant.micSupported && !assistant.confirmation && !assistant.capture) {
      void assistant.startListening();
    }
  }, [assistant.status, assistant.voiceOverlayOpen, assistant.micSupported, assistant.confirmation, assistant.capture, assistant.startListening]);

  useEffect(() => {
    transcriptRef.current?.scrollTo?.({ top: transcriptRef.current.scrollHeight, behavior: "smooth" });
  }, [assistant.messages.length, showTranscript]);

  if (!assistant.voiceOverlayOpen) return null;

  const handleMicTap = () => {
    if (listening) {
      assistant.stopListening();
    } else {
      void assistant.startListening();
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex flex-col overflow-hidden bg-gradient-to-b from-brand-950/95 via-slate-950/95 to-slate-950/95 backdrop-blur-xl"
      role="dialog"
      aria-modal="true"
      aria-label="Assistant"
    >
      {/* Top bar */}
      <div className="flex items-center justify-between px-5 pt-4">
        <button
          className="btn-ghost flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-white/10"
          onClick={() => setShowTranscript((v) => !v)}
        >
          {t("transcript", uiLang)}
        </button>
        <button
          className="btn-ghost flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-white/10"
          onClick={assistant.closeVoiceOverlay}
          aria-label={t("closeAssistant", uiLang)}
        >
          <X className="h-4 w-4" />
          {t("close", uiLang)}
        </button>
      </div>

      {/* Orb */}
      <div className="flex flex-1 flex-col items-center justify-center px-6">
        <div className="relative flex h-64 w-64 items-center justify-center" onClick={handleMicTap}>
          {animate &&
            ["h-72 w-72", "h-60 w-60", "h-48 w-48"].map((cls, i) => (
              <span
                key={i}
                className={`absolute rounded-full bg-gradient-to-br opacity-30 ${ORB_COLORS[assistant.status]} ${cls} animate-ping-slow`}
                style={{ animationDelay: `${i * 350}ms`, animationDuration: "2.4s" }}
              />
            ))}

          <div
            className={`relative z-10 flex h-56 w-56 items-center justify-center rounded-full bg-gradient-to-br shadow-[0_0_80px_rgba(255,255,255,0.35)] transition-transform duration-300 ${ORB_COLORS[assistant.status]} ${
              animate ? "scale-105" : "scale-100"
            }`}
          >
            {assistant.micSupported ? (
              <Mic className={`h-16 w-16 text-white ${listening ? "animate-pulse" : ""}`} />
            ) : (
              <MicOff className="h-16 w-16 text-white/70" />
            )}
          </div>
        </div>

        {/* Status */}
        <p className="mt-6 text-center text-lg font-semibold text-slate-100" aria-live="polite">
          {t(statusKey, uiLang)}
        </p>
        {assistant.errorMessage && (
          <p className="mt-2 max-w-xs text-center text-xs text-rose-300">{assistant.errorMessage}</p>
        )}

        {/* Voice indicator */}
        <div className="mt-2 flex items-center gap-1.5 text-xs font-medium text-slate-400">
          {settings.muteAssistantVoice ? <VolumeX className="h-3.5 w-3.5" /> : <Volume2 className="h-3.5 w-3.5" />}
          {settings.muteAssistantVoice ? t("voiceMuted", uiLang) : languageName(settings.language)}
        </div>

        {/* Waveform */}
        <div className="mt-6 h-20">
          <Waveform active={listening} speaking={assistant.status === "speaking"} />
        </div>
      </div>

      {/* Capture / confirmation / transcript */}
      <div className="flex flex-col gap-3 px-6 pb-10">
        {assistant.confirmation && (
          <div className="mx-auto w-full max-w-md rounded-2xl bg-white/95 p-4 text-center shadow-xl dark:bg-slate-800/95">
            <p className="mb-3 text-sm font-medium text-slate-700 dark:text-slate-100">{assistant.confirmation.text}</p>
            <div className="flex justify-center gap-3">
              <Button onClick={() => void assistant.answerConfirmation(true)}>{t("yes", uiLang)}</Button>
              <Button variant="secondary" onClick={() => void assistant.answerConfirmation(false)}>{t("no", uiLang)}</Button>
            </div>
          </div>
        )}

        {assistant.capture && (
          <div className="mx-auto w-full max-w-md rounded-2xl bg-white/95 p-4 shadow-xl dark:bg-slate-800/95">
            <p className="mb-3 text-center text-sm font-medium text-slate-700 dark:text-slate-100">
              {assistant.capture.type === "date" ? t("pickDate", uiLang) : t("pickCategory", uiLang)}
            </p>
            {assistant.capture.type === "date" ? (
              <div className="flex flex-col items-center gap-3">
                <input type="date" defaultValue={todayString()} aria-label={t("pickDate", uiLang)} id="assistant-date-picker" className="input w-full" />
                <div className="flex w-full gap-2">
                  <Button
                    className="flex-1"
                    onClick={() => {
                      const el = document.getElementById("assistant-date-picker") as HTMLInputElement | null;
                      void assistant.sendText(el?.value ?? todayString());
                    }}
                  >
                    {t("save", uiLang)}
                  </Button>
                  <Button variant="secondary" className="flex-1" onClick={() => void assistant.sendText("skip")}>
                    {t("skip", uiLang)}
                  </Button>
                </div>
              </div>
            ) : (
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

        {showTranscript && assistant.messages.length > 0 && (
          <div
            ref={transcriptRef}
            className="mx-auto flex max-h-44 w-full max-w-md flex-col gap-2 overflow-y-auto px-1"
          >
            {assistant.messages.slice(-8).map((m, i) => (
              <TranscriptBubble key={i} role={m.role} text={m.text} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { assistantApi } from "../api/endpoints";
import { useSpeechRecognition } from "../hooks/useSpeechRecognition";
import { useSpeechSynthesis } from "../hooks/useSpeechSynthesis";
import { useWakeWord } from "../hooks/useWakeWord";
import { useSettings } from "./SettingsContext";

export type AssistantStatus =
  | "idle"
  | "listening"
  | "processing"
  | "speaking"
  | "error"
  | "wake-listening";

export interface AssistantMessage {
  role: "user" | "assistant";
  text: string;
  timestamp: number;
}

interface AssistantContextValue {
  status: AssistantStatus;
  messages: AssistantMessage[];
  micSupported: boolean;
  errorMessage: string | null;
  confirmation: { text: string; pendingAction?: string } | null;
  capture: { type: "date" | "category" } | null;
  wakeListening: boolean;
  wakeEnabled: boolean;
  speaking: boolean;
  voiceOverlayOpen: boolean;
  openVoiceOverlay: () => void;
  closeVoiceOverlay: () => void;
  toggleWakeWord: () => void;
  startListening: () => Promise<void>;
  stopListening: () => void;
  sendText: (text: string) => Promise<void>;
  answerConfirmation: (ok: boolean) => Promise<void>;
  clearMessages: () => void;
  sessionId: string;
}

const AssistantContext = createContext<AssistantContextValue | undefined>(undefined);

const WAKE_REPLY: Record<string, string> = {
  en: "Yes, how can I help you?",
  hi: "जी, मैं आपकी कैसे सहायता कर सकता हूँ?",
  te: "అవును, నేను మీకు ఎలా సహాయం చేయగలను?"
};

function useSessionId() {
  const [id] = useState(() =>
    typeof crypto !== "undefined" && "randomUUID" in crypto
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`
  );
  return id;
}

export function AssistantProvider({ children }: { children: ReactNode }) {
  const { settings, setLocal } = useSettings();
  const rec = useSpeechRecognition(15000);
  const tts = useSpeechSynthesis();

  const sessionId = useSessionId();
  const [status, setStatus] = useState<AssistantStatus>("idle");
  const [messages, setMessages] = useState<AssistantMessage[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [confirmation, setConfirmation] = useState<{ text: string; pendingAction?: string } | null>(null);
  const [capture, setCapture] = useState<{ type: "date" | "category" } | null>(null);
  const [wakeEnabled, setWakeEnabled] = useState(settings.wakeWordEnabled);
  const [voiceOverlayOpen, setVoiceOverlayOpen] = useState(false);

  const statusRef = useRef<AssistantStatus>("idle");
  statusRef.current = status;

  const pushMessage = useCallback((role: "user" | "assistant", text: string) => {
    setMessages((prev) => [...prev.slice(-39), { role, text, timestamp: Date.now() }]);
  }, []);

  // ---------------- Commands ----------------
  async function sendToBackend(text: string, isVoice: boolean) {
    const payload = {
      text,
      language: settings.autoDetectLanguage ? "Auto" : settings.language,
      sessionId,
      isVoice
    };
    return assistantApi.command(payload);
  }

  const speakReply = useCallback(
    async (reply: string, lang?: string) => {
      return new Promise<void>((resolve) => {
        const speakLang = lang ?? settings.language;
        if (!tts.supported || settings.muteAssistantVoice) {
          resolve();
          return;
        }
        tts.speak(reply, speakLang, {
          rate: settings.speechSpeed,
          volume: settings.voiceVolume,
          muted: settings.muteAssistantVoice
        });
        const check = setInterval(() => {
          if (!tts.speaking) {
            clearInterval(check);
            resolve();
          }
        }, 200);
        setTimeout(() => {
          clearInterval(check);
          resolve();
        }, 20000);
      });
    },
    [tts, settings]
  );

  const text = useCallback(
    async (input: string, isVoice = false) => {
      const trimmed = input.trim();
      if (!trimmed) return;
      setStatus("processing");
      setErrorMessage(null);
      setConfirmation(null);
      setCapture(null);
      pushMessage("user", trimmed);
      try {
        const res = await sendToBackend(trimmed, isVoice);
        let replyLang: string | undefined;
        if (res.language) {
          const detected = res.language.toLowerCase();
          const uiLang = detected.startsWith("hi") ? "hi" : detected.startsWith("te") ? "te" : "en";
          replyLang = uiLang;
          const isLangSwitch = res.intent === "ChangeLanguage";
          const shouldSwitchUi = isLangSwitch
            ? uiLang !== settings.language
            : settings.autoDetectLanguage && (uiLang === "hi" || uiLang === "te") && uiLang !== settings.language;
          if (shouldSwitchUi) {
            setLocal({ language: uiLang });
          }
        }
        const reply = res.reply ?? "Done.";
        pushMessage("assistant", reply);
        if (res.captureType === "date" || res.captureType === "category") {
          setCapture({ type: res.captureType });
          setConfirmation(null);
          setStatus("idle");
        } else {
          setCapture(null);
          if (res.needsConfirmation) {
            setConfirmation({ text: res.confirmationPrompt ?? reply, pendingAction: res.pendingAction });
            setStatus("idle");
          } else {
            setStatus("speaking");
            await speakReply(res.ttsText ?? reply, replyLang);
            setStatus("idle");
          }
        }
      } catch (err) {
        const msg = err instanceof Error ? err.message : "Request failed";
        setErrorMessage(msg);
        setStatus("error");
        pushMessage("assistant", `⚠️ ${msg}`);
        setTimeout(() => setStatus("idle"), 2500);
      }
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [sessionId, settings, pushMessage]
  );

  // ---------------- Voice capture ----------------
  const startListening = useCallback(async () => {
    if (!rec.supported) {
      setErrorMessage("Speech recognition is not supported in this browser");
      setStatus("error");
      return;
    }

    setStatus("listening");
    setErrorMessage(null);
    try {
      const transcript = await rec.start(settings.language);
      if (transcript.trim()) {
        await text(transcript.replace(/^assistant\s*[,:.]?\s*/i, ""), true);
      } else if (statusRef.current === "listening") {
        setStatus("idle");
      }
    } catch (e) {
      const err = e as Error;
      setErrorMessage(rec.error === "not-allowed" ? "Microphone permission denied." : `Listening failed: ${err.message}`);
      setStatus("error");
      setTimeout(() => setStatus("idle"), 2500);
    } finally {
      rec.reset();
    }
  }, [rec, settings.language, settings.wakeWordEnabled]);

  const stopListening = useCallback(() => {
    rec.stop();
    if (statusRef.current === "listening") {
      setStatus("idle");
    }
  }, [rec]);

  // ---------------- Wake word ----------------
  const onWakeWordDetected = useCallback(
    async (event: { wakeWord: string; transcript: string }) => {
      const reply = WAKE_REPLY[settings.language] ?? WAKE_REPLY.en;
      pushMessage("assistant", reply);
      setStatus("speaking");
      await speakReply(reply, settings.language);

      if (event.transcript) {
        await text(event.transcript, true);
      } else {
        await startListening();
      }
    },
    [settings, pushMessage, speakReply, text, startListening]
  );

  const wake = useWakeWord(
    settings.wakeWord ?? "assistant",
    wakeEnabled && (status === "idle" || status === "wake-listening"),
    onWakeWordDetected
  );

  // Surface a clear message when the wake word is requested but this browser
  // cannot listen continuously (Firefox/Safari lack Web Speech Recognition),
  // or when the microphone permission hasn't been granted.
  useEffect(() => {
    if (wakeEnabled && !wake.supported) {
      setErrorMessage("This browser doesn't support always-on wake word detection. Use the mic button or text input instead.");
    } else if (wake.error) {
      setErrorMessage(wake.error);
    }
  }, [wakeEnabled, wake.supported, wake.error]);

  // ---------------- Confirmation ----------------
  const answerConfirmation = useCallback(
    async (ok: boolean) => {
      const prompt = confirmation?.text ?? "Should I continue?";
      setConfirmation(null);
      pushMessage("user", ok ? "Yes" : "No");
      await text(ok ? "Yes" : "No");
    },
    [confirmation, text, pushMessage]
  );

  const sendText = useCallback(
    async (input: string) => {
      await text(input, false);
    },
    [text]
  );

  const toggleWakeWord = useCallback(() => {
    setWakeEnabled((prev) => {
      const next = !prev;
      setLocal({ wakeWordEnabled: next });
      return next;
    });
  }, [setLocal]);

  const clearMessages = useCallback(() => setMessages([]), []);

  const openVoiceOverlay = useCallback(() => {
    setVoiceOverlayOpen(true);
  }, []);

  const closeVoiceOverlay = useCallback(() => {
    setVoiceOverlayOpen(false);
    setConfirmation(null);
    setCapture(null);
  }, []);

  useEffect(() => {
    setWakeEnabled(settings.wakeWordEnabled);
  }, [settings.wakeWordEnabled]);

  const errorMessageFor = useMemo(() => errorMessage, [errorMessage]);

  const value = useMemo<AssistantContextValue>(
    () => ({
      status,
      messages,
      micSupported: rec.supported,
      errorMessage: errorMessageFor,
      confirmation,
      capture,
      wakeListening: wake.state === "listening",
      wakeEnabled,
      speaking: tts.speaking,
      voiceOverlayOpen,
      openVoiceOverlay,
      closeVoiceOverlay,
      toggleWakeWord,
      startListening,
      stopListening,
      sendText,
      answerConfirmation,
      clearMessages,
      sessionId
    }),
    [status, messages, errorMessageFor, confirmation, capture, wakeEnabled, wake.state, tts.speaking, voiceOverlayOpen, openVoiceOverlay, closeVoiceOverlay, toggleWakeWord, startListening, stopListening, sendText, answerConfirmation, clearMessages, sessionId]
  );

  return <AssistantContext.Provider value={value}>{children}</AssistantContext.Provider>;
}

export function useAssistant() {
  const ctx = useContext(AssistantContext);
  if (!ctx) {
    throw new Error("useAssistant must be used within AssistantProvider");
  }
  return ctx;
}
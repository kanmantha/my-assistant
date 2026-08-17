import { useEffect, useRef, useState, useCallback } from "react";

export interface TtsOptions {
  rate?: number;
  volume?: number;
  muted?: boolean;
  voice?: string;
}

export interface UseSpeechSynthesisResult {
  supported: boolean;
  speaking: boolean;
  speak: (text: string, lang?: string, opts?: TtsOptions) => void;
  cancel: () => void;
  voices: SpeechSynthesisVoice[];
}

const LANG_MAP: Record<string, string> = {
  en: "en-IN",
  hi: "hi-IN",
  te: "te-IN"
};

function toSpeechLang(lang: string): string {
  return LANG_MAP[lang] ?? (lang.includes("-") ? lang : `en-${lang.toUpperCase()}`);
}

/**
 * Text-to-Speech via the browser SpeechSynthesis API. A provider abstraction
 * (swap this hook for Azure/Google TTS later) keeps the assistant decoupled
 * from any single speech vendor.
 */
export function useSpeechSynthesis(): UseSpeechSynthesisResult {
  const [supported] = useState<boolean>(() => typeof window !== "undefined" && "speechSynthesis" in window);
  const [speaking, setSpeaking] = useState(false);
  const [voices, setVoices] = useState<SpeechSynthesisVoice[]>([]);

  useEffect(() => {
    if (!supported) return;
    const load = () => setVoices(window.speechSynthesis.getVoices());
    load();
    window.speechSynthesis.addEventListener("voiceschanged", load);
    return () => window.speechSynthesis.removeEventListener("voiceschanged", load);
  }, [supported]);

  const cancel = useCallback(() => {
    if (supported) {
      window.speechSynthesis.cancel();
    }
    setSpeaking(false);
  }, [supported]);

  const speak = useCallback(
    (text: string, lang = "en", opts: TtsOptions = {}) => {
      if (!supported || opts.muted) return;
      window.speechSynthesis.cancel();
      const utterance = new SpeechSynthesisUtterance(text);
      const targetLang = toSpeechLang(lang);

      const preferred = voices.find((v) => v.lang.toLowerCase() === targetLang.toLowerCase());
      if (preferred) {
        utterance.voice = preferred;
      }
      utterance.lang = preferred?.lang ?? targetLang;
      utterance.rate = opts.rate ?? 1;
      utterance.volume = opts.volume ?? 1;

      utterance.onstart = () => setSpeaking(true);
      utterance.onend = () => setSpeaking(false);
      utterance.onerror = () => setSpeaking(false);

      window.speechSynthesis.speak(utterance);
    },
    [voices]
  );

  useEffect(() => {
    return cancel;
  }, [cancel]);

  return { supported, speaking, speak, cancel, voices };
}
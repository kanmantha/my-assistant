import { useEffect, useRef, useState, useCallback } from "react";

export type SpeechError = "not-supported" | "not-allowed" | "no-speech" | "audio-capture" | "network" | "aborted" | "unknown";

export interface UseSpeechRecognitionResult {
  supported: boolean;
  listening: boolean;
  interim: string;
  final: string;
  error: SpeechError | null;
  start: (lang?: string) => Promise<string>;
  stop: () => void;
  reset: () => void;
}

const LANG_MAP: Record<string, string> = {
  en: "en-IN",
  hi: "hi-IN",
  te: "te-IN"
};

function toSpeechLang(lang: string): string {
  return LANG_MAP[lang] ?? (lang.includes("-") ? lang : `en-${lang.toUpperCase()}`);
}

const SILENCE_TIMEOUT_MS = 10000;

/**
 * Thin wrapper over the browser Web Speech API (SpeechRecognition).
 * Used for both push-to-talk and wake-word monitoring. Audio never leaves
 * the browser with this provider, which keeps microphone activity private.
 */
export function useSpeechRecognition(timeoutMs = SILENCE_TIMEOUT_MS): UseSpeechRecognitionResult {
  const [supported] = useState<boolean>(() => typeof window !== "undefined" && !!(window.SpeechRecognition || window.webkitSpeechRecognition));
  const [listening, setListening] = useState(false);
  const [interim, setInterim] = useState("");
  const [final, setFinal] = useState("");
  const [error, setError] = useState<SpeechError | null>(null);

  const recRef = useRef<SpeechRecognition | null>(null);
  const watchdogRef = useRef<number | null>(null);

  const clearWatchdog = () => {
    if (watchdogRef.current !== null) {
      window.clearTimeout(watchdogRef.current);
      watchdogRef.current = null;
    }
  };

  const armWatchdog = () => {
    clearWatchdog();
    watchdogRef.current = window.setTimeout(() => {
      recRef.current?.stop();
      setListening(false);
      setError("no-speech");
    }, timeoutMs);
  };

  const stop = useCallback(() => {
    clearWatchdog();
    recRef.current?.stop();
    setListening(false);
  }, []);

  const reset = useCallback(() => {
    setInterim("");
    setFinal("");
    setError(null);
  }, []);

  useEffect(() => {
    return () => {
      clearWatchdog();
      recRef.current?.abort();
    };
  }, []);

  const start = useCallback(
    (lang = "en") => {
      return new Promise<string>((resolve, reject) => {
        const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!Recognition) {
          setError("not-supported");
          reject(new Error("Speech recognition not supported in this browser"));
          return;
        }

        setError(null);
        setInterim("");
        let collected = "";

        const rec = new Recognition();
        rec.lang = toSpeechLang(lang);
        rec.continuous = true;
        rec.interimResults = true;
        rec.maxAlternatives = 1;

        rec.onstart = () => {
          setListening(true);
          armWatchdog();
        };

        rec.onresult = (event) => {
          armWatchdog();
          let interimText = "";
          let finalText = "";
          for (let i = event.resultIndex; i < event.results.length; i += 1) {
            const result = event.results[i];
            const transcript = result[0].transcript;
            if (result.isFinal) {
              finalText += transcript;
            } else {
              interimText += transcript;
            }
          }
          if (finalText) {
            collected = collected ? `${collected} ${finalText}` : finalText;
            setFinal((prev) => (prev ? `${prev} ${finalText}` : finalText).trim());
          }
          setInterim(interimText);
        };

        rec.onerror = (event) => {
          setListening(false);
          clearWatchdog();
          switch (event.error) {
            case "not-allowed":
            case "service-not-allowed":
              setError("not-allowed");
              break;
            case "no-speech":
              setError("no-speech");
              break;
            case "audio-capture":
              setError("audio-capture");
              break;
            case "network":
              setError("network");
              break;
            case "aborted":
              setError("aborted");
              break;
            default:
              setError("unknown");
          }
          if (event.error === "aborted") {
            resolve(collected.trim());
          } else {
            reject(new Error(event.error));
          }
        };

        rec.onend = () => {
          setListening(false);
          clearWatchdog();
          recRef.current = null;
          resolve(collected.trim());
        };

        recRef.current = rec;
        try {
          rec.start();
        } catch {
          clearWatchdog();
          setError("unknown");
          reject(new Error("recognition could not start"));
        }
      });
    },
    [timeoutMs]
  );

  return { supported, listening, interim, final, error, start, stop, reset };
}
import { useEffect, useRef, useState, useCallback } from "react";

export type WakeWordState = "idle" | "listening";

export interface WakeWordEvent {
  wakeWord: string;
  transcript: string;
}

export interface WakeWordService {
  state: WakeWordState;
  supported: boolean;
  wakeWord: string;
  error: string | null;
  start: () => void;
  stop: () => void;
}

function pickRecognition(): SpeechRecognition | null {
  if (typeof window === "undefined") return null;
  const Ctor = window.SpeechRecognition || window.webkitSpeechRecognition;
  return Ctor ? new Ctor() : null;
}

function isSupported(): boolean {
  return typeof window !== "undefined" && !!(window.SpeechRecognition || window.webkitSpeechRecognition);
}

export function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// Accepts "assistant", "hey assistant", "okay assistant", "hello assistant".
export function buildWakeMatcher(wakeWord: string): RegExp {
  return new RegExp(
    `^(?:hey|ok(?:ay)?|hello|hi)?\\s*${escapeRegExp(wakeWord)}\\b\\s*`,
    "i"
  );
}

/**
 * Client-side wake-word service mirroring the backend's IWakeWordService
 * contract (Start / Stop / IsListening / WakeWordDetected).
 *
 * The current engine uses the browser SpeechRecognition in continuous
 * interim-result mode, matching the wake word locally on-device. Audio never
 * leaves the browser with this engine (nothing is uploaded to our server), and
 * the engine can be swapped for an AudioWorklet/ML keyword model behind the same
 * interface later.
 */
export function useWakeWord(
  wakeWord: string,
  enabled: boolean,
  onDetected: (event: WakeWordEvent) => void,
  language = "en-IN"
): WakeWordService {
  const [state, setState] = useState<WakeWordState>("idle");
  const [error, setError] = useState<string | null>(null);
  const recRef = useRef<SpeechRecognition | null>(null);
  const enabledRef = useRef(enabled);
  const onDetectedRef = useRef(onDetected);
  const languageRef = useRef(language);
  const wakeWordRef = useRef(wakeWord);
  // Once the wake word is heard, we keep listening for the command that
  // follows instead of firing/aborting on the bare wake word.
  const awakeRef = useRef(false);
  const commandRef = useRef("");
  const timerRef = useRef<number | null>(null);
  // Chrome's SpeechRecognition frequently drops the very first session of a
  // fresh page (and any session started before the mic permission prompt is
  // answered): start() "succeeds" but no onstart ever fires. We detect that
  // passively (watchdog timer) and let onend restart with a fresh session —
  // aborting from within onstart (an "active warm-up") corrupts Chrome's
  // recognizer state and can make every subsequent start() fail, which is worse.
  const primeTimerRef = useRef<number | null>(null);

  enabledRef.current = enabled;
  languageRef.current = language;
  wakeWordRef.current = wakeWord;

  useEffect(() => {
    onDetectedRef.current = onDetected;
  }, [onDetected]);

  const clearTimer = useCallback(() => {
    if (timerRef.current !== null) {
      window.clearTimeout(timerRef.current);
      timerRef.current = null;
    }
  }, []);

  const clearPrime = useCallback(() => {
    if (primeTimerRef.current !== null) {
      window.clearTimeout(primeTimerRef.current);
      primeTimerRef.current = null;
    }
  }, []);

  const fire = useCallback((transcript: string) => {
    clearTimer();
    // Reset capture state before aborting: a synchronous onend during abort()
    // must not see stale "awake + command" state and re-fire.
    awakeRef.current = false;
    commandRef.current = "";
    const ww = wakeWordRef.current.toLowerCase();
    recRef.current?.abort();
    recRef.current = null;
    setState("idle");
    onDetectedRef.current({ wakeWord: ww, transcript });
  }, []);

  const stop = useCallback(() => {
    clearTimer();
    clearPrime();
    recRef.current?.abort();
    recRef.current = null;
    awakeRef.current = false;
    commandRef.current = "";
    setError(null);
    setState("idle");
  }, [clearTimer, clearPrime]);

  const run = useCallback(() => {
    if (!enabledRef.current || recRef.current) return;

    const rec = pickRecognition();
    if (!rec) {
      setState("idle");
      return;
    }

    // Drop any timer left over from a previous recognition session.
    clearTimer();
    clearPrime();
    awakeRef.current = false;
    commandRef.current = "";
    setError(null);

    rec.lang = languageRef.current;
    rec.continuous = true;
    rec.interimResults = true;
    rec.maxAlternatives = 3;

    rec.onresult = (event: SpeechRecognitionEvent) => {
      // Keep only the most recent utterance chunk. Accumulating interim +
      // final results together can corrupt the buffer (e.g. an interim
      // partial "ass" followed by the final "assistant" producing "ass
      // assistant"), which breaks the first-word match below.
      let latest = "";
      let latestFinal = false;
      for (let i = event.resultIndex; i < event.results.length; i += 1) {
        latest = event.results[i][0].transcript.trim();
        latestFinal = event.results[i].isFinal;
      }

      const ww = wakeWordRef.current.toLowerCase();
      const matcher = buildWakeMatcher(ww);

      if (!awakeRef.current) {
        const match = latest.toLowerCase().match(matcher);
        if (!match) return; // no wake word yet; keep monitoring

        const transcript = latest.slice(match[0].length).trim();
        if (!transcript) {
          // Bare wake word (interim). Keep listening for the command that
          // follows instead of aborting here. Fire only if the user stays
          // silent, so a lone "assistant" still gets a response.
          awakeRef.current = true;
          commandRef.current = "";
          clearTimer();
          timerRef.current = window.setTimeout(() => fire(""), 8000);
          return;
        }
        // Wake word + command already in this utterance.
        commandRef.current = transcript;
        awakeRef.current = true;
      } else {
        // Command capture mode. The command may continue in this utterance
        // (starts with the wake word again in interim results) or arrive as
        // a fresh utterance after the wake word.
        const match2 = latest.toLowerCase().match(matcher);
        commandRef.current = match2 ? latest.slice(match2[0].length).trim() : latest || commandRef.current;
      }

      if (latestFinal && commandRef.current) {
        fire(commandRef.current);
        return;
      }

      // Not final yet — fire shortly after the user stops speaking so we
      // don't emit a partial interim command. Keep the bare-wake-word
      // fallback timer when nothing has been spoken yet.
      if (commandRef.current) {
        clearTimer();
        timerRef.current = window.setTimeout(() => {
          if (commandRef.current) fire(commandRef.current);
        }, 1200);
      }
    };

    rec.onend = () => {
      recRef.current = null;
      if (awakeRef.current && commandRef.current) {
        // Recognition ended mid-capture; deliver whatever we collected.
        fire(commandRef.current);
        return;
      }
      awakeRef.current = false;
      commandRef.current = "";
      if (enabledRef.current) {
        // Auto-restart keeps the wake word watched continuously.
        window.setTimeout(run, 300);
      } else {
        setState("idle");
      }
    };

    rec.onerror = (event: SpeechRecognitionErrorEvent) => {
      // Permission problems are the only thing worth surfacing to the user;
      // other errors (network, no-speech, audio-capture) are transient and the
      // auto-restart below keeps retrying.
      if (event.error === "not-allowed" || event.error === "service-not-allowed") {
        setError("Microphone permission denied or not granted yet. Click the mic button to allow it.");
      }
    };

    // Once the session actually starts (mic stream acquired), the watchdog is
    // no longer needed.
    rec.onstart = () => {
      clearPrime();
    };

    recRef.current = rec;
    setState("listening");
    // Passive warm-up: Chrome's dead first session never fires onstart. If that
    // happens, abort after a short grace period so onend restarts a fresh
    // (live) session. A live session fires onstart (clearing this timer) before
    // the grace period elapses; a synchronous start() failure also clears it.
    primeTimerRef.current = window.setTimeout(() => {
      if (recRef.current === rec) {
        try {
          rec.abort();
        } catch {
          /* ignore */
        }
      }
    }, 1500);
    try {
      rec.start();
    } catch {
      clearPrime();
      recRef.current = null;
      setState("idle");
      // A synchronous start() failure (e.g. recognizer not ready) is transient;
      // retry shortly instead of leaving the wake word dead.
      if (enabledRef.current) {
        window.setTimeout(run, 500);
      }
    }
  }, [stop, fire, clearTimer, clearPrime]);

  useEffect(() => {
    if (enabled) {
      run();
    } else {
      stop();
    }
    return stop;
  }, [enabled, wakeWord, run, stop]);

  return {
    state,
    supported: isSupported(),
    wakeWord,
    error,
    start: run,
    stop
  };
}
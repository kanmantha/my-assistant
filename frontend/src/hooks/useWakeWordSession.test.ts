import { describe, expect, it, beforeEach, afterEach, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useWakeWord } from "./useWakeWord";

interface FakeResult {
  isFinal: boolean;
  0: { transcript: string };
}

interface FakeEvent {
  resultIndex: number;
  results: FakeResult[];
}

class FakeRecognition {
  lang = "";
  continuous = false;
  interimResults = false;
  maxAlternatives = 1;
  onstart: (() => void) | null = null;
  onend: (() => void) | null = null;
  onerror: ((event: { error: string }) => void) | null = null;
  onresult: ((event: FakeEvent) => void) | null = null;
  aborted = false;
  started = false;

  static instances: FakeRecognition[] = [];
  // Simulates Chrome dropping a fresh session: the recognizer "starts" but
  // never fires onstart (so the hook's watchdog must abort + restart it).
  static deadSessionsRemaining = 0;

  constructor() {
    FakeRecognition.instances.push(this);
  }

  start() {
    this.started = true;
    if (FakeRecognition.deadSessionsRemaining > 0) {
      FakeRecognition.deadSessionsRemaining -= 1;
      return;
    }
    this.onstart?.();
  }

  abort() {
    this.aborted = true;
    this.onend?.();
  }
}

const last = () => FakeRecognition.instances[FakeRecognition.instances.length - 1];

describe("useWakeWord first-session recovery", () => {
  beforeEach(() => {
    FakeRecognition.instances = [];
    FakeRecognition.deadSessionsRemaining = 0;
    // jsdom has no SpeechRecognition; inject the fake.
    vi.stubGlobal("SpeechRecognition", FakeRecognition);
    vi.stubGlobal("webkitSpeechRecognition", undefined);
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
  });

  it("recovers a dead first session via the watchdog, then captures the first command", () => {
    FakeRecognition.deadSessionsRemaining = 1;
    const onDetected = vi.fn();
    renderHook(() => useWakeWord("assistant", true, onDetected, "en-IN"));

    // First session started but never came up (no onstart fired).
    expect(FakeRecognition.instances.length).toBe(1);
    expect(FakeRecognition.instances[0].started).toBe(true);
    expect(FakeRecognition.instances[0].aborted).toBe(false);

    // The watchdog aborts the dead session after 1.5s; onend restarts a fresh
    // (live) session 300ms later.
    act(() => {
      vi.advanceTimersByTime(1500);
    });
    expect(FakeRecognition.instances[0].aborted).toBe(true);

    act(() => {
      vi.advanceTimersByTime(300);
    });
    expect(FakeRecognition.instances.length).toBe(2);

    // The very first utterance on the live session must be recognized.
    const real = last();
    act(() => {
      real.onresult?.({
        resultIndex: 0,
        results: [{ isFinal: true, 0: { transcript: "assistant add note" } }]
      });
    });

    expect(onDetected).toHaveBeenCalledTimes(1);
    expect(onDetected).toHaveBeenCalledWith({ wakeWord: "assistant", transcript: "add note" });
  });

  it("keeps a live first session listening (watchdog cancelled on onstart) and captures split utterances", () => {
    const onDetected = vi.fn();
    renderHook(() => useWakeWord("assistant", true, onDetected, "en-IN"));

    const real = last();
    // onstart fired synchronously, so the watchdog must be cancelled: long after
    // the grace period the live session is still running.
    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(real.aborted).toBe(false);

    // Interim bare wake word first, then the command in a final result.
    act(() => {
      real.onresult?.({
        resultIndex: 0,
        results: [{ isFinal: false, 0: { transcript: "assistant" } }]
      });
    });
    act(() => {
      vi.advanceTimersByTime(100);
      real.onresult?.({
        resultIndex: 0,
        results: [{ isFinal: true, 0: { transcript: "remind me to call the client at 5" } }]
      });
    });

    expect(onDetected).toHaveBeenCalledTimes(1);
    expect(onDetected).toHaveBeenCalledWith({ wakeWord: "assistant", transcript: "remind me to call the client at 5" });
  });

  it("surfaces a microphone permission error without firing a detection", () => {
    const onDetected = vi.fn();
    const { result } = renderHook(() => useWakeWord("assistant", true, onDetected, "en-IN"));

    act(() => {
      last().onerror?.({ error: "not-allowed" });
    });

    expect(result.current.error).toContain("Microphone permission");
    expect(onDetected).not.toHaveBeenCalled();
  });
});

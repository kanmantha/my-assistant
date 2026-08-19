import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { UserSettings } from "../models";

const hoisted = vi.hoisted(() => {
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
  return { DEFAULT_SETTINGS };
});

let state: {
  voiceOverlayOpen: boolean;
  status: string;
  messages: { role: "user" | "assistant"; text: string }[];
  capture: { type: "date" | "category" } | null;
  confirmation: { text: string } | null;
  micSupported: boolean;
  errorMessage: string | null;
} = {
  voiceOverlayOpen: false,
  status: "idle",
  messages: [],
  capture: null,
  confirmation: null,
  micSupported: true,
  errorMessage: null
};

const startListening = vi.fn().mockResolvedValue(undefined);
const stopListening = vi.fn();
const closeVoiceOverlay = vi.fn();
const sendText = vi.fn().mockResolvedValue(undefined);
const answerConfirmation = vi.fn().mockResolvedValue(undefined);

vi.mock("../contexts/SettingsContext", () => ({
  useSettings: () => ({ settings: hoisted.DEFAULT_SETTINGS, update: vi.fn(), setLocal: vi.fn(), loading: false })
}));

vi.mock("../contexts/AssistantContext", () => ({
  useAssistant: () => ({
    status: state.status,
    messages: state.messages,
    micSupported: state.micSupported,
    errorMessage: state.errorMessage,
    confirmation: state.confirmation,
    capture: state.capture,
    wakeListening: false,
    wakeEnabled: false,
    speaking: state.status === "speaking",
    voiceOverlayOpen: state.voiceOverlayOpen,
    openVoiceOverlay: vi.fn(),
    closeVoiceOverlay,
    toggleWakeWord: vi.fn(),
    startListening,
    stopListening,
    sendText,
    answerConfirmation,
    clearMessages: vi.fn(),
    sessionId: "test"
  })
}));

import { VoiceOverlay } from "./VoiceOverlay";

describe("VoiceOverlay", () => {
  beforeEach(() => {
    state = {
      voiceOverlayOpen: false,
      status: "idle",
      messages: [],
      capture: null,
      confirmation: null,
      micSupported: true,
      errorMessage: null
    };
    startListening.mockClear();
    stopListening.mockClear();
    closeVoiceOverlay.mockClear();
    sendText.mockClear();
    answerConfirmation.mockClear();
  });

  it("renders nothing when closed", () => {
    render(<VoiceOverlay />);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("auto-starts listening when opened and idle with no messages", () => {
    state.voiceOverlayOpen = true;
    render(<VoiceOverlay />);
    expect(startListening).toHaveBeenCalled();
  });

  it("does not auto-listen when already speaking", () => {
    state.voiceOverlayOpen = true;
    state.status = "speaking";
    render(<VoiceOverlay />);
    expect(startListening).not.toHaveBeenCalled();
  });

  it("closes via the close button", async () => {
    const user = userEvent.setup();
    state.voiceOverlayOpen = true;
    render(<VoiceOverlay />);
    await user.click(screen.getByRole("button", { name: "Close assistant" }));
    expect(closeVoiceOverlay).toHaveBeenCalled();
  });

  it("shows capture category chips and sends the selection", async () => {
    const user = userEvent.setup();
    state.voiceOverlayOpen = true;
    state.capture = { type: "category" };
    render(<VoiceOverlay />);
    expect(screen.getByText("Choose a category")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Work" }));
    expect(sendText).toHaveBeenCalledWith("Work");
  });

  it("shows a confirmation prompt and answers it", async () => {
    const user = userEvent.setup();
    state.voiceOverlayOpen = true;
    state.confirmation = { text: "Should I schedule the meeting?" };
    render(<VoiceOverlay />);
    expect(screen.getByText("Should I schedule the meeting?")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Yes" }));
    expect(answerConfirmation).toHaveBeenCalledWith(true);
  });

  it("renders the conversation transcript", () => {
    state.voiceOverlayOpen = true;
    state.messages = [
      { role: "user", text: "add a task to file taxes" },
      { role: "assistant", text: "Your task has been added." }
    ];
    render(<VoiceOverlay />);
    expect(screen.getByText("add a task to file taxes")).toBeInTheDocument();
    expect(screen.getByText("Your task has been added.")).toBeInTheDocument();
  });
});
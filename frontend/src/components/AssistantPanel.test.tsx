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

let capture: { type: "date" | "category" } | null = null;
const sendText = vi.fn().mockResolvedValue(undefined);

vi.mock("../contexts/SettingsContext", () => ({
  useSettings: () => ({ settings: hoisted.DEFAULT_SETTINGS, update: vi.fn(), setLocal: vi.fn(), loading: false })
}));

vi.mock("./MicOrb", () => ({
  MicOrb: () => <div data-testid="mic-orb" />
}));

vi.mock("../contexts/AssistantContext", () => ({
  useAssistant: () => ({
    status: "idle",
    messages: [],
    micSupported: true,
    errorMessage: null,
    confirmation: null,
    capture,
    wakeListening: false,
    wakeEnabled: false,
    speaking: false,
    toggleWakeWord: vi.fn(),
    startListening: vi.fn(),
    stopListening: vi.fn(),
    sendText,
    answerConfirmation: vi.fn(),
    clearMessages: vi.fn(),
    sessionId: "test"
  })
}));

import { AssistantPanel } from "./AssistantPanel";

describe("AssistantPanel", () => {
  beforeEach(() => {
    capture = null;
    sendText.mockClear();
  });

  it("renders the input and mic without capture UI when idle", () => {
    render(<AssistantPanel />);
    expect(screen.getByLabelText("Assistant command input")).toBeInTheDocument();
    expect(screen.getByLabelText("Microphone")).toBeInTheDocument();
    expect(screen.queryByText("Choose a category")).not.toBeInTheDocument();
    expect(screen.queryByText("Pick a date")).not.toBeInTheDocument();
  });

  it("shows category chips when capture type is category", () => {
    capture = { type: "category" };
    render(<AssistantPanel />);
    expect(screen.getByText("Choose a category")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Work" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Personal" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Travel" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Skip" })).toBeInTheDocument();
  });

  it("sends the selected category and skip via sendText", async () => {
    const user = userEvent.setup();
    capture = { type: "category" };
    render(<AssistantPanel />);

    await user.click(screen.getByRole("button", { name: "Work" }));
    expect(sendText).toHaveBeenCalledWith("Work");

    await user.click(screen.getByRole("button", { name: "Skip" }));
    expect(sendText).toHaveBeenCalledWith("skip");
  });

  it("shows a date picker with Save and Skip when capture type is date", async () => {
    const user = userEvent.setup();
    capture = { type: "date" };
    render(<AssistantPanel />);

    expect(screen.getByText("Pick a date")).toBeInTheDocument();
    const picker = screen.getByLabelText("Pick a date") as HTMLInputElement;
    expect(picker.type).toBe("date");

    await user.click(screen.getByRole("button", { name: "Save" }));
    expect(sendText).toHaveBeenCalledWith(picker.value);

    await user.click(screen.getByRole("button", { name: "Skip" }));
    expect(sendText).toHaveBeenCalledWith("skip");
  });
});
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { UserSettings } from "../models";
import type { Appointment } from "../models";

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
  const SAMPLE_APPT: Appointment = {
    id: "a1",
    title: "Standup",
    description: undefined,
    startDateTime: new Date().toISOString().slice(0, 10) + "T04:00:00Z",
    endDateTime: new Date().toISOString().slice(0, 10) + "T04:30:00Z",
    location: undefined,
    participants: [],
    reminderMinutes: 15,
    status: 0,
    createdAt: new Date().toISOString()
  };
  return { DEFAULT_SETTINGS, SAMPLE_APPT };
});

vi.mock("../contexts/SettingsContext", () => ({
  useSettings: () => ({ settings: hoisted.DEFAULT_SETTINGS, update: vi.fn(), setLocal: vi.fn(), loading: false })
}));

vi.mock("../api/endpoints", () => ({
  appointmentsApi: {
    list: vi.fn().mockResolvedValue([hoisted.SAMPLE_APPT]),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn()
  }
}));

import { CalendarPage } from "./CalendarPage";

describe("CalendarPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the month view by default with a selected-day panel", async () => {
    render(<CalendarPage />);
    expect(await screen.findByText("Standup")).toBeInTheDocument();
  });

  it("switches to the year view and shows 12 mini month grids", async () => {
    const user = userEvent.setup();
    render(<CalendarPage />);
    await screen.findByText("Standup");

    await user.click(screen.getByRole("button", { name: "Year" }));

    const year = new Date().getFullYear();
    expect(screen.getByText(String(year))).toBeInTheDocument();
    expect(screen.getAllByText("Jan")).toHaveLength(1);
    expect(screen.getAllByText("Dec")).toHaveLength(1);
    expect(screen.getAllByText(/· 1$/)).toHaveLength(1);
  });

  it("opens the month view when a mini month grid is clicked", async () => {
    const user = userEvent.setup();
    render(<CalendarPage />);
    await screen.findByText("Standup");

    await user.click(screen.getByRole("button", { name: "Year" }));
    await user.click(screen.getByText("Feb"));

    const monthTitle = screen.getByText(/February 2026/);
    expect(monthTitle).toBeInTheDocument();
  });
});
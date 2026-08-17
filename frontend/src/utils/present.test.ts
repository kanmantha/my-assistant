import { describe, expect, it } from "vitest";
import { formatDateOnly, statusName, priorityName, apptStatusName } from "./present";
import { getGreeting, monthMatrix } from "./date";
import { t, taskStatusLabel } from "./locale";

describe("present", () => {
  it("maps numeric task status to a name", () => {
    expect(statusName(0)).toBe("Pending");
    expect(statusName(2)).toBe("Completed");
  });

  it("maps string enum values through unchanged", () => {
    expect(statusName("Cancelled")).toBe("Cancelled");
    expect(priorityName("Urgent")).toBe("Urgent");
  });

  it("maps numeric priority and appointment status", () => {
    expect(priorityName(3)).toBe("Urgent");
    expect(apptStatusName(3)).toBe("Rescheduled");
  });

  it("formats ISO date-only strings", () => {
    expect(formatDateOnly("2026-08-08")).toBe("08-08-2026");
    expect(formatDateOnly(undefined)).toBe("");
  });
});

describe("date", () => {
  it("builds a 6-week month matrix", () => {
    const weeks = monthMatrix(2026, 7);
    expect(weeks).toHaveLength(6);
    expect(weeks.every((w) => w.length === 7)).toBe(true);
  });

  it("greets with a localized morning greeting", () => {
    const d = new Date(2026, 7, 8, 9, 0, 0);
    expect(getGreeting(d, "Ravi", "en")).toBe("Good Morning, Ravi");
    expect(getGreeting(d, "Ravi", "te")).toContain("శుభ");
  });
});

describe("locale", () => {
  it("translates known keys", () => {
    expect(t("tasks", "hi")).toBe("कार्य");
    expect(t("tasks", "te")).toBe("టాస్క్‌లు");
  });

  it("falls back to English", () => {
    expect(t("tasks", "fr")).toBe("Tasks");
  });

  it("labels enum values per language", () => {
    expect(taskStatusLabel(2, "hi")).toBe("पूर्ण");
  });
});
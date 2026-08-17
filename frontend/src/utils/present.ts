import type { EnumValue } from "../models";

const STATUS_NAMES = ["Pending", "InProgress", "Completed", "Cancelled"];
const PRIORITY_NAMES = ["Low", "Medium", "High", "Urgent"];
const RECURRENCE_NAMES = ["Once", "Daily", "Weekly", "Monthly", "Yearly", "Custom"];
const APPT_STATUS_NAMES = ["Scheduled", "Completed", "Cancelled", "Rescheduled"];

function asString(value: EnumValue, names: string[]): string {
  if (typeof value === "string") {
    return names.includes(value) ? value : names[0];
  }
  return names[value] ?? names[0];
}

export function statusName(value: EnumValue): string {
  return asString(value, STATUS_NAMES);
}

export function priorityName(value: EnumValue): string {
  return asString(value, PRIORITY_NAMES);
}

export function recurrenceName(value: EnumValue): string {
  return asString(value, RECURRENCE_NAMES);
}

export function apptStatusName(value: EnumValue): string {
  return asString(value, APPT_STATUS_NAMES);
}

const STATUS_COLOR: Record<string, string> = {
  Pending: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300",
  InProgress: "bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300",
  Completed: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  Cancelled: "bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300"
};

const PRIORITY_COLOR: Record<string, string> = {
  Low: "bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300",
  Medium: "bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300",
  High: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300",
  Urgent: "bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300"
};

export function statusColor(value: EnumValue): string {
  return STATUS_COLOR[statusName(value)];
}

export function priorityColor(value: EnumValue): string {
  return PRIORITY_COLOR[priorityName(value)];
}

export function statusBadgeColor(value: EnumValue): string {
  return statusName(value).toLowerCase() === "completed"
    ? "green"
    : statusName(value).toLowerCase() === "cancelled"
      ? "red"
      : statusName(value).toLowerCase() === "inprogress"
        ? "blue"
        : "amber";
}

export function priorityBadgeColor(value: EnumValue): string {
  const name = priorityName(value).toLowerCase();
  return name === "urgent" ? "red" : name === "high" ? "amber" : name === "medium" ? "blue" : "slate";
}

const APPT_STATUS_COLOR: Record<string, string> = {
  Scheduled: "bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300",
  Completed: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-300",
  Cancelled: "bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300",
  Rescheduled: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300"
};

export function apptStatusColor(value: EnumValue): string {
  return APPT_STATUS_COLOR[apptStatusName(value)] ?? APPT_STATUS_COLOR.Scheduled;
}

export function formatDateOnly(value?: string): string {
  if (!value) return "";
  const [y, m, d] = value.split("-");
  return `${d}-${m}-${y}`;
}
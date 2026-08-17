export function toLocalDate(iso?: string): Date | null {
  if (!iso) return null;
  const d = new Date(iso);
  return isNaN(d.getTime()) ? null : d;
}

export function toDateOnly(iso?: string): string | undefined {
  const d = toLocalDate(iso);
  return d ? d.toISOString().slice(0, 10) : undefined;
}

export function toTimeInput(date: Date): string | undefined {
  return date ? date.toTimeString().slice(0, 5) : undefined;
}

export function isoFromParts(date?: string, time?: string): string | undefined {
  if (!date) return undefined;
  const t = time || "09:00";
  return new Date(`${date}T${t}:00`).toISOString();
}

export function formatTime(date: Date): string {
  return date.toLocaleTimeString("en-IN", { hour: "numeric", minute: "2-digit" });
}

export function formatDate(iso: string | Date): string {
  const d = typeof iso === "string" ? new Date(iso) : iso;
  return d.toLocaleDateString("en-IN", { weekday: "short", day: "numeric", month: "short" });
}

export function formatFullDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString("en-IN", { weekday: "long", day: "numeric", month: "long", year: "numeric" });
}

export function todayString(): string {
  return new Date().toISOString().slice(0, 10);
}

export function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}

export function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

export function startOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay() || 7;
  d.setDate(d.getDate() - day + 1);
  return d;
}

export function monthMatrix(year: number, month: number): Date[][] {
  const first = new Date(year, month, 1);
  const startDay = first.getDay();
  const start = addDays(first, -startDay);
  const cells: Date[] = [];
  for (let i = 0; i < 42; i += 1) {
    cells.push(addDays(start, i));
  }
  const weeks: Date[][] = [];
  for (let i = 0; i < 42; i += 7) {
    weeks.push(cells.slice(i, i + 7));
  }
  return weeks;
}

const GREETINGS: Record<string, Record<string, string>> = {
  en: { morning: "Good Morning", afternoon: "Good Afternoon", evening: "Good Evening" },
  hi: { morning: "सुप्रभात", afternoon: "नमस्ते", evening: "शुभ संध्या" },
  te: { morning: "శుభోదయం", afternoon: "శుభ మధ్యాహ్నం", evening: "శుభ సాయంత్రం" }
};

function uiLang(lang: string): "en" | "hi" | "te" {
  switch (lang.toLowerCase()) {
    case "hi":
      return "hi";
    case "te":
      return "te";
    default:
      return "en";
  }
}

export function getGreeting(date: Date, name: string, lang: string): string {
  const h = date.getHours();
  const key = h < 12 ? "morning" : h < 17 ? "afternoon" : "evening";
  const map = GREETINGS[uiLang(lang)] ?? GREETINGS.en;
  const firstName = name.split(" ")[0] || "";
  return firstName ? `${map[key]}, ${firstName}` : map[key];
}
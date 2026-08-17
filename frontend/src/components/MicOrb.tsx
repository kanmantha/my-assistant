import { Mic, MicOff } from "lucide-react";
import type { AssistantStatus } from "../contexts/AssistantContext";

const STATUS_COLORS: Record<AssistantStatus, string> = {
  idle: "from-brand-500 via-brand-400 to-sky-400",
  listening: "from-rose-500 via-pink-500 to-fuchsia-500",
  processing: "from-amber-400 via-orange-400 to-rose-400",
  speaking: "from-emerald-500 via-teal-400 to-sky-400",
  error: "from-rose-600 via-rose-500 to-red-400",
  "wake-listening": "from-brand-400 via-indigo-400 to-violet-400"
};

export function MicOrb({
  status,
  onClick,
  disabled = false,
  size = "lg"
}: {
  status: AssistantStatus;
  onClick: () => void;
  disabled?: boolean;
  size?: "sm" | "lg";
}) {
  const animate = status !== "idle";
  const scale = size === "lg" ? "h-40 w-40" : "h-24 w-24";
  const iconSize = size === "lg" ? "h-12 w-12" : "h-7 w-7";
  const pulseRings = ["h-52 w-52", "h-40 w-40"];

  return (
    <div className="relative flex items-center justify-center" role="group" aria-label={status}>
      {animate &&
        pulseRings.map((cls, i) => (
          <span
            key={i}
            className={`absolute rounded-full bg-gradient-to-br opacity-25 ${STATUS_COLORS[status]} ${cls} animate-ping-slow`}
            style={{ animationDelay: `${i * 400}ms`, animationDuration: "2.2s" }}
          />
        ))}

      <button
        type="button"
        onClick={onClick}
        disabled={disabled}
        aria-label={status === "listening" || status === "wake-listening" ? "Stop listening" : "Activate assistant"}
        className={`relative z-10 flex ${scale} items-center justify-center rounded-full bg-gradient-to-br text-white shadow-2xl transition-transform duration-300 hover:scale-105 active:scale-95 disabled:opacity-40 ${STATUS_COLORS[status]} ${
          animate ? "shadow-[0_0_40px_rgba(255,255,255,0.35)]" : ""
        }`}
      >
        {disabled ? (
          <MicOff className={`${iconSize} text-white/70`} />
        ) : (
          <Mic className={`${iconSize} transition`} />
        )}
      </button>
    </div>
  );
}
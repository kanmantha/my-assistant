/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        brand: {
          50: "#eef4ff",
          100: "#dfeaff",
          200: "#c5d8ff",
          300: "#a2bdfc",
          400: "#7d98f6",
          500: "#6073ee",
          600: "#4a53e0",
          700: "#3d42c5",
          800: "#34399f",
          900: "#2f357e",
          950: "#1c1e4a"
        }
      },
      fontFamily: {
        sans: ["Inter", "system-ui", "-apple-system", "Segoe UI", "Noto Sans Devanagari", "Noto Sans Telugu", "sans-serif"]
      },
      animation: {
        "pulse-slow": "pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite",
        "spin-slow": "spin 2.5s linear infinite",
        shimmer: "shimmer 2s linear infinite"
      },
      keyframes: {
        shimmer: {
          "0%": { backgroundPosition: "-200% 0" },
          "100%": { backgroundPosition: "200% 0" }
        }
      }
    }
  },
  plugins: []
};

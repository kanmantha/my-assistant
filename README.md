# My Assistant — Multilingual AI Personal Voice Assistant

A production-ready, privacy-conscious personal assistant web app built with ASP.NET Core 10 + React + SQLite. Speaks **English**, **Hindi**, and **Telugu**, understands natural-language voice commands, and manages notes, tasks, reminders, and appointments with a free-tier subscription model.

## Features

- 🎤 **Voice assistant** — push-to-talk + optional wake word ("Hey Assistant"), on-device speech recognition (Web Speech API), multilingual TTS replies (en/hi/te).
- 🌐 **Multilingual** — English (en-IN), Hindi (hi-IN), Telugu (te-IN), with auto-detection of the language you speak.
- 🗒️ **Full CRUD modules** — Notes, Tasks, Reminders, Appointments/Calendar, Search, Conversation history, Notifications.
- ⚙️ **Intelligent command parser** — offline heuristic NLP engine (no cloud needed) with an optional OpenAI provider; handles relative dates/times ("kal subah", "repu sandhyam 6 ganta"), multi-step confirmations for destructive actions.
- 🔐 **Auth** — JWT access + refresh tokens, ASP.NET Identity, profile management, password reset flow.
- 💳 **Subscription & usage limits** — free tier (50 notes, 50 tasks, 20 reminders/month) enforced server-side with usage records.
- 🔔 **Background notifications** — recurring reminders processed by a hosted service every 30s.
- 📱 **PWA** — installable, offline-capable, mobile-first bottom navigation.
- 🐳 **Containerized** — docker-compose brings up API + frontend with a persisted SQLite volume.

## Tech Stack

| Layer | Tech |
| --- | --- |
| Backend | ASP.NET Core 10, EF Core 10 (SQLite), ASP.NET Identity, JWT Bearer, Serilog, FluentValidation, xUnit/Moq |
| Frontend | React 18, TypeScript, Vite, Tailwind CSS 3, react-router-dom, Vitest/RTL |
| Database | SQLite (single-file, auto-migrated on first run) |
| Infra | Docker, docker-compose, GitHub Actions (optional) |

## Project Structure

```
backend/
  MyAssistant.Domain/          # Entities, enums, identity user
  MyAssistant.Application/     # DTOs, interfaces, services, NLP engine, validators
  MyAssistant.Infrastructure/  # EF Core, repositories, JWT, seeding, hosted services
  MyAssistant.API/             # Controllers, middleware, Program.cs
  MyAssistant.Tests/           # Unit tests (xUnit + Moq + FluentAssertions)
frontend/
  src/api/                     # HTTP client + typed endpoints
  src/contexts/                # Auth / Settings / Assistant providers
  src/hooks/                   # Speech recognition, TTS, wake word
  src/pages/                   # Dashboard, Tasks, Notes, Reminders, Calendar, Search, History, Settings
  src/components/              # Layout, assistant panel, UI kit
docker-compose.yml             # API + frontend (persisted SQLite volume)
MyAssistant.slnx               # .NET solution
```

## Prerequisites

- .NET SDK 10.0
- Node.js 22+ and npm

No database server is required — the API creates a local `myassistant.db` SQLite file automatically on first run.

## Getting Started

### 1. Database

Nothing to install. On first start the API creates the `myassistant.db` SQLite file and schema automatically. To use a different location, set `ConnectionStrings__DefaultConnection` (e.g. `Data Source=C:\data\myassistant.db`).

### 2. Run everything (recommended)

The backend auto-starts the frontend Vite dev server via the ASP.NET Core SPA proxy, so one command (or one F5 in Visual Studio) runs the full stack and opens the app in the browser.

**Visual Studio:** open `MyAssistant.slnx`, set `MyAssistant.API` as the startup project, and press F5. The API starts, the frontend dev server launches automatically, and the browser opens the app at http://localhost:5173.

**Command line:**

```bash
cd backend/MyAssistant.API
dotnet restore
dotnet run
```

On first start the API seeds demo accounts:

| Role | Email | Password |
| --- | --- | --- |
| User | `demo@example.com` | `Demo@12345` |
| Admin | `admin@example.com` | `Admin@12345` |

The API listens on `http://localhost:5036` and exposes Swagger UI at `/swagger`. The SPA proxy starts `npm run dev` in `frontend/` and the browser opens http://localhost:5173, where `/api` requests are proxied to the backend.

### 3. Running frontend and backend separately

```bash
# terminal 1
cd backend/MyAssistant.API
dotnet run

# terminal 2
cd frontend
npm install
npm run dev
```

Then open http://localhost:5173. The Vite dev server proxies `/api` to the backend.

### 4. Full stack with Docker

```bash
cp .env.example .env   # set JWT_SECRET etc.
docker compose up --build
```

- Frontend: http://localhost:5173
- API: http://localhost:8080
- Swagger: http://localhost:8080/swagger

## Configuration

Environment variables (also see `.env.example`):

| Variable | Description | Default |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | SQLite connection string | `Data Source=myassistant.db` |
| `Jwt__Secret` | JWT signing secret (set a long random value!) | dev placeholder |
| `AI__Provider` | `local` (offline) or `openai` | `local` |
| `AI__ApiKey` | OpenAI key (only for `openai`) | empty |
| `AI__Model` | OpenAI model | `gpt-4o-mini` |
| `Cors__AllowedOrigins` | CORS origins (comma separated) | localhost:5173 |
| `Email__Host` | SMTP host; when set, forgot-password emails are sent via SMTP | empty (logs emails instead) |
| `Email__Port` | SMTP port | `587` |
| `Email__Username` / `Email__Password` | SMTP credentials | empty |
| `Email__From` | Sender address | `no-reply@myassistant.app` |
| `Email__FrontendUrl` | Base URL used for password-reset links | `http://localhost:5173` |

## API Overview

| Area | Endpoints |
| --- | --- |
| Auth | `POST /api/auth/register` `login` `refresh` `forgot-password` `reset-password`, `PUT/GET /api/auth/profile` |
| Notes | `GET/POST /api/notes`, `GET/PUT/DELETE /api/notes/{id}` |
| Tasks | `GET/POST /api/tasks`, `PATCH /api/tasks/{id}/status`, `PUT/DELETE /api/tasks/{id}` |
| Reminders | `GET/POST /api/reminders`, `PATCH /api/reminders/{id}/acknowledge`, `PUT/DELETE /api/reminders/{id}` |
| Appointments | `GET /api/appointments/range?start&end`, `POST /api/appointments`, `PATCH .../reschedule` |
| Assistant | `POST /api/assistant/command`, `POST /api/assistant/transcribe`, `POST /api/assistant/speak` |
| Admin | `GET /api/admin/users`, `GET /api/admin/stats`, `POST /api/admin/users/{id}/reset-usage` (requires `Admin` role) |
| Other | `GET /api/dashboard`, `GET /api/search?q=`, `GET/PUT /api/settings`, `GET /api/conversations`, `GET /api/notifications`, `GET /api/subscription` |

All endpoints (except auth and admin) require `Authorization: Bearer <token>`; admin endpoints additionally require the `Admin` role. `POST /api/auth/forgot-password` sends a reset link by email (or writes it to the log when no SMTP host is configured).

## Voice Assistant Design

- **Recognition:** the browser's Web Speech API runs locally — audio never leaves the device. This is the supported transcription path; `POST /api/assistant/transcribe` returns a placeholder response explaining that server-side ASR is not configured. To add a server-side ASR provider, implement `ISpeechRecognitionService` and set a `SPEECH_API_KEY`.
- **Wake word:** a lightweight phonetic matcher listens for "hey assistant" (or your custom wake word) and opens the mic automatically.
- **Intent engine:** `HeuristicAIService` parses text into intents (create/list/update/delete tasks, notes, reminders, appointments, change language, ask schedule…) with multilingual date/time parsing. Set `AI__Provider=openai` to swap in a cloud LLM — the interface is the same.
- **Confirmation mode:** destructive actions ("delete my notes") require an explicit "yes/no" before executing.

## Testing

```bash
dotnet test backend/MyAssistant.Tests        # backend unit tests
cd frontend && npm test                       # frontend unit tests
cd frontend && npm run typecheck              # TypeScript check
```

## Roadmap

- Email verification (password-reset emails are implemented via SMTP, see `Email__*` config)
- Push notifications (Web Push) instead of in-app only
- Recurring task support and natural-language recurrence parsing
- Payment gateway integration for Pro/Business tiers
- Offline-first sync via IndexedDB

## License

Private / proprietary. Demo credentials are for local evaluation only.

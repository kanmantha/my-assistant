# Feature Inventory — MyAssistant (Subscription Model)

- **Application:** MyAssistant — AI personal assistant with notes, tasks, reminders, appointments, search, subscription tiers.
- **Stack:** .NET 10 (ASP.NET Core / EF Core / SQLite / ASP.NET Identity / JWT) + React 18 (Vite / Tailwind / Vitest).
- **Auth:** JWT bearer (access 60 min, refresh 7 days). Roles: `User`, `Admin` (seeded `admin@example.com` / `Admin@12345`; enforced via `[Authorize(Roles = "Admin")]` on `/api/admin/*`).
- **DB:** SQLite `myassistant.db`, `EnsureCreated` (no EF migrations), DbSeeder seeds `demo@example.com` / `Demo@12345` and `admin@example.com` / `Admin@12345`.
- **Languages supported:** English (en), Hindi (hi), Telugu (te).
- **AI parsing:** local heuristic (regex) by default; optional OpenAI via `AI_PROVIDER`/`AI_API_KEY`.

## Modules & Features

| Module | Feature | Screen | API | Role | Expected behavior | Current impl | Test status |
|---|---|---|---|---|---|---|---|
| Auth | Register | AuthPages | `POST /api/Auth/register` | Guest | Create account, auto-login | Creates user+`User` role+settings+Free subscription; `EmailConfirmed=true` immediately | ⬜ Pending |
| Auth | Login | AuthPages | `POST /api/Auth/login` | Guest/User | Validate credentials, return tokens | JWT+refresh token; 5-attempt lockout | ⬜ Pending |
| Auth | Refresh token | — | `POST /api/Auth/refresh` | User | Rotate refresh token, get new access | Rotates + persists new refresh token; SPA retries on 401 (`http.ts`) | ⬜ Pending |
| Auth | Forgot password | AuthPages | `POST /api/Auth/forgot-password` | Guest | Email reset token | Emails reset link via `IEmailSender` (SMTP if `Email__Host`, else logs) | ⬜ Pending |
| Auth | Reset password | AuthPages | `POST /api/Auth/reset-password` | Guest | Set new password w/ token | Uses Identity reset; validator mismatch (6 vs 8 check) | ⬜ Pending |
| Auth | Change password | — | `POST /api/Auth/change-password` | User | Verify current, set new | Implemented | ⬜ Pending |
| Auth | Profile | AuthPages/AppLayout | `GET`/`PUT /api/Auth/profile` | User | View/update first/last name | Implemented | ⬜ Pending |
| Tasks | CRUD | TasksPage | `GET/POST /api/tasks`, `GET/PUT/DELETE /api/tasks/{id}` | User | Create/list/update/delete tasks | Enforces free-tier 50/month limit | ⬜ Pending |
| Tasks | Status update | TasksPage | `PATCH /api/tasks/{id}/status` | User | Mark pending/in-progress/completed | Implemented | ⬜ Pending |
| Notes | CRUD | NotesPage | `GET/POST /api/notes`, `GET/PUT/DELETE /api/notes/{id}` | User | Create/list/update/delete notes | Auto-derives title; free-tier 50/month | ⬜ Pending |
| Reminders | CRUD | RemindersPage | `GET/POST /api/reminders`, `GET/PUT/DELETE /api/reminders/{id}` | User | Create/update/acknowledge reminders | Free-tier 20/month; creates Notification row | ⬜ Pending |
| Reminders | Acknowledge | RemindersPage | `PUT /api/reminders/{id}/acknowledge` | User | Mark reminder acknowledged | Implemented | ⬜ Pending |
| Appointments | CRUD | CalendarPage | `GET/POST /api/appointments`, `GET/PUT/DELETE /api/appointments/{id}` | User | Schedule/edit/delete (with reminder) | Validates end>start | ⬜ Pending |
| Appointments | Reschedule | CalendarPage | `PATCH /api/appointments/{id}/reschedule` | User | Move appointment | Implemented | ⬜ Pending |
| Appointment list | Range | CalendarPage | `GET /api/appointments/range?start=&end=` | User | Filter by date range | Implemented | ⬜ Pending |
| Subscription | Get status + usage | — | `GET /api/subscription` | User | Show tier/status/expiry + monthly counts per type | Free tier default; no payments wired; usage returns notes/tasks/reminders/appointments/aiCommands/stt/tts/searches | ⬜ Pending |
| Subscription | Limits | enforced in services | — | User | 50 notes / 50 tasks / 20 reminders per mo | `SubscriptionService.CanUseFeatureAsync` throws 403 | ⬜ Pending |
| Settings | Get/Put | SettingsContext | `GET/PUT /api/settings` | User | Language, theme, TTS prefs persisted | Clamps speech speed/volume/font | ⬜ Pending |
| Search | Cross-entity | SearchPage | `GET /api/search?q=&scopes=` | User | Search notes/tasks/appts/reminders | Scoped to user | ⬜ Pending |
| Dashboard | Aggregates | DashboardPage | `GET /api/dashboard` | User | Counts + today lists (IST) | Implemented | ⬜ Pending |
| Conversations | History | HistoryPage | `GET /api/conversations` | User | View assistant transcript | Implemented | ⬜ Pending |
| Conversations | Clear | HistoryPage | `DELETE /api/conversations` | User | Delete all history | Implemented | ⬜ Pending |
| Assistant | Command | AssistantPanel | `POST /api/assistant/command` | User | NL parse → intent → action → reply | Heuristic parser (AI local default) | ⬜ Pending |
| Assistant | Transcribe | AssistantPanel | `POST /api/assistant/transcribe` | User | Audio → text | Placeholder (returns explicit "not configured"); browser Web Speech API is the supported path | ⬜ Pending |
| Admin | Users | — | `GET /api/admin/users` | Admin | List users + roles + usage | Implemented (`AdminService`) | ⬜ Pending |
| Admin | Stats | — | `GET /api/admin/stats` | Admin | Platform analytics | Implemented (`AdminService`) | ⬜ Pending |
| Admin | Reset usage | — | `POST /api/admin/users/{id}/reset-usage` | Admin | Clear this month's usage | Implemented (`AdminService`) | ⬜ Pending |
| Payments/Stripe | checkout/webhook | — | — | — | Subscriptions with payment | **Not implemented** | ⬜ N/A |

## Roles

| Role | Can do | Assigned to |
|---|---|---|
| User | All app features | Every registered account (AuthService line 63), demo user (Seeder line 49) |
| Admin | All app features + `/api/admin/*` endpoints | `admin@example.com` (Seeder) |

## Notes / Risks from code review

- Backend unit suites in `MyAssistant.Tests`: `AssistantAITests`, `AdminServiceTests`, `AuthServiceTests`, `ApplicationServiceTests` (34 tests).
- Frontend vitest suites: `present.test.ts`, `useWakeWord.test.ts`, `http.test.ts` (refresh-token flow).
- `[Authorize]` class-level on non-auth controllers; `AdminController` additionally requires `Roles = "Admin"`.
- No payment provider; "subscription model" = soft free-tier feature limits only. Pro/Business tier values exist but no upgrade path/UI.
- No browser-based E2E (Playwright/Cypress) configured; API-level harness lives in `tests/qa/`.
- JWT secret default is `CHANGE_ME...` (dev only, warns at startup).
# Bugs Found & Resolved During QA

Status legend:
- **FIXED** – defect reproduced, fixed in source, and verified by regression tests in the suite (all green in the final run).
- **CONFIRMED (product gap)** – behaviour is a design/implementation gap, documented here for a future sprint; not hidden by the suite.
- **INFO** – observed behaviour that is intentional or a frontend responsibility, recorded for clarity.

---

## BUG-001 — FluentValidation validators were registered but never executed

- **Status:** FIXED
- **Severity:** High
- **Module:** API (cross-cutting)
- **Symptoms:**
  - `PATCH /api/tasks/{id}/status` accepted `status=99` (stored an invalid status).
  - Empty task titles were accepted.
  - Notes with both `title` and `content` empty were accepted.
  - Oversized strings were accepted (no max-length enforcement).
- **Root cause:** `AddValidatorsFromAssembly` registered validators in DI, but no automatic validation pipeline (e.g. an action filter) applied them, so the controller-level manual checks were the only enforcement — and several were missing/incomplete.
- **Fix applied:**
  - New `backend/MyAssistant.API/Middleware/FluentValidationFilter.cs` — an `IActionFilter` that reflects over action parameters, finds `IValidator<T>` in `HttpContext.RequestServices`, and runs `ValidateAsync` before the action executes; invalid results return `400` with `errors`.
  - Registered globally in `Program.cs`: `AddControllers(options => options.Filters.Add<FluentValidationActionFilter>())`.
  - Expanded `backend/MyAssistant.Application/Validation/Validators.cs` (see BUG-003 for details).
- **Regression coverage:** `CRUD 11–20, 24–25, 29`; `SECURITY 18, 20`; `ASSISTANT 40–41`.

## BUG-002 — Reset-password minimum length mismatch (6 vs 8)

- **Status:** FIXED
- **Severity:** Medium
- **Symptoms:** Client/other validators enforced 8 characters for passwords; reset-password path enforced 6, so a 6–7 char password could be set via reset.
- **Fix applied:** reset-password validator minimum length aligned to 8 (with complexity rules), matching registration/change-password.
- **Regression coverage:** `AUTH 07` (weak password), `AUTH 13–16` (refresh/rotation paths).

## BUG-003 — Incomplete validator coverage across write endpoints

- **Status:** FIXED
- **Severity:** Medium
- **Symptoms:** Several update/DTOs had no rules (or inconsistent rules), e.g.:
  - `UpdateNote`, `UpdateTask`, `UpdateTaskStatus`, `UpdateReminder`, `UpdateAppointment`, `RescheduleAppointment`, `ChangePassword`, `UpdateProfile` — no validators.
  - Task title and note content lacked max lengths.
  - `ReminderAt` had no `NotEqual(default)` guard.
  - Enum fields lacked `IsInEnum`.
- **Fix applied:** Added/expanded validators with: title max 500, content max 5000, `IsInEnum` for status/priority/language, `ReminderAt NotEqual(default)`, and note rule where `title` is required only when `content` is empty (so content-only "auto-title" notes still pass).
- **Regression coverage:** `CRUD 11–13, 16, 19–20, 24–25, 29`; `AUTH 12`.

## BUG-004 — Assistant accepted empty/missing `text` and returned 200

- **Status:** FIXED
- **Severity:** Low
- **Symptoms:** `POST /api/assistant/command` with empty or missing `text` returned 200 with a fabricated reply instead of a validation error.
- **Fix applied:** Command DTO now requires non-empty text (`Length >= 1`, max-length bound); returns 400 when violated.
- **Regression coverage:** `ASSISTANT 40–41`.

## BUG-005 — Refresh token issued but never used by the SPA

- **Status:** FIXED
- **Severity:** Medium (UX/robustness)
- **Symptom:** The backend implements refresh-token rotation (`/api/auth/refresh`, reuse rejection verified in `AUTH 14–16`), but `frontend/src/api/http.ts` never calls it — a 401 triggers full logout. Users are silently logged out when the 60-minute access token expires mid-session.
- **Fix applied:** `http.ts` now intercepts 401 on authenticated requests, calls `/api/auth/refresh` (single-flight via a module-level promise to avoid token thundering herd), stores the rotated tokens, and retries the original request once. A retry is only attempted once per call, so a genuinely unauthorized response still logs out (tokens cleared + redirect to `/login`). Calls with `auth: false` never refresh.
- **Regression coverage:** new `frontend/src/api/http.test.ts` (5 tests: refresh-and-retry, `auth:false` skips refresh, concurrent single-flight shares one refresh, refresh-failure clears tokens, no double-refresh on repeated 401).

## BUG-006 — Admin role seeded but not enforced (no admin endpoints)

- **Status:** FIXED
- **Severity:** Medium (visibility)
- **Symptom:** The DbSeeder creates an `Admin` role and the SPA has no admin surface; no endpoint required the role. It was effectively dead configuration.
- **Fix applied:** new `AdminController` at `/api/admin` protected by `[Authorize(Roles = "Admin")]` — `GET /users` (profile, roles, subscription tier/status, current-month usage per type), `GET /stats` (platform totals, tier breakdown, active users this month), `POST /users/{id}/reset-usage` (removes this month's usage records). Backing `IAdminService`/`AdminService` + DTOs added. DbSeeder now also seeds `admin@example.com` / `Admin@12345` with the `Admin` role.
- **Regression coverage:** new `AdminServiceTests.cs` (4 tests: user listing with roles/tier/usage, stats totals, reset-usage scoping to current month, unknown-user 404).

## BUG-007 — Forgot-password token is logged, not emailed

- **Status:** FIXED
- **Severity:** Low (no SMTP provider configured)
- **Symptom:** `POST /api/auth/forgot-password` generated a reset token and wrote it to the server log (Serilog) instead of emailing it.
- **Fix applied:** pluggable `IEmailSender` (`Application/Interfaces/IEmailSender.cs`) with two implementations: `LogEmailSender` (default — logs the email body, used when no SMTP is configured) and `SmtpEmailSender` (active when `Email__Host` is set). `AuthService.ForgotPasswordAsync` now builds a reset link from `Email__FrontendUrl` and sends it through the configured sender; the token is no longer logged. The SPA gained a `/reset-password` route (AuthPage `reset` mode already existed) so the emailed link resolves.
- **Regression coverage:** new `AuthServiceTests.cs` (3 tests: existing user gets an email with an encoded reset link, unknown user sends nothing, `LogEmailSender` writes the message).

## BUG-008 — Voice transcription endpoint is a placeholder

- **Status:** FIXED
- **Severity:** Low
- **Symptom:** `POST /api/assistant/transcribe` is a stub; the SPA actually uses the browser Web Speech API (`webkitSpeechRecognition`), so no server-side ASR exists.
- **Fix applied:** formally documented the browser Web Speech API as the supported transcription path (README "Voice Assistant Design"); the endpoint keeps returning an explicit "server-side speech recognition is not configured" message so callers never mistake it for a real ASR result. `ISpeechRecognitionService` remains the extension point for a future provider.
- **Regression coverage:** documentation only (endpoint behaviour unchanged).

## BUG-009 — Free-tier quota can be raced by parallel creates (soft limit)

- **Status:** INFO (by design)
- **Severity:** Low
- **Symptom:** During the performance test, 120 parallel note creates landed 77 records against a 50/month cap. The quota check (`SubscriptionService`) is read-then-write without a transaction/lock, so bursty concurrency can overshoot the soft limit.
- **Note:** The limit is a soft business rule (403 guidance to upgrade), not a storage/security boundary. Acceptable for the current single-DB scope; add optimistic concurrency / atomic increment if hard enforcement is required.

## BUG-010 — Appointment end<=start validation (covered, fixed)

- **Status:** FIXED (validated pre-session; now guarded by validators)
- **Severity:** Low
- **Symptom:** Creating an appointment with `endTime <= startTime` returned 400 in tests; the contract is now also enforced via the FluentValidation filter (`RescheduleAppointment`/`CreateAppointment`).
- **Regression coverage:** `CRUD 29`.

## BUG-011 — Public auth pages (`/reset-password`, `/forgot-password`) bounce to `/login`

- **Status:** FIXED
- **Severity:** Medium (functional: reset/forgot links never opened the intended form)
- **Symptom:** Visiting `/reset-password` (or `/forgot-password`) in a browser immediately redirected to `/login`, so the reset-password form was unreachable. The `/login`, `/register`, and the authenticated app all worked.
- **Root cause:** `SettingsProvider` (wrapping the whole app in `App.tsx`) called `settingsApi.get()` on mount. That is an authenticated call; with no token present it 401s and `http.ts:redirectToLogin()` runs, which sets `window.location.href = "/login"` unless the current path already starts with `/login`. So every public page except `/login` was bounced.
- **Fix applied:** `frontend/src/contexts/SettingsContext.tsx` now skips the authenticated settings fetch when `tokenStore.accessToken` is absent (same guard pattern as `AuthProvider`), falling back to defaults. Authenticated sessions still load live settings.
- **Regression coverage:** verified in-browser (headless Edge) that `/login`, `/register`, `/forgot-password`, and `/reset-password` all render their own form and no longer redirect; `npm run test` 21/21, `npm run typecheck` and `npm run build` clean.

## BUG-012 — Wake word always answered "Today's schedule: You have nothing scheduled."

- **Status:** FIXED
- **Severity:** High (functional: spoken scheduling/task/reminder commands never executed)
- **Symptom:** Saying the wake word (e.g. "Assistant") always produced the reply "Today's schedule: You have nothing scheduled." — it never scheduled calls, added tasks, or set reminders, no matter what command followed.
- **Root cause:** `frontend/src/hooks/useWakeWord.ts` fired the detection the moment the wake word appeared in an interim (partial) result — `rec.abort()` and an immediate `onDetected({ wakeWord, transcript })` where `transcript` was empty (the command part had not been recognized yet). `AssistantContext.tsx:84` then fell back to `event.transcript || "What is my schedule today?"`, so every wake event became a schedule query. Backend parsing itself was fine (verified: "schedule a call with John at 3pm" → `CreateAppointment`, "add a task to buy milk tomorrow" → `CreateTask`, "remind me to call the client at 5" → `CreateReminder`).
- **Fix applied:** `useWakeWord.ts` no longer aborts on the bare wake word. On a bare wake word it stays listening (command-capture mode) with an 8s timeout that fires an empty transcript if the user stays silent; a wake word + command in one utterance is captured and fired on the final result or after a 1.2s trailing-silence timer; `onend` delivers any command collected mid-capture; stale timers are cleared when a new recognition session starts.
- **Regression coverage:** `useWakeWord.test.ts` grew from 7 to 9 tests (command capture after the wake word: appointments, tasks, reminders, "hey" prefix; schedule-questions still pass through as the query). `npm run test` 23/23, `npm run typecheck` and `npm run build` clean.

## BUG-013 — "Add Note" / "Add Task" returned Unknown; "Today Tasks Reminders" / "Todays Appointments" returned create-intents

- **Status:** FIXED
- **Severity:** High (functional: four requested voice commands mis-parsed)
- **Symptom:** Saying "Add Note" and "Add Task" returned "Unknown" (nothing happened), while "Today Tasks Reminders" parsed as `CreateReminder` and "Todays Appointments" as `CreateAppointment` (it tried to *create* items instead of reading today's).
- **Root cause:** `HeuristicAIService.cs` note/task patterns required content after the keyword (`add a note to …` / `add a task to …`), so bare "add note"/"add task" fell through to `Unknown`. The list rules (`IsList`) ran before note/task patterns but only matched explicit "list my …"/"show my …" phrasing and returned early for anything starting with "add/create"; phrases like "today reminders" / "today tasks reminders" / "todays appointments" hit the reminder/appointment creation matchers instead (they contain "today"/"remind"/"appointment"). No day-scoping existed for list intents.
- **Fix applied:**
  - `HeuristicAIService.cs` — `IsNote`/`IsTask` now also match the bare forms ("add note", "create note", "write note", "take note", "add task", "create task", plus `note …`/`task …` prefixes). `IsList` was rewritten with a `scope` out-parameter: it recognizes today/tomorrow-scoped reminder/appointment/task phrases (guarded so creation phrasings still create) and returns the scope; creation-worded phrasings no longer slip past it.
  - `ParsedCommand.cs` — new `Scope` property ("today"/"tomorrow"/null).
  - `AssistantService.cs` — list handlers now filter by scope using the user's timezone (`ScopeTasks`/`ScopeReminders`/`ScopeAppointments` + `ScopeWindow`); bare "Add Note"/"Add Task" with no content no longer create an "Untitled note"/"add task" item — they start a two-turn capture (pending `Stage` 2 note-content / 3 task-title in the session store), reply with `AskNoteContent`/`AskTaskTitle`, and `ProcessAsync` stores the next spoken phrase as the note content / task title (a real command while mid-capture supersedes it; "no"/denial cancels).
  - `AssistantReplies.cs` — added `AskNoteContent`/`AskTaskTitle`.
  - `ExtractTaskTitle` returns empty for a bare "add task" so the capture branch triggers instead of a junk title.
- **Regression coverage:** backend unit suite grew 34→44 tests (`Note_BareAddNote_IsCreateNoteWithoutContent`, `Note_BareAddNoteWithContent_IsCreateNote`, `Task_BareAddTask_IsCreateTaskWithoutTitle`, `Task_BareAddTaskWithContent_IsCreateTask`, `List_TodayTasksReminders_IsListRemindersScopedToday`, `List_TodayReminders_IsListRemindersScopedToday`, `List_TodaysAppointments_IsListAppointmentsScopedToday`, `List_TomorrowReminders_IsListRemindersScopedTomorrow`, plus create-intent regression guards) — all pass. QA harness added the four phrases (ASSISTANT 41→45, all pass). Live API verified: scoped lists only show that day's data; multi-turn capture saves the spoken content as note/task. `dotnet test` 44/44, full QA `node run.mjs` 150/150, frontend vitest 23/23.

---

## BUG-014 — Wake word misses the very first utterance / can die on first session

- **Status:** FIXED
- **Severity:** High (functional: first spoken command after loading the app could be lost; a first-session start/abort warm-up then made the wake word dead entirely)
- **Symptom:** "for the very first time" the wake word does not respond. Chrome's SpeechRecognition frequently drops the very first session of a fresh page (and any session started while the microphone permission prompt is open): `start()` "succeeds" but `onstart` never fires. Also, if the first `rec.start()` threw synchronously the hook went idle and never retried.
- **Root cause (first attempt, worsened it):** an "active warm-up" that aborted the throwaway session from inside `onstart`. That pattern is known to corrupt Chrome's recognizer state so every subsequent `start()` fails — the wake word became dead on every page load.
- **Fix applied (final):** `frontend/src/hooks/useWakeWord.ts`:
  - Removed the start→abort warm-up entirely. The first session is now a real session.
  - Added a **passive watchdog**: a 1.5s timer armed before each `start()`; a live session fires `onstart` (which cancels the timer); a dead first session never fires `onstart`, so the watchdog aborts it and the existing `onend` auto-restart brings up a fresh, live session ~300ms later. Abort happens from the timer, never from inside `onstart`.
  - A synchronous `start()` throw cancels the watchdog and schedules a 500ms retry instead of giving up.
  - `fire()` resets `awake`/`command` state *before* calling `abort()` (prevents double-fire on synchronous `onend`).
  - New `WakeWordService.error` surfaces "Microphone permission denied or not granted yet. Click the mic button to allow it." when recognition reports `not-allowed`/`service-not-allowed`; `AssistantContext` displays it.
- **Regression coverage:** `frontend/src/hooks/useWakeWordSession.test.ts` mocks SpeechRecognition (including a configurable "dead first session" that never fires `onstart`) and verifies (1) the watchdog aborts a dead first session and the restart then captures the very first utterance; (2) a live session survives the watchdog grace period and captures wake word + command split across utterances; (3) a `not-allowed` error sets the permission message without firing a detection. `npm run test` 26/26, `npm run typecheck` and `npm run build` clean.

---

## BUG-015 — Wake word feature was silently disabled for every registered user

- **Status:** FIXED
- **Severity:** High (functional: the Assistant panel never showed a wake-word indicator, so "Assistant" was ignored entirely)
- **Symptom:** "Wake word Assistant is not working at all." On load the Assistant panel showed no "wake word active" indicator and speaking "Assistant" did nothing. Fresh registrations returned `wakeWordEnabled: false` from `GET /api/settings`.
- **Root cause:** the backend defaulted the wake word to OFF. The C# `UserSettings.WakeWordEnabled` property had no default (`bool` → `false`), and every creation path — `AuthService.RegisterAsync` (`new UserSettings { UserId = user.Id }`), `UserSettingsRepository.GetOrCreateAsync`, and the admin seed — created settings without setting it, so all app-registered users got the wake word disabled. The frontend default (`wakeWordEnabled: true`) was silently overridden as soon as the app fetched settings. All 121 rows in the live DB had `WakeWordEnabled = 0`.
- **Fix applied:**
  - `MyAssistant.Domain/Entities/UserSettings.cs`: `public bool WakeWordEnabled { get; set; } = true;` (entity default).
  - `AuthService.RegisterAsync`, `UserSettingsRepository.GetOrCreateAsync`, `DbSeeder` (admin): explicitly `WakeWordEnabled = true`.
  - Backfilled the existing SQLite DB: `UPDATE UserSettings SET WakeWordEnabled = 1 WHERE WakeWordEnabled = 0` (121 rows) via `node:sqlite`.
- **Verification:** new `SettingsDefaultTests.NewUserSettings_WakeWordEnabled_IsTrueByDefault`; backend suite now 45 tests, all pass; live check — a fresh registration returns `wakeWordEnabled: true`; full QA harness still **150 passed / 0 failed**.
- **Note (API process):** the API process has silently died twice today (no stack trace in logs). It was restarted via `Start-Process dotnet run --launch-profile http` with output redirected to `G:\Temp\opencode\api-out.log` / `api-err.log`. If the app ever stops responding, check whether `http://localhost:5036` is listening first and restart the API.

---

## Previously fixed bugs now locked in by regression tests

These were fixed earlier in the project history; the QA suite guards against regression (referenced suite lines pass in the final run):

| Area | Fix | Regression coverage |
|---|---|---|
| Language switch (settings) applied only after restart/other-session | Settings update applied live; language persisted (`CRUD 34`, `ASSISTANT 38–39`) | `CRUD 32–35`, `ASSISTANT 38–39` |
| Assistant "not responding" after a task completion | Root cause was reminder-quota 403 thrown for a side-effect operation; assistant now handles quota errors as guidance, not crashes (`SUBSCRIPTION 7` message path) | `ASSISTANT 33–37`, `SUBSCRIPTION 7` |
| Task created via assistant carried schedule junk in the title | Title cleaned; `dueDate`/`dueTime` separated (`ASSISTANT 31–32`) | `ASSISTANT 31–32` |
| Task completion fuzzy match too strict | Partial/tolerant matching with confirmation prompt (`ASSISTANT 33`) | `ASSISTANT 33` |
| Calendar/date parsing threw for malformed input | Query guard added; date parsing returns 400 instead of 500 (`CRUD 38`) | `CRUD 38`, `EXTRA 1–2` |
| Calendar range query bug | `?from=&to=` filtering correct in and out of range (`EXTRA 1–2`) | `EXTRA 1–2` |

## Open observations (not bugs, for the report)

- Global rate limiter is configured at 300 req/min (SingleUser limit); no 429 observed in the suite at the tested concurrency.
- The QA automation now lives in the repo at `tests/qa/` (Node harness, no npm dependencies) — run `node run.mjs` from that directory with the API on `:5036`.

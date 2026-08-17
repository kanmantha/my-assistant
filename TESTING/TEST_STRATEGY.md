# TEST_STRATEGY.md

## Approach
API-level (HTTP E2E) testing driven by a Node harness hitting `http://localhost:5036` directly, plus the existing vitest frontend suite and code review. Browser automation is not configured in this repo (no Playwright/Cypress dependency); API tests exercise the same controllers and business logic the UI calls.

The harness lives in the repo at `tests/qa/` (zero npm dependencies; requires Node 18+ for built-in `fetch`).

## Environment
- Backend: `MyAssistant.API` net10.0 at `http://localhost:5036`, Development env, SQLite `myassistant.db` (real 380KB demo DB).
- Frontend: Vite dev server `http://localhost:5173` (proxy `/api` → 5036).
- Roles: `User` (seeded demo + new registered), `Admin` (seeded `admin@example.com` / `Admin@12345`, enforced via `[Authorize(Roles = "Admin")]`).
- Test accounts created on demand via `/api/Auth/register`.

## Test categories planned
1. **Functional happy paths** — CRUD for Tasks/Notes/Reminders/Appointments, search, settings, dashboard, conversations, auth.
2. **Validation / negative** — empty, missing, wrong-typed, oversized, invalid enum, invalid dates (end<=start), out-of-range settings.
3. **Boundary / edge** — free-tier limits (50 tasks, 50 notes, 20 reminders → 403), zero-length lists, pagination, deleted-record access, duplicate records.
4. **Authentication** — register, duplicate email, weak passwords, login wrong creds, case sensitivity, refresh rotation, lockout behavior, change/reset password, profile update.
5. **Authorization / security** — missing token (401), invalid token (401), IDOR (user A edits user B's resource — expect 404/403), SQL injection payloads, XSS payloads, path traversal, malformed JSON, oversized payload, unknown properties, rate beyond limits.
6. **Assistant NLP** — full intent battery in EN/HI/TE, schedule parsing (due dates separate from titles), multi-turn confirmations, completion/deletion fuzzy matching, language persistence.
7. **Subscription** — usage reporting, free-tier enforcement at service level on create, Pro-unchecked features (search/appointments not gated — verify design).
8. **Database/data integrity** — delete cascades (note/task/reminder/appointment list after delete), notifications created, conversation history persisted.
9. **Error handling** — consistent `ApiResponse` envelope `{success, data, message, errors}`, no stack traces, correct HTTP codes 400/401/403/404/429.
10. **Concurrency** — parallel creates do not exceed-limit wrongly / refresh token rotation.
11. **UI** — frontend source review + component render checks where feasible; message localization keys present.

## Pass/fail criteria
- All tests must record real HTTP results (no faked passes).
- Every bug logged in `BUGS.md`; fixed; regression re-run via full suite.

## Execution
`node run.mjs` (single entry) from `tests/qa/`, output JSON + console summary. Results appended to `API_TEST_RESULTS.md`.
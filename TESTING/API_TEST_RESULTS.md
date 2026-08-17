# API Test Results

Execution context:
- Backend: `MyAssistant.API` (.NET 10 / EF Core / SQLite / JWT), run from `backend/MyAssistant.API` with `ASPNETCORE_ENVIRONMENT=Development`, URL `http://localhost:5036`, real DB `backend/MyAssistant.API/myassistant.db`.
- Harness: `tests/qa/` (Node, no external packages), entry `node run.mjs`.
- **Final run: 146 passed / 0 failed** (7 suites). Includes fixes BUG-001..004, BUG-010 from `BUGS.md`.

## Results by suite

| Suite | Scope | Pass | Fail | Notes |
|---|---|---|---|---|
| AUTH | register/login/profile/password/refresh | 18 | 0 | Refresh rotation + reuse rejection verified |
| CRUD | notes/tasks/reminders/appointments/settings/search/dashboard/conversations/subscription | 42 | 0 | Full create→read→update→delete + validation |
| SECURITY | auth gates, IDOR, injection, malformed input | 22 | 0 | Cross-tenant isolation confirmed (404 not 403/200) |
| SUBSCRIPTION | tier, usage, quota enforcement | 8 | 0 | 50 notes / 50 tasks / 20 reminders caps; appointments ungated |
| ASSISTANT | intent recognition, multi-turn confirm, languages (en/hi/te), quotas | 41 | 0 | Includes Hindi + Telugu intents |
| EXTRA | range queries, usage counters, dashboard shape, settings clamps | 11 | 0 | |
| PERF | bulk create, list latency, search, parallel commands | 4 | 0 | ~77 records after 120-way parallel create (soft cap race, see BUG-009) |
| **TOTAL** | | **146** | **0** | |

## Endpoint matrix (verified behaviour)

| Method | Route | Auth | Result | Covered by |
|---|---|---|---|---|
| POST | /api/Auth/register | No | 200 valid / 409 duplicate / 400 weak or missing fields / invalid email | AUTH 1–4, 7–9 |
| POST | /api/Auth/login | No | 200 valid / 401 wrong pw or unknown email / 400 empty | AUTH 3–6 |
| POST | /api/Auth/refresh | No | 200 rotation / 401 reuse rejected | AUTH 14–16 |
| POST | /api/Auth/forgot-password | No | Reachable; token logged (BUG-007 gap) | BUGS.md |
| POST | /api/Auth/reset-password | No | 400 weak (<8) | BUG-002 |
| POST | /api/Auth/change-password | Yes | 400 wrong current; 400 invalid | AUTH 13 |
| GET | /api/Auth/profile | Yes | 200; 401 without token | AUTH 10–11 |
| PUT | /api/Auth/profile | Yes | 200 | AUTH 12 |
| GET/POST | /api/notes | Yes | 200/201; 400 both-empty, oversize; 403 quota (50/mo) | CRUD 1–13, SUB 4–5, PERF 1–2 |
| GET/PUT/DELETE | /api/notes/{id} | Yes | 200; 404 foreign/unknown (IDOR-safe) | CRUD 6–9, SEC 12–16 |
| GET/POST | /api/tasks | Yes | 200/201; 400 empty title; 403 quota (50/mo) | CRUD 14–15, 20, SUB 6 |
| PUT/DELETE | /api/tasks/{id} | Yes | 200; 404 | CRUD 17–19 |
| PATCH | /api/tasks/{id}/status | Yes | 200 valid; 400 invalid enum (BUG-001) | CRUD 16, BUG-001 |
| GET/POST | /api/reminders | Yes | 200/201; 403 quota (20/mo); past date allowed | CRUD 21–22, 25, SUB 7 |
| PATCH | /api/reminders/{id}/acknowledge | Yes | 200 | CRUD 23, EXTRA 4 |
| PUT/DELETE | /api/reminders/{id} | Yes | 200 | EXTRA 3, CRUD 24 |
| GET/POST | /api/appointments | Yes | 200/201; 400 end<=start | CRUD 26–27, 30 |
| GET | /api/appointments/range?start=&end= | Yes | 200; filters in/out range correctly | EXTRA 1–2 |
| PATCH | /api/appointments/{id}/reschedule | Yes | 200 | CRUD 27 |
| DELETE | /api/appointments/{id} | Yes | 200 | CRUD 28 |
| GET/PUT | /api/settings | Yes | 200; clamps to valid ranges; language persists | CRUD 31–35, EXTRA 7–8, ASSISTANT 38–39 |
| GET | /api/search?q= | Yes | 200 (incl. empty query, SQLi-safe) | CRUD 36–38, PERF 3 |
| GET | /api/dashboard | Yes | 200; stable shape | CRUD 39, EXTRA 6, 11 |
| GET | /api/conversations | Yes | 200; history recorded on assistant use | CRUD 40, EXTRA 5 |
| DELETE | /api/conversations | Yes | 200 (clear) | CRUD 41 |
| GET | /api/subscription | Yes | 200; tier=Free; usage per type (notes/tasks/reminders/appointments/aiCommands/stt/tts/searches) | CRUD 42, SUB 1–2, 4, EXTRA 9 |
| POST | /api/assistant/command | Yes | 200 intents (en/hi/te); 400 empty/missing text; 403 quota guidance | ASSISTANT 1–41, SUB 7 |
| POST | /api/assistant/transcribe | Yes | Placeholder stub (BUG-008) | BUGS.md |

## Security results

| Check | Result |
|---|---|
| 401 without token (all protected routes) | PASS — every gated endpoint rejects |
| Invalid / tampered token | 401 |
| IDOR cross-tenant read/update/delete | PASS — 404 (no data leak) |
| SQL injection in login / search | 400 / handled, no 500 |
| Malformed JSON body | 400 with RFC 9110 problem details |
| XSS payloads in names/notes | Accepted server-side (stored as-is); sanitization is the frontend render responsibility — flagged in report |
| Oversized assistant input | 400 |
| Invalid GUID | 400/404, never 500 |

## Coverage note (manual / blocked)

The suite is **API-level (backend)**. The following require manual or unavailable infrastructure and are covered in the final report as BLOCKED/partial:

- Real browser E2E (Playwright not installed) — SPA flows verified via API only + existing Vitest unit tests.
- Email delivery (forgot-password) — no SMTP provider configured (BUG-007).
- Stripe/payment upgrade flow — no provider configured; subscription is soft-tier.
- OpenAI parsing — `AI_PROVIDER=local` (heuristic) is the active path; OpenAI path not exercised.
- Rate limiting (300 req/min) — no 429 observed at tested concurrency.

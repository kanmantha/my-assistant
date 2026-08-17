import { api, registerRandom, login, Suite, summarize } from "./harness.mjs";

export async function runCrud() {
  const S = new Suite("CRUD");
  const acc = await registerRandom("crud");
  if (acc.registerError) { S.fail("register for CRUD", summarize(acc.registerError)); return S; }
  const tkn = acc.accessToken;
  if (!tkn) { S.fail("login after register", "no token"); return S; }

  // ===== NOTES =====
  let noteId = null, noteList = [];
  {
    const r = await api("POST", "/api/notes", { token: tkn, body: { content: "Meeting minutes about project X agenda" } });
    S.check(r.status === 200 || r.status === 201, "note create (auto title)", `status=${r.status}`, r.status, r.json);
    noteId = r.json?.data?.id || null;
    S.check(!!r.json?.data?.title, "note auto-title generated", `title=${r.json?.data?.title}`);
  }
  {
    const r = await api("GET", "/api/notes", { token: tkn });
    S.check(r.status === 200 && Array.isArray(r.json?.data), "notes list", `status=${r.status} count=${r.json?.data?.length}`);
    noteList = r.json?.data || [];
    // list contains created note
    S.check(!!noteList.length, "notes list non-empty", `count=${noteList.length}`);
  }
  if (noteId) {
    const r = await api("GET", `/api/notes/${noteId}`, { token: tkn });
    S.check(r.status === 200, "note get by id", `status=${r.status}`, r.status, r.json);
    const u = await api("PUT", `/api/notes/${noteId}`, { token: tkn, body: { title: "Updated title", content: "new content", isPinned: true } });
    S.check(u.status === 200, "note update", `status=${u.status}`, u.status, u.json);
    const g = await api("GET", `/api/notes/${noteId}`, { token: tkn });
    S.check(g.json?.data?.title === "Updated title", "note update persisted", `title=${g.json?.data?.title}`);
    const d = await api("DELETE", `/api/notes/${noteId}`, { token: tkn });
    S.check(d.status === 200 || d.status === 204, "note delete", `status=${d.status}`, d.status, d.json);
    const gone = await api("GET", `/api/notes/${noteId}`, { token: tkn });
    S.check(gone.status === 404, "deleted note -> 404", `status=${gone.status}`, gone.status, gone.json);
  }
  // --- Note negatives ---
  {
    const r = await api("POST", "/api/notes", { token: tkn, body: { title: "T", content: "" } });
    S.check(r.status === 200 || r.status === 201, "note title-only accepted", `status=${r.status}`, r.status, r.json);
  }
  {
    const r = await api("POST", "/api/notes", { token: tkn, body: { title: "", content: "" } });
    S.check(r.status === 400, "note both empty -> 400", `status=${r.status}`, r.status, r.json);
  }
  {
    const r = await api("POST", "/api/notes", { token: tkn, body: { title: "x".repeat(3000) } });
    S.check(r.status === 400, "note oversize title -> 400", `status=${r.status}`, r.status, r.json);
  }

  // ===== TASKS =====
  let taskId = null;
  {
    const r = await api("POST", "/api/tasks", { token: tkn, body: { title: "Write quarterly report", priority: 1, dueDate: "2026-08-20" } });
    S.check(r.status === 200 || r.status === 201, "task create", `status=${r.status}`, r.status, r.json);
    taskId = r.json?.data?.id;
  }
  {
    const r = await api("GET", "/api/tasks", { token: tkn });
    S.check(r.status === 200 && Array.isArray(r.json?.data), "tasks list", `status=${r.status} count=${r.json?.data?.length}`);
  }
  if (taskId) {
    const st = await api("PATCH", `/api/tasks/${taskId}/status`, { token: tkn, body: { status: 2 } });
    S.check(st.status === 200, "task status update", `status=${st.status}`, st.status, st.json);
    const st2 = await api("PATCH", `/api/tasks/${taskId}/status`, { token: tkn, body: { status: 99 } });
    S.check(st2.status === 400, "task invalid status -> 400", `status=${st2.status}`, st2.status, st2.json);
    const u = await api("PUT", `/api/tasks/${taskId}`, { token: tkn, body: { title: "Renamed task", priority: 2, status: 1 } });
    S.check(u.status === 200, "task update", `status=${u.status}`, u.status, u.json);
    const d = await api("DELETE", `/api/tasks/${taskId}`, { token: tkn });
    S.check(d.status === 200 || d.status === 204, "task delete", `status=${d.status}`, d.status, d.json);
    const gone = await api("GET", `/api/tasks/${taskId}`, { token: tkn });
    S.check(gone.status === 404, "deleted task -> 404", `status=${gone.status}`, gone.status, gone.json);
  }
  {
    const r = await api("POST", "/api/tasks", { token: tkn, body: { title: "" } });
    S.check(r.status === 400, "task empty title -> 400", `status=${r.status}`, r.status, r.json);
  }

  // ===== REMINDERS =====
  let remId = null;
  const now = new Date();
  now.setHours(now.getHours() + 2);
  {
    const r = await api("POST", "/api/reminders", { token: tkn, body: { title: "Call vendor", reminderAt: now.toISOString() } });
    S.check(r.status === 200 || r.status === 201, "reminder create", `status=${r.status}`, r.status, r.json);
    remId = r.json?.data?.id;
  }
  if (remId) {
    const g = await api("GET", `/api/reminders/${remId}`, { token: tkn });
    S.check(g.status === 200, "reminder get", `status=${g.status}`);
    const a = await api("PATCH", `/api/reminders/${remId}/acknowledge`, { token: tkn });
    S.check(a.status === 200 || a.status === 204, "reminder acknowledge", `status=${a.status}`, a.status, a.json);
    const d = await api("DELETE", `/api/reminders/${remId}`, { token: tkn });
    S.check(d.status === 200 || d.status === 204, "reminder delete", `status=${d.status}`, d.status, d.json);
  }
  {
    const past = new Date(); past.setHours(past.getHours() - 1);
    const r = await api("POST", "/api/reminders", { token: tkn, body: { title: "Past", reminderAt: past.toISOString() } });
    S.check((r.status === 200 || r.status === 201) || r.status === 400, "reminder in past (accepted or rejected clearly)", `status=${r.status}`, r.status, r.json);
  }

  // ===== APPOINTMENTS =====
  let apptId = null;
  const start = new Date(); start.setDate(start.getDate() + 3); start.setHours(10, 0, 0, 0);
  const end = new Date(start); end.setHours(11, 0, 0, 0);
  {
    const r = await api("POST", "/api/appointments", { token: tkn, body: { title: "Project sync", startDateTime: start.toISOString(), endDateTime: end.toISOString(), participants: ["ravi@x.com"] } });
    S.check(r.status === 200 || r.status === 201, "appointment create", `status=${r.status}`, r.status, r.json);
    apptId = r.json?.data?.id;
  }
  if (apptId) {
    const r2 = await api("PATCH", `/api/appointments/${apptId}/reschedule`, { token: tkn, body: { startDateTime: end.toISOString(), endDateTime: new Date(end.getTime()+3600000).toISOString() } });
    S.check(r2.status === 200, "appointment reschedule", `status=${r2.status}`, r2.status, r2.json);
    const d = await api("DELETE", `/api/appointments/${apptId}`, { token: tkn });
    S.check(d.status === 200 || d.status === 204, "appointment delete", `status=${d.status}`, d.status, d.json);
  }
  // end before start -> should reject
  {
    const s2 = new Date(); s2.setDate(s2.getDate() + 5); s2.setHours(10);
    const e2 = new Date(s2); e2.setHours(9);
    const r = await api("POST", "/api/appointments", { token: tkn, body: { title: "Bad", startDateTime: s2.toISOString(), endDateTime: e2.toISOString() } });
    S.check(r.status === 400, "appointment end<=start -> 400", `status=${r.status}`, r.status, r.json);
  }

  // ===== SETTINGS =====
  {
    const g = await api("GET", "/api/settings", { token: tkn });
    S.check(g.status === 200, "settings get", `status=${g.status}`, g.status, g.json);
  }
  {
    const r = await api("PUT", "/api/settings", { token: tkn, body: { language: "hi", speechSpeed: 2, voiceVolume: 200, fontScale: 300 } });
    S.check(r.status === 200, "settings put (clamped values)", `status=${r.status}`, r.status, r.json);
    const g = await api("GET", "/api/settings", { token: tkn });
    const d = g.json?.data || {};
    S.check(d.language === "hi", "settings language persisted = hi", `lang=${d.language}`);
    S.check(typeof d.speechSpeed === "number", "settings speechSpeed is number", `speed=${d.speechSpeed}`);
  }
  {
    // reset to en
    const r = await api("PUT", "/api/settings", { token: tkn, body: { language: "en" } });
    S.check(r.status === 200, "settings reset to en", `status=${r.status}`, r.status, r.json);
  }

  // ===== SEARCH =====
  {
    const r = await api("GET", `/api/search?q=${encodeURIComponent("project")}`, { token: tkn });
    S.check(r.status === 200, "search", `status=${r.status}`, r.status, r.json);
    const r2 = await api("GET", "/api/search?q=", { token: tkn });
    S.check(r2.status === 200, "search empty query", `status=${r2.status}`, r2.status, r2.json);
    const r3 = await api("GET", `/api/search?q=${encodeURIComponent("'; DROP TABLE Notes; --")}`, { token: tkn });
    S.check(r3.status === 200, "search SQLi payload handled", `status=${r3.status}`, r3.status, r3.json);
  }

  // ===== DASHBOARD / CONVERSATIONS =====
  {
    const r = await api("GET", "/api/dashboard", { token: tkn });
    S.check(r.status === 200, "dashboard", `status=${r.status}`, r.status, r.json);
    const c = await api("GET", "/api/conversations", { token: tkn });
    S.check(c.status === 200, "conversations list", `status=${c.status}`, c.status, c.json);
    const d = await api("DELETE", "/api/conversations", { token: tkn });
    S.check(d.status === 200 || d.status === 204, "conversations clear", `status=${d.status}`, d.status, d.json);
  }

  // ===== SUBSCRIPTION =====
  {
    const r = await api("GET", "/api/subscription", { token: tkn });
    S.check(r.status === 200 && r.json?.data, "subscription get", `status=${r.status}`, r.status, r.json);
    S.check(r.json?.data?.usage !== undefined, "subscription usage", `usage=${JSON.stringify(r.json?.data?.usage)}`, r.status, r.json);
  }

  return S;
}
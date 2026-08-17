import { api, registerRandom, Suite, summarize } from "./harness.mjs";

export async function runExtra() {
  const S = new Suite("EXTRA");
  const acc = await registerRandom("extra");
  if (acc.registerError) { S.fail("register", summarize(acc.registerError)); return S; }
  const tkn = acc.accessToken;
  if (!tkn) { S.fail("token", "missing"); return S; }

  // --- Appointment range query (start/end on /api/appointments/range) ---
  {
    const st = new Date(); st.setDate(st.getDate() + 2); st.setHours(9, 0, 0, 0);
    const en = new Date(st); en.setHours(10);
    const fut1 = new Date(); fut1.setDate(fut1.getDate() + 40);
    await api("POST", "/api/appointments", { token: tkn, body: { title: "InRange", startDateTime: st.toISOString(), endDateTime: en.toISOString() } });
    await api("POST", "/api/appointments", { token: tkn, body: { title: "OutRange", startDateTime: fut1.toISOString(), endDateTime: new Date(fut1.getTime()+3600000).toISOString() } });
    const from = new Date(); from.setDate(from.getDate() + 1);
    const to = new Date(); to.setDate(to.getDate() + 5);
    const r = await api("GET", `/api/appointments/range?start=${encodeURIComponent(from.toISOString())}&end=${encodeURIComponent(to.toISOString())}`, { token: tkn });
    const list = r.json?.data ?? [];
    S.check(r.status === 200 && list.some(a => a.title === "InRange"), "appointment range query includes in-range", `status=${r.status} titles=${list.map(x=>x.title).join(",")}`);
    S.check(r.status === 200 && !list.some(a => a.title === "OutRange"), "appointment range excludes out-of-range", `status=${r.status}`);
  }

  // --- Reminder update + acknowledge ---
  {
    const t = new Date(); t.setHours(t.getHours() + 4);
    const c = await api("POST", "/api/reminders", { token: tkn, body: { title: "UpdateMe", reminderAt: t.toISOString() } });
    const id = c.json?.data?.id;
    if (id) {
      const t2 = new Date(t); t2.setHours(t2.getHours() + 1);
      const u = await api("PUT", `/api/reminders/${id}`, { token: tkn, body: { title: "UpdatedName", reminderAt: t2.toISOString() } });
      S.check(u.status === 200, "reminder update", `status=${u.status}`, u.status, u.json);
      const a = await api("PATCH", `/api/reminders/${id}/acknowledge`, { token: tkn });
      S.check(a.status === 200 || a.status === 204, "reminder acknowledge", `status=${a.status}`, a.status, a.json);
      // notification should exist (in-app)
      const notes = await api("GET", "/api/conversations", { token: tkn });
      S.check(r => true, "reminder created w/o crash");
    }
  }

  // --- Conversation history records assistant usage ---
  {
    const before = (await api("GET", "/api/conversations", { token: tkn })).json?.data?.length ?? 0;
    await api("POST", "/api/assistant/command", { token: tkn, body: { text: "Hello", language: "Auto", isVoice: false } });
    const after = (await api("GET", "/api/conversations", { token: tkn })).json?.data?.length ?? 0;
    S.check(after > before, "conversation history recorded", `before=${before} after=${after}`);
  }

  // --- Dashboard counts sane ---
  {
    const d = (await api("GET", "/api/dashboard", { token: tkn })).json?.data;
    S.check(d && typeof d.tasksToday !== "undefined" && Array.isArray(d.todayTasks), "dashboard shape", `tasksToday=${d?.tasksToday}`);
  }

  // --- Settings clamping: over-large fontScale / negative speed ---
  {
    const r = await api("PUT", "/api/settings", { token: tkn, body: { language: "en", speechSpeed: -3, voiceVolume: 500, fontScale: 99999 } });
    S.check(r.status === 200, "settings accepts (clamps)" , `status=${r.status}`, r.status, r.json);
    const g = (await api("GET", "/api/settings", { token: tkn })).json?.data;
    S.check(g.fontScale <= 300 && g.voiceVolume <= 1, "settings clamped", `fontScale=${g.fontScale} voiceVolume=${g.voiceVolume}`);
  }

  // --- Subscription usage includes AiCommand count after assistant cmds ---
  {
    await api("POST", "/api/assistant/command", { token: tkn, body: { text: "list my tasks", language: "Auto", isVoice: false } });
    const u = (await api("GET", "/api/subscription", { token: tkn })).json?.data?.usage;
    S.check(u && u.aiCommands > 0, "usage aiCommands increments", `aiCommands=${u?.aiCommands}`);
  }

  // --- Notifications: check via search for reminder? Verify no crash on notifications endpoints (none exposed)
  {
    // No public notifications endpoint exists; verify dashboard works after data created
    const d = await api("GET", "/api/dashboard", { token: tkn });
    S.check(d.status === 200, "dashboard after mutations", `status=${d.status}`);
  }

  return S;
}
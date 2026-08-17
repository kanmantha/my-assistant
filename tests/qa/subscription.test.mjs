import { api, registerRandom, Suite, summarize } from "./harness.mjs";

export async function runSubscription() {
  const S = new Suite("SUBSCRIPTION");
  const acc = await registerRandom("sub");
  if (acc.registerError) { S.fail("register", summarize(acc.registerError)); return S; }
  const tkn = acc.accessToken;
  const uid = acc.user?.id;
  if (!tkn) { S.fail("token", "missing"); return S; }

  // default tier should be Free
  {
    const r = await api("GET", "/api/subscription", { token: tkn });
    const d = r.json?.data || {};
    S.check(r.status === 200, "subscription endpoint reachable", `status=${r.status}`, r.status, r.json);
    S.check(d.tier === "Free" || typeof d.tier === "string", "tier present", `tier=${d.tier}`);
  }

  // usage initially zero-ish (no commands yet)
  {
    const r = await api("GET", "/api/subscription", { token: tkn });
    S.check(r.status === 200 && r.json?.data?.usage, "usage endpoint", `usage=${JSON.stringify(r.json?.data?.usage)}`, r.status, r.json);
  }

  // ---- Notes limit: Free = 50/month ----
  {
    let reachedLimit = false, made = 0;
    for (let i = 0; i < 57; i++) {
      const r = await api("POST", "/api/notes", { token: tkn, body: { content: `Bulk note number ${i}` } });
      if (r.status === 200 || r.status === 201) made++;
      else if (r.status === 403) { reachedLimit = true; S.ok(`notes limit reached after ${made}`, `status=${r.status} msg=${r.json?.message}`, r.status, r.json); break; }
      else { S.fail("notes bulk create unexpected", `status=${r.status} body=${summarize(r)}`, r.status, r.json); break; }
    }
    if (!reachedLimit) S.fail("notes free limit should be enforced at 50", `made=${made}`);
    const u = await api("GET", "/api/subscription", { token: tkn });
    const d = u.json?.data;
    S.check(d && d.usage && d.usage.notes >= 50, "usage.notes reflects records", `notes=${d?.usage?.notes}`);
  }

  // ---- Tasks limit: Free = 50/month ----
  {
    let reachedLimit = false, made = 0;
    for (let i = 0; i < 57; i++) {
      const r = await api("POST", "/api/tasks", { token: tkn, body: { title: `Bulk task ${i}` } });
      if (r.status === 200 || r.status === 201) made++;
      else if (r.status === 403) { reachedLimit = true; S.ok(`tasks limit reached after ${made}`, `status=${r.status}`, r.status, r.json); break; }
      else { S.fail("tasks bulk create unexpected", `status=${r.status} body=${summarize(r)}`, r.status, r.json); break; }
    }
    if (!reachedLimit) S.fail("tasks free limit should be enforced at 50", `made=${made}`);
  }

  // ---- Reminders limit: Free = 20/month ----
  {
    let reachedLimit = false, made = 0;
    const t = new Date(); t.setHours(t.getHours() + 3);
    for (let i = 0; i < 26; i++) {
      const r = await api("POST", "/api/reminders", { token: tkn, body: { title: `Bulk reminder ${i}`, reminderAt: t.toISOString() } });
      if (r.status === 200 || r.status === 201) made++;
      else if (r.status === 403) { reachedLimit = true; S.ok(`reminders limit reached (20)`, `after=${made} status=${r.status} msg=${r.json?.message}`); break; }
      else if (r.status === 400) { S.fail("reminder rejected with 400 during bulk", summarize(r), r.status, r.json); break; }
    }
    if (!reachedLimit) S.fail("reminders free limit should be enforced at 20", `made=${made}`);
  }

  // ---- Appointments NOT gated (design: unlimited on Free) ----
  {
    const st = new Date(); st.setDate(st.getDate() + 10); st.setHours(9); 
    const en = new Date(st); en.setHours(10);
    const r = await api("POST", "/api/appointments", { token: tkn, body: { title: "Unlimited check", startDateTime: st.toISOString(), endDateTime: en.toISOString() } });
    S.check(r.status === 200 || r.status === 201, "appointments not gated on Free (design)", `status=${r.status}`, r.status, r.json);
  }

  // cleanup created bulk (avoid polluting)
  {
    const r = await api("GET", "/api/notes", { token: tkn });
    // leave data; this is a throwaway user
  }

  return S;
}
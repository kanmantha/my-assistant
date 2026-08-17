import { api, registerRandom, Suite, summarize } from "./harness.mjs";

export async function runPerf() {
  const S = new Suite("PERF");
  const acc = await registerRandom("perf");
  if (acc.registerError) { S.fail("register", summarize(acc.registerError)); return S; }
  const tkn = acc.accessToken;

  // Create 120 notes to measure list latency (Free-tier caps at ~50)
  {
    const t0 = Date.now();
    const batches = [];
    for (let i = 0; i < 120; i++) batches.push(api("POST", "/api/notes", { token: tkn, body: { content: `Perf note ${i} content text` } }));
    await Promise.all(batches);
    S.ok("create 120 notes batch (free cap ~50)", `elapsed=${Date.now() - t0}ms`);
  }
  // Read the list
  {
    const t0 = Date.now();
    const r = await api("GET", "/api/notes", { token: tkn });
    const dt = Date.now() - t0;
    const count = r.json?.data?.length ?? 0;
    S.check(r.status === 200 && count >= 50, "list notes after bulk", `count=${count} elapsed=${dt}ms`);
  }
  // Search across 120
  {
    const t0 = Date.now();
    const r = await api("GET", `/api/search?q=${encodeURIComponent("Perf note 7")}`, { token: tkn });
    const dt = Date.now() - t0;
    S.check(r.status === 200, "search over dataset", `elapsed=${dt}ms`, r.status, r.json);
  }
  // Concurrency: 20 parallel assistant commands shouldn't 500
  {
    const cmds = Array.from({ length: 20 }, (_, i) => api("POST", "/api/assistant/command", { token: tkn, body: { text: i % 2 ? "Hello" : "Show my tasks", language: "Auto", isVoice: false } }));
    const results = await Promise.allSettled(cmds);
    const okCount = results.filter(r => r.status === "fulfilled" && r.value.status === 200).length;
    S.check(okCount >= 18, "20 parallel assistant cmds mostly 200", `ok=${okCount}`);
  }

  return S;
}
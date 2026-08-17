import { api, registerRandom, Suite, summarize } from "./harness.mjs";

export async function runSecurity() {
  const S = new Suite("SECURITY");
  const userA = await registerRandom("secA");
  const userB = await registerRandom("secB");
  if (userA.registerError || userB.registerError) {
    S.fail("register users for security", `A=${summarize(userA.registerError)} B=${summarize(userB.registerError)}`);
    return S;
  }
  const tkA = userA.accessToken, tkB = userB.accessToken;
  if (!tkA || !tkB) { S.fail("tokens", "missing"); return S; }

  // No token for any protected route
  for (const [name, method, path, body, sendBody] of [
    ["notes", "GET", "/api/notes", null, false],
    ["notes create", "POST", "/api/notes", { content: "x" }, true],
    ["tasks", "GET", "/api/tasks", null, false],
    ["reminders", "GET", "/api/reminders", null, false],
    ["appointments", "GET", "/api/appointments", null, false],
    ["dashboard", "GET", "/api/dashboard", null, false],
    ["settings", "GET", "/api/settings", null, false],
    ["search", "GET", "/api/search?q=x", null, false],
    ["subscription", "GET", "/api/subscription", null, false],
    ["conversations", "GET", "/api/conversations", null, false],
    ["assistant", "POST", "/api/assistant/command", { text: "hello" }, true]
  ]) {
    const rr = await api(method, path, { body: sendBody ? body : undefined });
    const ok = rr.status === 401;
    S.check(ok, `401 without token [${path}]`, `status=${rr.status}`, rr.status, rr.json);
  }

  // Invalid/garble token
  {
    const r = await api("GET", "/api/notes", { token: "not.a.valid.token" });
    S.check(r.status === 401, "invalid token -> 401", `status=${r.status}`, r.status, r.json);
  }

  // IDOR: user A creates, user B tries to read/update/delete
  {
    const rN = await api("POST", "/api/notes", { token: tkA, body: { content: "Secret of A note" } });
    const id = rN.json?.data?.id;
    if (id) {
      const read = await api("GET", `/api/notes/${id}`, { token: tkB });
      S.check(read.status === 404 || read.status === 403, "IDOR: B reads A's note rejected", `status=${read.status}`, read.status, read.json);
      const upd = await api("PUT", `/api/notes/${id}`, { token: tkB, body: { title: "hacked" } });
      S.check(upd.status === 404 || upd.status === 403, "IDOR: B updates A's note rejected", `status=${upd.status}`, upd.status, upd.json);
      const del = await api("DELETE", `/api/notes/${id}`, { token: tkB });
      S.check(del.status === 404 || del.status === 403, "IDOR: B deletes A's note rejected", `status=${del.status}`, del.status, del.json);
      // verify A still owns it
      const still = await api("GET", `/api/notes/${id}`, { token: tkA });
      S.check(still.status === 200, "A still owns note after IDOR attempts", `status=${still.status}`);
    }
  }

  // Assistant with no valid auth
  {
    const r = await api("POST", "/api/assistant/command", { body: { text: "Hello" } });
    S.check(r.status === 401, "assistant no token -> 401", `status=${r.status}`, r.status, r.json);
  }

  // Malformed JSON body
  {
    const res = await fetch("http://localhost:5036/api/notes", {
      method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${tkA}` }, body: "{not json"
    });
    const text = await res.text();
    S.check(res.status === 400, "malformed JSON -> 400", `status=${res.status} body=${text.slice(0,120)}`, res.status, null);
  }

  // Unknown properties (additionalProperties not enforced by STJ; ignored)
  {
    const r = await api("POST", "/api/notes", { token: tkA, body: { content: "hello", evil: "prop", "<x>": "y" } });
    S.check(r.status === 200 || r.status === 201 || r.status === 400, "note with unknown props handled", `status=${r.status}`, r.status, r.json);
  }

  // XSS payload stored in note and returned
  {
    const r = await api("POST", "/api/notes", { token: tkA, body: { title: "<script>alert('x')</script>", content: "<img src=x onerror=alert(1)>" } });
    S.check(r.status === 200 || r.status === 201, "note with XSS payload accepted (server-side encoding is frontend concern)", `status=${r.status}`, r.status, r.json);
  }

  // oversized assistant input
  {
    const big = "A".repeat(20000);
    const r = await api("POST", "/api/assistant/command", { token: tkA, body: { text: big } });
    S.check(r.status === 400 || r.status === 200, "assistant oversized input handled", `status=${r.status}`, r.status, r.json);
  }

  // GET tasks with invalid Guid => 400 not 500
  {
    const r = await api("GET", "/api/tasks/not-a-guid", { token: tkA });
    S.check(r.status === 400 || r.status === 404, "invalid guid -> 400/404 not 500", `status=${r.status}`, r.status, r.json);
  }

  return S;
}
import { api, registerRandom, login, Suite, summarize } from "./harness.mjs";

export async function runAuth() {
  const S = new Suite("AUTH");
  const email = `auth${Date.now()}@example.com`;

  // --- Register: valid ---
  {
    const r = await api("POST", "/api/Auth/register", {
      body: { email, password: "Qa@12345!", firstName: "Alice", lastName: "Smith" }
    });
    S.check(r.status === 200 || r.status === 201, "register valid", `status=${r.status} body=${summarize(r)}`, r.status, r.json);
    if (r.status !== 200 && r.status !== 201) return S;
  }

  // --- Register: duplicate email ---
  {
    const r = await api("POST", "/api/Auth/register", {
      body: { email, password: "Qa@12345!", firstName: "A2", lastName: "S2" }
    });
    S.check(r.status === 409 || r.status === 400, "register duplicate email rejected", `status=${r.status} body=${summarize(r)}`, r.status, r.json);
  }

  // --- Login: valid ---
  let tkn = null;
  {
    const r = await api("POST", "/api/Auth/login", { body: { email, password: "Qa@12345!" } });
    S.check(r.status === 200 && r.json?.data?.accessToken, "login valid", `status=${r.status} hasAccessToken=${!!r.json?.data?.accessToken}`, r.status, r.json);
    tkn = r.json?.data?.accessToken;
  }

  // --- Login: wrong password ---
  {
    const r = await api("POST", "/api/Auth/login", { body: { email, password: "Wrong@12345" } });
    S.check(r.status === 401 || r.status === 400, "login wrong password rejected", `status=${r.status}`, r.status, r.json);
  }

  // --- Login: nonexistent email ---
  {
    const r = await api("POST", "/api/Auth/login", { body: { email: "nobody@example.com", password: "Qa@12345!" } });
    S.check(r.status === 401 || r.status === 400, "login unknown email rejected", `status=${r.status}`, r.status, r.json);
  }

  // --- Login: empty fields ---
  {
    const r = await api("POST", "/api/Auth/login", { body: { email: "", password: "" } });
    S.check(r.status === 400, "login empty fields -> 400", `status=${r.status}`, r.status, r.json);
  }

  // --- Register: weak password ---
  {
    const r = await api("POST", "/api/Auth/register", {
      body: { email: `weak${Date.now()}@example.com`, password: "weak", firstName: "W", lastName: "E" }
    });
    S.check((r.status === 400 || r.status === 422), "register weak password rejected", `status=${r.status}`, r.status, r.json);
  }

  // --- Register: missing fields ---
  {
    const r = await api("POST", "/api/Auth/register", { body: { email: `miss${Date.now()}@example.com`, password: "Qa@12345!" } });
    S.check(r.status === 400, "register missing first/last name -> 400", `status=${r.status}`, r.status, r.json);
  }

  // --- Register: invalid email ---
  {
    const r = await api("POST", "/api/Auth/register", {
      body: { email: "not-an-email", password: "Qa@12345!", firstName: "X", lastName: "Y" }
    });
    S.check(r.status === 400, "register invalid email rejected", `status=${r.status}`, r.status, r.json);
  }

  // --- Profile: auth required ---
  {
    const r = await api("GET", "/api/Auth/profile");
    S.check(r.status === 401, "profile without token -> 401", `status=${r.status}`, r.status, r.json);
  }

  // --- Profile: get ---
  {
    const r = await api("GET", "/api/Auth/profile", { token: tkn });
    S.check(r.status === 200 && r.json?.data?.email === email.toLowerCase(), "profile get", `status=${r.status}`, r.status, r.json);
  }

  // --- Profile: update ---
  {
    const r = await api("PUT", "/api/Auth/profile", { token: tkn, body: { firstName: "Bob", lastName: "Jones" } });
    S.check(r.status === 200, "profile update", `status=${r.status}`, r.status, r.json);
  }

  // --- Change password: wrong current ---
  {
    const r = await api("POST", "/api/Auth/change-password", { token: tkn, body: { currentPassword: "Nope@12345", newPassword: "New@12345!" } });
    S.check(r.status === 400 || r.status === 401, "change-password wrong current rejected", `status=${r.status}`, r.status, r.json);
  }
  // NOTE: we do NOT change demo pass. Do change on this throwaway user at end? Keep for later.

  // --- Refresh token flow ---
  {
    const log = await login(email, "Qa@12345!");
    if (log.token) {
      const r = await api("POST", "/api/Auth/refresh", { body: { accessToken: log.token, refreshToken: log.refreshToken } });
      S.check(r.status === 200 && r.json?.data?.accessToken, "refresh rotation valid", `status=${r.status}`, r.status, r.json);
      const t2 = r.json?.data?.accessToken;
      // old refresh token should now be rotated; try reusing old -> expect failure
      const r2 = await api("POST", "/api/Auth/refresh", { body: { accessToken: log.token, refreshToken: log.refreshToken } });
      S.check(r2.status === 401 || r2.status === 400, "refresh reuse rejected (rotation)", `status=${r2.status}`, r2.status, r2.json);
      // use new access token with profile
      const p = await api("GET", "/api/Auth/profile", { token: t2 });
      S.check(p.status === 200, "refreshed token works", `status=${p.status}`, p.status, p.json);
    }
  }

  // --- Validation-aware edge: register with XSS in name ---
  {
    const r = await api("POST", "/api/Auth/register", {
      body: { email: `xss${Date.now()}@example.com`, password: "Qa@12345!", firstName: "<script>alert(1)</script>", lastName: "SAFE" }
    });
    S.check(r.status === 200 || r.status === 400, "register name with XSS (accepts or rejects cleanly)", `status=${r.status}`, r.status, r.json);
  }

  // --- SQL injection attempt on login ---
  {
    const r = await api("POST", "/api/Auth/login", { body: { email: "' OR '1'='1", password: "' OR '1'='1" } });
    S.check(r.status !== 200, "login SQL injection rejected", `status=${r.status}`, r.status, r.json);
  }

  return S;
}
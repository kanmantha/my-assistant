// Shared helpers + reporting for the MyAssistant QA harness.
export const BASE = "http://localhost:5036";
export const API = BASE; // direct to backend

export const DEMO = { email: "demo@example.com", password: "Demo@12345" };

export class Suite {
  constructor(name) {
    this.name = name;
    this.results = [];
  }
  _record(ok, test, detail, status, resp) {
    this.results.push({ ok, test, detail, status, resp: resp || null });
  }
  check(ok, test, detail = "", status = null, resp = null) {
    this._record(Boolean(ok), test, detail, status, resp);
  }
  ok(test, detail = "", status = null, resp = null) {
    this._record(true, test, detail, status, resp);
  }
  fail(test, detail = "", status = null, resp = null) {
    this._record(false, test, detail, status, resp);
  }
  get passed() { return this.results.filter(r => r.ok).length; }
  get failed() { return this.results.filter(r => !r.ok).length; }
}

export async function api(method, path, { token, body, headers } = {}) {
  const h = { ...(headers || {}) };
  if (token) h.Authorization = `Bearer ${token}`;
  if (body !== undefined) h["Content-Type"] = "application/json";
  const res = await fetch(BASE + path, {
    method,
    headers: h,
    body: body !== undefined ? JSON.stringify(body) : undefined
  });
  let json = null;
  const raw = await res.text();
  try { json = JSON.parse(raw); } catch { /* non-json */ }
  return { status: res.status, ok: res.ok, json, raw };
}

export async function registerRandom(prefix = "qa") {
  const email = `${prefix}${Date.now()}${Math.floor(Math.random()*999)}@example.com`;
  const p = { email, password: "Qa@12345!", firstName: "QA", lastName: "Bot" };
  const r = await api("POST", "/api/Auth/register", { body: p });
  if (r.status === 200 || r.status === 201) {
    return { email, password: p.password, ...(r.json?.data || {}) };
  }
  return { email, password: p.password, registerError: r };
}

export async function login(email, password) {
  const r = await api("POST", "/api/Auth/login", { body: { email, password } });
  if (r.json?.data?.accessToken) return { token: r.json.data.accessToken, refreshToken: r.json.data.refreshToken, data: r.json.data, status: r.status, json: r.json };
  return { error: r };
}

export function summarize(r, max = 160) {
  const s = r.json ? JSON.stringify(r.json) : r.raw || "";
  return s.length > max ? s.slice(0, max) + "…" : s;
}
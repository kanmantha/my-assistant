import type { ApiResponse } from "../models";

const API_BASE = import.meta.env.VITE_API_URL ?? "/api";
const TOKEN_KEY = "myassistant.access_token";
const REFRESH_KEY = "myassistant.refresh_token";

export class ApiError extends Error {
  status: number;
  errors?: string[];

  constructor(message: string, status: number, errors?: string[]) {
    super(message);
    this.status = status;
    this.errors = errors;
  }
}

export const tokenStore = {
  get accessToken() {
    return localStorage.getItem(TOKEN_KEY);
  },
  get refreshToken() {
    return localStorage.getItem(REFRESH_KEY);
  },
  set(tokens: { accessToken: string; refreshToken: string }) {
    localStorage.setItem(TOKEN_KEY, tokens.accessToken);
    localStorage.setItem(REFRESH_KEY, tokens.refreshToken);
  },
  clear() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
  }
};

interface HttpOptions {
  method?: string;
  body?: unknown;
  auth?: boolean;
  signal?: AbortSignal;
}

interface RefreshResponse {
  success: boolean;
  data?: { accessToken: string; refreshToken: string };
}

let refreshInFlight: Promise<boolean> | null = null;

function buildHeaders(body: unknown, token?: string | null): Record<string, string> {
  const headers: Record<string, string> = {};
  const isFormData = typeof FormData !== "undefined" && body instanceof FormData;
  if (!isFormData) {
    headers["Content-Type"] = "application/json";
  }
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

function buildBody(body: unknown): BodyInit | undefined {
  if (body === undefined) return undefined;
  if (typeof FormData !== "undefined" && body instanceof FormData) return body;
  return JSON.stringify(body);
}

function request(path: string, options: HttpOptions, token?: string | null): Promise<Response> {
  return fetch(`${API_BASE}${path}`, {
    method: options.method ?? "GET",
    headers: buildHeaders(options.body, token),
    signal: options.signal,
    body: buildBody(options.body)
  });
}

async function refreshTokens(): Promise<boolean> {
  const refreshToken = tokenStore.refreshToken;
  if (!refreshToken) return false;
  try {
    const response = await fetch(`${API_BASE}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ accessToken: tokenStore.accessToken, refreshToken })
    });
    if (!response.ok) return false;
    const json = (await response.json()) as RefreshResponse;
    if (!json.success || !json.data) return false;
    tokenStore.set({ accessToken: json.data.accessToken, refreshToken: json.data.refreshToken });
    return true;
  } catch {
    return false;
  }
}

function requestRefresh(): Promise<boolean> {
  if (!refreshInFlight) {
    refreshInFlight = refreshTokens().finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

function redirectToLogin(): void {
  tokenStore.clear();
  if (!window.location.pathname.startsWith("/login")) {
    window.location.href = "/login";
  }
}

export async function http<T>(
  path: string,
  options: HttpOptions = {}
): Promise<T> {
  const { auth = true } = options;
  let attemptedRefresh = false;
  let response = await request(path, options, tokenStore.accessToken);

  if (response.status === 401 && auth && !attemptedRefresh) {
    attemptedRefresh = true;
    if (await requestRefresh()) {
      response = await request(path, options, tokenStore.accessToken);
    }
  }

  if (response.status === 401 && auth) {
    redirectToLogin();
    throw new ApiError("Unauthorized", 401);
  }

  const contentType = response.headers.get("content-type") ?? "";
  if (!contentType.includes("application/json")) {
    throw new ApiError(`Unexpected response from server (${response.status})`, response.status);
  }

  const json = (await response.json()) as ApiResponse<T>;

  if (!response.ok || json.success === false) {
    const message = json.message ?? json.errors?.[0] ?? `Request failed (${response.status})`;
    throw new ApiError(message, response.status, json.errors);
  }

  return json.data as T;
}

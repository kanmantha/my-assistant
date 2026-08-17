import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { http, tokenStore } from "./http";

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" }
  });
}

const okData = { data: { id: "n1", title: "Note" }, success: true };
const authTokens = {
  accessToken: "access-2",
  refreshToken: "refresh-2"
};

describe("http token refresh", () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("myassistant.access_token", "access-1");
    localStorage.setItem("myassistant.refresh_token", "refresh-1");
    fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    Object.defineProperty(window, "location", {
      configurable: true,
      value: { pathname: "/dashboard", href: "http://localhost:5173/dashboard" }
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("refreshes once and retries the original request on 401", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, { success: false, message: "Unauthorized" }))
      .mockResolvedValueOnce(jsonResponse(200, { success: true, data: authTokens }))
      .mockResolvedValueOnce(jsonResponse(200, okData));

    const result = await http<{ id: string; title: string }>("/notes");

    expect(result).toEqual(okData.data);
    expect(tokenStore.accessToken).toBe("access-2");
    expect(tokenStore.refreshToken).toBe("refresh-2");
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/notes");
    expect(fetchMock.mock.calls[1][0]).toBe("/api/auth/refresh");
    expect(fetchMock.mock.calls[2][0]).toBe("/api/notes");
    const retryHeaders = fetchMock.mock.calls[2][1].headers as Record<string, string>;
    expect(retryHeaders.Authorization).toBe("Bearer access-2");
  });

  it("does not attempt refresh when auth is disabled", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(401, { success: false, message: "Unauthorized" }));

    await expect(http("/auth/login", { method: "POST", auth: false, body: {} })).rejects.toThrow(
      "Unauthorized"
    );

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/auth/login");
  });

  it("shares a single refresh across concurrent 401s (single-flight)", async () => {
    let resolveRefresh: () => void;
    const refreshGate = new Promise<void>((r) => {
      resolveRefresh = r;
    });

    const callCounts = new Map<string, number>();
    fetchMock.mockImplementation((url: string) => {
      const n = (callCounts.get(url) ?? 0) + 1;
      callCounts.set(url, n);
      if (url.includes("/auth/refresh")) {
        return refreshGate.then(() => jsonResponse(200, { success: true, data: authTokens }));
      }
      return Promise.resolve(
        n === 1
          ? jsonResponse(401, { success: false, message: "Unauthorized" })
          : jsonResponse(200, { success: true, data: { ok: true } })
      );
    });

    const first = http<unknown>("/notes");
    const second = http<unknown>("/tasks");
    resolveRefresh!();
    await Promise.all([first, second]);

    const refreshCalls = fetchMock.mock.calls.filter((c) =>
      typeof c[0] === "string" && c[0].includes("/auth/refresh")
    );
    expect(refreshCalls).toHaveLength(1);
    expect(tokenStore.accessToken).toBe("access-2");
  });

  it("clears tokens and throws when refresh fails", async () => {
    localStorage.setItem("myassistant.access_token", "access-1");
    localStorage.setItem("myassistant.refresh_token", "refresh-1");
    fetchMock.mockResolvedValue(jsonResponse(401, { success: false, message: "Unauthorized" }));

    await expect(http<unknown>("/notes")).rejects.toThrow("Unauthorized");

    expect(tokenStore.accessToken).toBeNull();
    expect(tokenStore.refreshToken).toBeNull();
    const refreshCalls = fetchMock.mock.calls.filter((c) =>
      typeof c[0] === "string" && c[0].includes("/auth/refresh")
    );
    expect(refreshCalls).toHaveLength(1);
  });

  it("does not refresh twice when the retried request is still 401", async () => {
    localStorage.setItem("myassistant.access_token", "access-1");
    localStorage.setItem("myassistant.refresh_token", "refresh-1");
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, { success: false, message: "Unauthorized" }))
      .mockResolvedValueOnce(jsonResponse(200, { success: true, data: authTokens }))
      .mockResolvedValueOnce(jsonResponse(401, { success: false, message: "Unauthorized" }));

    await expect(http<unknown>("/notes")).rejects.toThrow("Unauthorized");

    const refreshCalls = fetchMock.mock.calls.filter((c) =>
      typeof c[0] === "string" && c[0].includes("/auth/refresh")
    );
    expect(refreshCalls).toHaveLength(1);
  });
});

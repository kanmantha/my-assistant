import { describe, expect, it } from "vitest";
import { buildWakeMatcher } from "./useWakeWord";

function matchText(wakeWord: string, text: string): { hit: boolean; transcript: string } {
  const match = text.toLowerCase().match(buildWakeMatcher(wakeWord.toLowerCase()));
  if (!match) return { hit: false, transcript: text };
  return { hit: true, transcript: text.slice(match[0].length).trim() };
}

describe("buildWakeMatcher", () => {
  const ww = "assistant";

  it("matches the bare wake word", () => {
    expect(matchText(ww, "assistant")).toMatchObject({ hit: true, transcript: "" });
  });

  it("matches common prefixes and keeps the command text", () => {
    expect(matchText(ww, "hey assistant create a note about milk")).toMatchObject({
      hit: true,
      transcript: "create a note about milk",
    });
    expect(matchText(ww, "okay assistant what is on my list")).toMatchObject({
      hit: true,
      transcript: "what is on my list",
    });
    expect(matchText(ww, "ok assistant good morning")).toMatchObject({
      hit: true,
      transcript: "good morning",
    });
    expect(matchText(ww, "hello assistant").hit).toBe(true);
    expect(matchText(ww, "hi assistant").hit).toBe(true);
  });

  it("matches interim partial phrases", () => {
    expect(matchText(ww, "hey assistant")).toMatchObject({ hit: true, transcript: "" });
    expect(matchText(ww, "hey assista").hit).toBe(false);
  });

  it("is case-insensitive", () => {
    expect(matchText(ww, "ASSISTANT remind me").transcript).toBe("remind me");
    expect(matchText(ww, "Hey Assistant").hit).toBe(true);
  });

  it("does not fire on plain speech that merely contains the word", () => {
    expect(matchText(ww, "my assistant friend").hit).toBe(false);
    expect(matchText(ww, "the assistant").hit).toBe(false);
  });

  it("does not match words sharing the wake word as a prefix", () => {
    expect(matchText(ww, "assistance").hit).toBe(false);
  });

  it("escapes regex metacharacters in custom wake words", () => {
    expect(matchText("a.b", "a.b what time").transcript).toBe("what time");
    expect(matchText("a.b", "axb what time").hit).toBe(false);
  });

  it("captures scheduling commands after the wake word", () => {
    expect(matchText(ww, "assistant schedule a call with John at 3pm").transcript).toBe(
      "schedule a call with John at 3pm"
    );
    expect(matchText(ww, "assistant add a task to buy milk tomorrow").transcript).toBe(
      "add a task to buy milk tomorrow"
    );
    expect(matchText(ww, "assistant remind me to call the client at 5").transcript).toBe(
      "remind me to call the client at 5"
    );
    expect(matchText(ww, "hey assistant set a meeting with Priya next Monday").transcript).toBe(
      "set a meeting with Priya next Monday"
    );
  });

  it("does not treat 'what is my schedule' style questions as scheduling commands", () => {
    expect(matchText(ww, "assistant what is my schedule today").transcript).toBe(
      "what is my schedule today"
    );
    expect(matchText(ww, "assistant what's on today").transcript).toBe("what's on today");
  });
});

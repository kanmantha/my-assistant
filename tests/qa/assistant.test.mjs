import { api, registerRandom, Suite, summarize } from "./harness.mjs";

export async function runAssistant() {
  const S = new Suite("ASSISTANT");
  // Use a fresh user so free-tier quotas never make the suite flaky.
  const acc = await registerRandom("asst");
  if (acc.registerError) { S.fail("register fresh assistant user", summarize(acc.registerError)); return S; }
  const tkn = acc.accessToken;

  const cmd = async (text, language = "Auto", sessionId = undefined) => {
    const r = await api("POST", "/api/assistant/command", { token: tkn, body: { text, language, sessionId, isVoice: false } });
    return { ...r, intent: r.json?.data?.intent, reply: r.json?.data?.reply, data: r.json?.data };
  };

  const cases = [
    ["Hello", "Greeting", "en greeting hello"],
    ["Good morning", "Greeting", "en greeting morning"],
    ["Thanks!", "Greeting", "en greeting thanks"],
    ["Help", "Help", "en help"],
    ["Show my tasks", "ListTasks", "en list tasks"],
    ["What are my pending tasks?", "ListTasks", "en list tasks 2"],
    ["List my reminders", "ListReminders", "en list reminders"],
    ["List my notes", "ListNotes", "en list notes"],
    ["List my appointments", "ListAppointments", "en list appointments"],
    ["Show my calendar", "ListAppointments", "en calendar"],
    ["What's on my calendar this week?", "ListAppointments", "en calendar week"],
    ["What is my schedule today?", "TodaySchedule", "en today"],
    ["What is my schedule tomorrow?", "TomorrowSchedule", "en tomorrow"],
    ["List down today's events", "TodaySchedule", "en today's events"],
    ["What's on my agenda today", "TodaySchedule", "en agenda today"],
    ["Take a note about the meeting", "CreateNote", "en create note"],
    ["Note down: buy milk on the way home", "CreateNote", "en create note 2"],
    ["Create a task called Buy milk", "CreateTask", "en create task"],
    ["Add a task to buy groceries by tomorrow at 5 PM", "CreateTask", "en create task sched"],
    ["Add Note", "CreateNote", "en bare add note"],
    ["Add Task", "CreateTask", "en bare add task"],
    ["Today Tasks Reminders", "ListReminders", "en today tasks reminders"],
    ["Todays Appointments", "ListAppointments", "en todays appointments"],
    ["Create a reminder at 5pm to wash plants", "CreateReminder", "en create reminder"],
    ["Remind me to drink water at 9am", "CreateReminder", "en create reminder 2"],
    ["Schedule a meeting with Ravi tomorrow at 10am", "CreateAppointment", "en create appointment"],
    ["Search for groceries", "SearchNotes", "en search"],
    ["Cancel", "CancelAction", "en cancel"],
    ["नमस्ते", "Greeting", "hi greeting"],
    ["मेरी टास्क दिखाओ", "ListTasks", "hi list tasks"],
    ["शेड्यूल दिखाओ कल का", "TomorrowSchedule", "hi tomorrow"],
    ["स्विच टू हिंदी", "ChangeLanguage", "hi change lang"],
    ["నమస్కారం", "Greeting", "te greeting"],
    ["ఒక నోట్ తీసుకో కిరాణా", "CreateNote", "te create note"],
    ["రేపు షెడ్యూల్ చూపించు", "TomorrowSchedule", "te tomorrow"],
    ["తెలుగుకి మార్చు", "ChangeLanguage", "te change lang"]
  ];

  for (const [text, expected, label] of cases) {
    const r = await cmd(text);
    S.check(r.intent === expected, `intent ${expected}`, `text="${text}" intent=${r.intent} status=${r.status}`, r.status, r.json);
  }

  // Schedule parsing: verify via tasks list that title is clean + due separate
  {
    await cmd("Add a task called uni-longwinded-test by tomorrow at 5 PM");
    await cmd("Yes");
    await cmd("Add a task called schedule-uni-check-review");
    await cmd("Yes");
    const r = await api("GET", "/api/tasks", { token: tkn });
    const tasks = r.json?.data ?? [];
    const clean = tasks.filter(x => String(x.title) === "uni-longwinded-test");
    S.check(clean.length >= 1, "task sched title clean", `found=${clean.length}`);
    const withDue = clean.find(x => x.dueDate || x.dueTime);
    S.check(!!withDue, "task sched has dueDate/dueTime sep", `dueDate=${withDue?.dueDate} dueTime=${withDue?.dueTime}`);
  }

  // --- Cleans up after schedule tests so data stays tidy ---
  {
    const r = await api("GET", "/api/tasks", { token: tkn });
    await Promise.all((r.json?.data ?? [])
      .filter(x => String(x.title).includes("uni-") || String(x.title).includes("schedule-uni"))
      .map(x => api("DELETE", `/api/tasks/${x.id}`, { token: tkn })));
  }

  // Task completion: fuzzy with a real created task
  {
    await cmd("Add a task called ReviewDeploymentQA");
    await cmd("Yes");
    const c1 = await cmd("Complete task ReviewDeploymentQA");
    S.check(c1.intent === "CompleteTask" && c1.data?.needsConfirmation === true, "complete task fuzzy", `intent=${c1.intent} needsConf=${c1.data?.needsConfirmation}`);
    await cmd("Yes");
  }

  // Multi-turn confirmation used with real meeting
  {
    const sid = "qa-" + Date.now();
    const t1 = await cmd("Schedule a meeting with Priya tomorrow at 9:30am", "Auto", sid);
    S.check(t1.intent === "CreateAppointment" && t1.data?.needsConfirmation === true, "mt turn1 needs confirmation", `intent=${t1.intent} needsConf=${t1.data?.needsConfirmation}`);
    const t2 = await cmd("Yes", "Auto", sid);
    S.check(t2.intent === "CreateAppointment", "mt turn2 yes -> execute", `intent=${t2.intent}`);
    // Complete a real task, then decline -> should cancel.
    const sid2 = "qa2-" + Date.now();
    await cmd("Add a task uniConfSampl");
    await cmd("Yes");
    const n1 = await cmd("Complete task uniConfSampl", "Auto", sid2);
    S.check(n1.intent === "CompleteTask" && n1.data?.needsConfirmation === true, "real task asks confirmation", `intent=${n1.intent} needsConf=${n1.data?.needsConfirmation}`);
    const n2 = await cmd("No", "Auto", sid2);
    S.check(n2.intent === "CancelAction" || n2.intent === "Unknown", "mt declines (cancel or no-op)", `intent=${n2.intent}`);
  }

  // New: creates confirm before saving, echoing the destination tab
  {
    const sid = "qa-confirm-" + Date.now();
    const t1 = await cmd("Add a task called uni-confirm-echo", "Auto", sid);
    S.check(t1.data?.needsConfirmation === true && (t1.reply || "").includes("Tasks"), "task asks confirmation echoing Tasks tab", `needsConf=${t1.data?.needsConfirmation} reply=${t1.reply?.slice(0, 60)}`);
    const t2 = await cmd("Yes", "Auto", sid);
    S.check(t2.intent === "CreateTask", "task confirmed -> executed", `intent=${t2.intent}`);
    const list = await api("GET", "/api/tasks", { token: tkn });
    const found = (list.json?.data ?? []).find(x => String(x.title) === "uni-confirm-echo");
    S.check(!!found, "confirmed task persisted", `found=${!!found}`);
    if (found) await api("DELETE", `/api/tasks/${found.id}`, { token: tkn });

    const sid2 = "qa-note-confirm-" + Date.now();
    const n1 = await cmd("Add Note", "Auto", sid2);
    S.check(n1.intent === "CreateNote", "bare add note -> section stage", `intent=${n1.intent}`);
    const n2 = await cmd("Notes", "Auto", sid2);
    S.check(n2.intent === "CreateNote", "section answer keeps note intent", `intent=${n2.intent}`);
    const n3 = await cmd("buy milk on the way home", "Auto", sid2);
    S.check(n3.data?.needsConfirmation === true && (n3.reply || "").includes("Notes"), "note asks confirmation echoing Notes tab", `needsConf=${n3.data?.needsConfirmation} reply=${n3.reply?.slice(0, 60)}`);
    const n4 = await cmd("Yes", "Auto", sid2);
    S.check(n4.intent === "CreateNote", "note confirmed -> executed", `intent=${n4.intent}`);
  }

  // Language persistence via ChangeLanguage
  {
    const sid = "qa-lang-" + Date.now();
    await cmd("switch to Hindi", "Auto", sid);
    const s = await api("GET", "/api/settings", { token: tkn });
    S.check(s.json?.data?.language === "hi", "settings language switched to hi", `lang=${s.json?.data?.language}`);
    await cmd("switch to English", "Auto", sid);
    const s2 = await api("GET", "/api/settings", { token: tkn });
    S.check(s2.json?.data?.language === "en", "settings language back to en", `lang=${s2.json?.data?.language}`);
  }

  // Empty / null input handling
  {
    const r = await api("POST", "/api/assistant/command", { token: tkn, body: { text: "", language: "Auto", isVoice: false } });
    S.check(r.status === 400, "assistant empty text -> 400", `status=${r.status}`, r.status, r.json);
    const r2 = await api("POST", "/api/assistant/command", { token: tkn, body: {} });
    S.check(r2.status === 400, "assistant missing text -> 400", `status=${r2.status}`, r2.status, r2.json);
  }

  return S;
}
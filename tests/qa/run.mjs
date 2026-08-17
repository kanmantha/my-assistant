import { runAuth } from "./auth.test.mjs";
import { runCrud } from "./crud.test.mjs";
import { runSecurity } from "./security.test.mjs";
import { runSubscription } from "./subscription.test.mjs";
import { runAssistant } from "./assistant.test.mjs";
import { runExtra } from "./extra.test.mjs";
import { runPerf } from "./perf.test.mjs";

const suites = {
  AUTH: runAuth,
  CRUD: runCrud,
  SECURITY: runSecurity,
  SUBSCRIPTION: runSubscription,
  ASSISTANT: runAssistant,
  EXTRA: runExtra,
  PERF: runPerf
};

(async () => {
  const all = {};
  let grP = 0, grF = 0;
  for (const [name, fn] of Object.entries(suites)) {
    console.log(`\n===== ${name} =====`);
    let S;
    try { S = await fn(); } catch (e) { S = { name, results: [{ ok: false, test: "suite crashed", detail: e.message, status: null }] }; console.log("  CRASH", e.message); }
    all[name] = S;
    for (const r of S.results) {
      const mark = r.ok ? "PASS" : "FAIL";
      console.log(`  ${mark} [${name}] ${r.test}${r.detail ? " — " + r.detail : ""}`);
      if (!r.ok) grF++;
      else grP++;
    }
    console.log(`  -> ${S.passed}/${S.results.length} passed`);
  }

  console.log(`\n==================`);
  console.log(`TOTAL: ${grP} passed, ${grF} failed`);
  console.log(`==================`);
  process.exit(grF === 0 ? 0 : 1);
})();
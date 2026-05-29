// Smoke test for the JAML language service. Run: npm run smoke
import {
  getCompletions,
  getDiagnostics,
  getHover,
  getDocumentSymbols,
  Severity,
} from "./src/index.js";

let failures = 0;
function check(name: string, cond: boolean, extra?: unknown) {
  if (cond) console.log(`  ok   ${name}`);
  else {
    failures++;
    console.error(`FAIL   ${name}`, extra ?? "");
  }
}

const valid = `name: Demo
deck: Erratic
stake: White
must:
  - joker: WeeJoker
    antes: [1, 2]
should:
  - voucher: Telescope
    score: 100
`;

check(
  "valid filter has no errors",
  getDiagnostics(valid).every((d) => d.severity !== Severity.Error),
  getDiagnostics(valid),
);

check(
  "bad enum value is an error",
  getDiagnostics(`name: X\nmust:\n  - joker: NotARealJoker\n`).some(
    (d) => d.severity === Severity.Error,
  ),
);

check(
  "unknown root key is flagged",
  getDiagnostics(`name: X\nwat: 5\nmust:\n  - joker: Blueprint\n`).some((d) =>
    /Unknown key/.test(d.message),
  ),
);

check(
  "broken YAML reports a syntax error",
  getDiagnostics(`must:\n  - joker: [unclosed\n`).some(
    (d) => d.severity === Severity.Error,
  ),
);

{
  const head = `must:\n  - joker: We`;
  const c = getCompletions(head, head.length);
  check(
    "value completion: WeeJoker",
    c.some((x) => x.label === "WeeJoker" && x.kind === "enum"),
    c.map((x) => x.label).slice(0, 8),
  );
}

{
  const head = `must:\n  - jo`;
  const c = getCompletions(head, head.length);
  check(
    "clause key completion: joker",
    c.some((x) => x.label === "joker" && x.kind === "field"),
    c.map((x) => x.label).slice(0, 8),
  );
}

{
  const head = `de`;
  const c = getCompletions(head, head.length);
  check("root key completion: deck", c.some((x) => x.label === "deck"), c.map((x) => x.label));
}

{
  const head = `must:\n  - jokers:\n      - Ha`;
  const c = getCompletions(head, head.length);
  check(
    "block-seq enum completion: Hack",
    c.some((x) => x.label === "Hack"),
    c.map((x) => x.label).slice(0, 8),
  );
}

{
  const text = `must:\n  - joker: WeeJoker\n`;
  const h = getHover(text, text.indexOf("joker:") + 2);
  check("hover on joker key", !!h && /joker/i.test(h.contents), h);
}

{
  const syms = getDocumentSymbols(valid);
  check("symbols include must", syms.some((s) => s.name === "must"), syms.map((s) => s.name));
  check("must lists its clauses", !!syms.find((s) => s.name === "must")?.children?.length);
}

console.log(failures === 0 ? "\nRESULT: PASS" : `\nRESULT: FAIL (${failures})`);
process.exit(failures === 0 ? 0 : 1);

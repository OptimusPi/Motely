#!/usr/bin/env node
// motely-wasm CLI — boot the embedded engine and DO something from the terminal.
// Doubles as the v20 end-to-end test nobody wrote. Exercises the full Program surface.
//
//   node cli.mjs search     <filter.jaml> --random <N>        bounded random search
//   node cli.mjs search     <filter.jaml> --seeds A,B,C       seed-list search
//   node cli.mjs jimmolate  <filter.jaml> --random <N> --min <s>   JS predicate keeps score>=s
//   node cli.mjs sequential <filter.jaml> [--start N] [--end M]    ORDERED sweep (bounded!)
//   node cli.mjs aesthetic  <filter.jaml> <aestheticIndex>    aesthetic-lens search of --seeds/--random
//   node cli.mjs native     [<name> --seeds A,B,C]            list or run built-in native filters
//   node cli.mjs explain    <filter.jaml>                     print the search plan in English
//   node cli.mjs convert    <file.jaml>                       JAML -> JSON -> JAML roundtrip
//
import bootsharp from "./dist/index.mjs";
import { Program } from "./dist/generated/modules/motely/wasm.g.mjs";
import { readFileSync } from "node:fs";

const t0 = Date.now();
const secs = () => ((Date.now() - t0) / 1000).toFixed(2);
const die = (m) => { console.error(`✗ ${m}`); process.exit(1); };
const readJaml = (p) => { try { return readFileSync(p, "utf8"); } catch { die(`cannot read JAML: ${p}`); } };

const [cmd, ...rest] = process.argv.slice(2);
const flag = (name, def) => { const i = rest.indexOf(name); return i >= 0 ? rest[i + 1] : def; };
const arg0 = () => (rest[0] && !rest[0].startsWith("--") ? rest[0] : null);

if (!cmd || cmd === "--help" || cmd === "-h") {
  console.log(readFileSync(new URL(import.meta.url)).toString().split("\n").slice(1, 16).join("\n").replace(/^\/\/ ?/gm, ""));
  process.exit(0);
}

// ── jimmolate predicate MUST be wired before boot (Bootsharp [Import], snapshotted at boot). ──
let scoredTotal = 0, keptTotal = 0;
const MIN = Number(flag("--min", "0"));
if (cmd === "jimmolate") {
  Program.jimmolatePredicate = (r) => {
    scoredTotal++;
    const keep = r.score >= MIN;
    if (keep) keptTotal++;
    return keep;
  };
}

console.log(`[+${secs()}s] booting embedded WASM (no args)…`);
await bootsharp.boot();
console.log(`[+${secs()}s] booted (status ${bootsharp.getStatus()}).`);

// ── commands that don't need a search ──
if (cmd === "native" && !arg0()) {
  console.log("Built-in native filters:");
  for (const n of Program.nativeFilterNames()) console.log(`  • ${n}`);
  process.exit(0);
}
if (cmd === "explain") {
  const cfg = Program.parseJaml(readJaml(arg0() ?? die("usage: explain <filter.jaml>")));
  console.log(`\n${Program.explainJaml(cfg) || "(no must/should/mustNot clauses to explain)"}`);
  process.exit(0);
}
if (cmd === "convert") {
  const src = readJaml(arg0() ?? die("usage: convert <file.jaml>"));
  const json = Program.jamlToJson(src);
  const back = Program.jsonToJaml(json);
  console.log(`JAML -> JSON (${json.length} chars):\n${json}\n\nJSON -> JAML (${back.length} chars):\n${back}`);
  process.exit(0);
}
// ── search-family commands ──
const wireStreams = () => {
  let shown = 0;
  Program.onScoredResult.subscribe((r) => {
    if (shown++ < 15) console.log(`   ★ ${r.seed}  score=${r.score}  tallies=[${r.tallies}]`);
  });
  Program.onProgress.subscribe((p) => {
    process.stdout.write(`\r[+${secs()}s] ${p.percentComplete.toFixed(0)}%  hits=${p.matchingSeeds}   `);
  });
};
const report = (search) => {
  console.log(`\n[+${secs()}s] done: searched=${search.totalSeedsSearched} matches=${search.matchingSeeds} completed=${search.isCompleted}`);
  process.exit(search.isCompleted ? 0 : 1);
};

if (cmd === "native") {
  const seeds = (flag("--seeds") ?? die("usage: native <name> --seeds A,B,C")).split(",");
  wireStreams();
  report(Program.runNativeListSearch(arg0(), seeds));
}

const cfgFile = arg0() ?? die(`usage: ${cmd} <filter.jaml> …`);
const config = Program.parseJaml(readJaml(cfgFile));
console.log(`[+${secs()}s] parsed: must=${config.must?.length ?? 0} should=${config.should?.length ?? 0} deck=${config.deck} stake=${config.stake}`);
wireStreams();

if (cmd === "jimmolate") {
  Program.jimmolateEnabled = true;
  console.log(`[+${secs()}s] jimmolate ON — predicate keeps score >= ${MIN}`);
}

let search;
if (cmd === "sequential") {
  // SAFETY RAIL: never default to an unbounded sweep (the forbidden ~2.3T run).
  const start = BigInt(flag("--start", "0"));
  const end = BigInt(flag("--end", String(start + 1n))); // default: exactly one batch
  if (end <= start) die("--end must be greater than --start");
  if (end - start > 64n) die(`refusing ${end - start} batches — that's a huge sweep. Cap it (<=64 batches) for the CLI.`);
  console.log(`[+${secs()}s] sequential sweep: batches [${start}, ${end}) …`);
  search = Program.runSequentialSearch(config, start, end);
} else if (cmd === "aesthetic") {
  const aesthetic = Number(rest[1] ?? die("usage: aesthetic <filter.jaml> <aestheticIndex> --seeds …/--random …"));
  const seeds = flag("--seeds"); const random = flag("--random");
  if (seeds) config.seeds = seeds.split(",");
  // aesthetic search reads its seed source from the config/settings the same way;
  // run it and let the engine drive.
  search = Program.runAestheticSearch(config, aesthetic);
} else if (cmd === "search" || cmd === "jimmolate") {
  const random = flag("--random"); const seeds = flag("--seeds");
  search = random
    ? Program.runRandomSearch(config, Number(random))
    : seeds
      ? (() => { config.seeds = seeds.split(","); return Program.runSeedListSearch(config); })()
      : die("specify --random <N> or --seeds A,B,C");
} else {
  die(`unknown command "${cmd}" — try --help`);
}

if (cmd === "jimmolate")
  console.log(`\n[+${secs()}s] jimmolate: predicate saw ${scoredTotal} scored seeds, KEPT ${keptTotal} (score >= ${MIN}).`);
report(search);

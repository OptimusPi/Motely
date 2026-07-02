import bootsharp, { Program } from "../bin/motely/index.mjs";
await bootsharp.boot();
const cfg = Program.loadJaml("name: P\ndeck: Red\nstake: White\nseeds:\n  - ALEEB\nshould:\n  - voucher: MagicTrick\n    antes:\n      - 1\n    score: 10\n");
const r = Program.analyzeSeed(cfg, "ALEEB");
console.log(JSON.stringify(r.streamStates));
const nxt = Program.analyzeNext(cfg, r.streamStates);
console.log(JSON.stringify(nxt.streamStates));
console.log("events lm len:", r.events.luckyMoney.length);

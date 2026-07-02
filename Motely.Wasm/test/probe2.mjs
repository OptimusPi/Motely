import bootsharp, { Program } from "../bin/motely/index.mjs";
await bootsharp.boot();
const cfg = Program.loadJaml("name: P\ndeck: Red\nstake: White\nseeds:\n  - ALEEB\nshould:\n  - voucher: MagicTrick\n    antes:\n      - 1\n    score: 10\n");
const r = Program.analyzeSeed(cfg, "ALEEB", 20);
console.log("explicit 20 -> rollOffset:", r.streamStates.rollOffset, "events:", r.events.luckyMoney.length);

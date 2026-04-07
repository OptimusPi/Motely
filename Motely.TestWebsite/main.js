import bootsharp, { MotelyJamlSearchBuilder, MotelySingleSearchContext, SearchEvents, Motely, Filters } from "motely-wasm";

const out = document.getElementById("out");
const status = document.getElementById("status");
const log = s => { out.textContent += s + "\n"; };

// Tabs
document.querySelectorAll(".tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach(t => t.classList.remove("active"));
    document.querySelectorAll(".panel").forEach(p => p.classList.remove("active"));
    tab.classList.add("active");
    document.getElementById("panel-" + tab.dataset.tab).classList.add("active");
  });
});
document.getElementById("btn-clear").addEventListener("click", () => { out.textContent = ""; });

// Populate deck/stake dropdowns
const deckSel = document.getElementById("deck");
const stakeSel = document.getElementById("stake");
const aesSel = document.getElementById("aes-val");
for (const [k, v] of Object.entries(Motely.MotelyDeck))
  if (typeof v === "number") deckSel.append(new Option(k, v));
for (const [k, v] of Object.entries(Motely.MotelyStake))
  if (typeof v === "number") stakeSel.append(new Option(k, v));
// Aesthetic enum
for (const [k, v] of Object.entries(Filters.JamlAesthetic))
  if (typeof v === "number") aesSel.append(new Option(k, v));

// Events
SearchEvents.onProgress.subscribe((searched, matching) =>
  log(`[progress] ${searched} searched  ${matching} matching`));
SearchEvents.onResult.subscribe((seed, score, tally) =>
  log(`[result] ${seed}  score=${score}  tally=[${[...tally]}]`));
SearchEvents.onComplete.subscribe((status, searched, matching) =>
  log(`[done] ${status}  searched=${searched}  matching=${matching}`));

// Track active search session for cancellation
let activeSession = null;

try {
  await bootsharp.boot();
  const ver = MotelyJamlSearchBuilder.getVersion();
  document.getElementById("ver").textContent = ver;
  status.textContent = `Ready — ${ver}`;
} catch (e) {
  status.textContent = `Boot failed: ${e?.message ?? e}`;
  console.error(e);
}

// ── Single-seed explorer ──────────────────────────────────────────────────────
let ctx = null;

document.getElementById("btn-ctx").addEventListener("click", () => {
  const seed = document.getElementById("seed").value.trim();
  ctx = MotelySingleSearchContext.open(seed, Number(deckSel.value), Number(stakeSel.value));
  out.textContent = "";
  log(`Loaded: ${seed}  deck=${deckSel.options[deckSel.selectedIndex].text}  stake=${stakeSel.options[stakeSel.selectedIndex].text}`);
  log(`Context: ${JSON.stringify(ctx, null, 2)}`);
});

const ante = () => Number(document.getElementById("ante").value);
const baseLuck = () => Number(document.getElementById("baseluck").value);

document.querySelectorAll("[data-action]").forEach(btn => {
  btn.addEventListener("click", () => {
    if (!ctx) { log("Load a seed first."); return; }
    const a = ante(), bl = baseLuck();
    try {
      const r = {
        voucher:    () => `voucher ante ${a}: ${ctx.getAnteFirstVoucher(a)}`,
        tag:        () => `tag ante ${a}: ${ctx.getNextTag(a)}`,
        boss:       () => `boss ante ${a}: ${ctx.getBossForAnte(a)}`,
        pack:       () => `pack ante ${a}: ${JSON.stringify(ctx.getNextBoosterPack(a))}`,
        shopitem:   () => `shop item ante ${a}: ${JSON.stringify(ctx.getNextShopItem(a))}`,
        shopjoker:  () => `shop joker ante ${a}: ${JSON.stringify(ctx.getNextShopJoker(a))}`,
        tarot:      () => `tarot ante ${a}: ${JSON.stringify(ctx.getNextTarot(a))}`,
        spectral:   () => `spectral ante ${a}: ${JSON.stringify(ctx.getNextSpectral(a))}`,
        planet:     () => `planet ante ${a}: ${JSON.stringify(ctx.getNextPlanet(a))}`,
        stdcard:    () => `std card ante ${a}: ${JSON.stringify(ctx.getNextStandardCard(a))}`,
        misprint:   () => `misprint mult: ${ctx.getNextMisprintMult()}`,
        luckymoney: () => `lucky money (luck=${bl}): ${ctx.getNextLuckyMoney(bl)}`,
        luckymult:  () => `lucky mult (luck=${bl}): ${ctx.getNextLuckyMult(bl)}`,
        erratic:    () => `erratic card: ${JSON.stringify(ctx.getNextErraticDeckCard())}`,
      }[btn.dataset.action];
      log(r ? r() : "?");
    } catch (e) {
      log(`Error: ${e?.message ?? e}`);
    }
  });
});

// ── Sequential ────────────────────────────────────────────────────────────────
document.getElementById("btn-seq").addEventListener("click", () => {
  const jaml = document.getElementById("jaml-seq").value.trim();
  if (!jaml) { log("Paste JAML first."); return; }
  try {
    MotelyJamlSearchBuilder.loadJaml(jaml);
    const batchcc = Number(document.getElementById("seq-batchcc").value);
    const start = BigInt(document.getElementById("seq-start").value);
    const end = BigInt(document.getElementById("seq-end").value);
    out.textContent = "";
    MotelyJamlSearchBuilder.sequential(batchcc, start, end);
    activeSession = MotelyJamlSearchBuilder.run();
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});
document.getElementById("btn-seq-stop").addEventListener("click", () => {
  if (activeSession) { activeSession.cancel(); activeSession = null; }
});

// ── Provider ──────────────────────────────────────────────────────────────────
const loadProvJaml = () => {
  const j = document.getElementById("jaml-prov").value.trim();
  if (!j) throw new Error("Paste JAML first.");
  MotelyJamlSearchBuilder.loadJaml(j);
};

document.getElementById("btn-random").addEventListener("click", () => {
  try {
    loadProvJaml();
    const count = Number(document.getElementById("prov-count").value);
    out.textContent = "";
    MotelyJamlSearchBuilder.random(count);
    activeSession = MotelyJamlSearchBuilder.run();
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});

document.getElementById("btn-keyword").addEventListener("click", () => {
  try {
    loadProvJaml();
    const kw = document.getElementById("prov-keywords").value.trim();
    const pad = document.getElementById("prov-padding").value.trim();
    if (!kw) { log("Enter keywords."); return; }
    out.textContent = "";
    MotelyJamlSearchBuilder.keywords(kw, pad);
    activeSession = MotelyJamlSearchBuilder.run();
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});

document.getElementById("btn-seedlist").addEventListener("click", () => {
  try {
    loadProvJaml();
    const seeds = document.getElementById("prov-seeds").value.trim();
    if (!seeds) { log("Enter seeds."); return; }
    out.textContent = "";
    MotelyJamlSearchBuilder.seedList(seeds.split(",").map(s => s.trim()));
    activeSession = MotelyJamlSearchBuilder.run();
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});

document.getElementById("btn-prov-stop").addEventListener("click", () => {
  if (activeSession) { activeSession.cancel(); activeSession = null; }
});

// ── Shop scroll stress test ──────────────────────────────────────────────────
let scrollRunning = false;

document.getElementById("btn-shopscroll").addEventListener("click", async () => {
  if (!ctx) { log("Load a seed first."); return; }
  if (scrollRunning) return;
  if (typeof ctx.getNextShopItem !== "function") {
    log("Shop scroll requires instance methods (not supported with current binding).");
    return;
  }
  scrollRunning = true;
  const batchSize = Number(document.getElementById("scroll-batch").value) || 100;
  const a = ante();
  let total = 0;
  const t0 = performance.now();
  let lastLog = t0;

  const tick = () => {
    if (!scrollRunning) return;
    const batchStart = performance.now();
    for (let i = 0; i < batchSize; i++) ctx.getNextShopItem(a);
    total += batchSize;
    const now = performance.now();
    if (now - lastLog >= 500) {
      log(`${Math.round(now - t0)}ms  →  ${total} items  (${Math.round(total / ((now - t0) / 1000))}/s)`);
      lastLog = now;
    }
    requestAnimationFrame(tick);
  };
  requestAnimationFrame(tick);
});

document.getElementById("btn-shopscroll-stop").addEventListener("click", () => {
  scrollRunning = false;
  log("stopped");
});

// ── Aesthetic ────────────────────────────────────────────────────────────────
document.getElementById("btn-aes").addEventListener("click", () => {
  try {
    const jaml = document.getElementById("jaml-aes").value.trim();
    if (!jaml) { log("Paste JAML first."); return; }
    MotelyJamlSearchBuilder.loadJaml(jaml);
    const aes = Number(aesSel.value);
    out.textContent = "";
    MotelyJamlSearchBuilder.aesthetic(aes);
    activeSession = MotelyJamlSearchBuilder.run();
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});
document.getElementById("btn-aes-stop").addEventListener("click", () => {
  if (activeSession) { activeSession.cancel(); activeSession = null; }
});

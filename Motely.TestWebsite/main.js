import bootsharp, { MotelyWasmHost, SearchEvents, Motely } from "motely-wasm";

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
// Aesthetic enum — matches JamlAesthetic C# enum order
const aesNames = ["Palindrome", "Psychosis", "Gross", "Nsfw", "Funny", "Balatro"];
aesNames.forEach((n, i) => aesSel.append(new Option(n, i)));

// Events
SearchEvents.onProgress.subscribe((searched, matching) =>
  log(`[progress] ${searched} searched  ${matching} matching`));
SearchEvents.onResult.subscribe((seed, score, tally) =>
  log(`[result] ${seed}  score=${score}`));
SearchEvents.onComplete.subscribe((status, searched, matching) =>
  log(`[done] ${status}  searched=${searched}  matching=${matching}`));

try {
  await bootsharp.boot();
  const ver = MotelyWasmHost.getVersion();
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
  ctx = MotelyWasmHost.motelySingleSearchContext(seed, Number(deckSel.value), Number(stakeSel.value));
  out.textContent = "";
  log(`Loaded: ${seed}  deck=${deckSel.options[deckSel.selectedIndex].text}  stake=${stakeSel.options[stakeSel.selectedIndex].text}`);
});

const ante = () => Number(document.getElementById("ante").value);
const baseLuck = () => Number(document.getElementById("baseluck").value);

document.querySelectorAll("[data-action]").forEach(btn => {
  btn.addEventListener("click", () => {
    if (!ctx) { log("Load a seed first."); return; }
    const a = ante(), bl = baseLuck();
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
  });
});

// ── Sequential ────────────────────────────────────────────────────────────────
document.getElementById("btn-seq").addEventListener("click", () => {
  const jaml = document.getElementById("jaml-seq").value.trim();
  if (!jaml) { log("Paste JAML first."); return; }
  try {
    const config = MotelyWasmHost.loadJaml(jaml);
    const batchcc = Number(document.getElementById("seq-batchcc").value);
    const start = BigInt(document.getElementById("seq-start").value);
    const end = BigInt(document.getElementById("seq-end").value);
    out.textContent = "";
    MotelyWasmHost.startSequentialSearch(config, batchcc, start, end);
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});
document.getElementById("btn-seq-stop").addEventListener("click", () => MotelyWasmHost.stopSearch());

// ── Provider ──────────────────────────────────────────────────────────────────
const provJaml = () => {
  const j = document.getElementById("jaml-prov").value.trim();
  if (!j) throw new Error("Paste JAML first.");
  return MotelyWasmHost.loadJaml(j);
};

document.getElementById("btn-random").addEventListener("click", () => {
  try {
    const count = Number(document.getElementById("prov-count").value);
    out.textContent = "";
    MotelyWasmHost.startRandomSearch(provJaml(), count);
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});

document.getElementById("btn-keyword").addEventListener("click", () => {
  try {
    const kw = document.getElementById("prov-keywords").value.trim();
    const pad = document.getElementById("prov-padding").value.trim();
    if (!kw) { log("Enter keywords."); return; }
    out.textContent = "";
    MotelyWasmHost.startKeywordSearch(provJaml(), kw, pad);
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});

document.getElementById("btn-seedlist").addEventListener("click", () => {
  try {
    const seeds = document.getElementById("prov-seeds").value.trim();
    const threads = Number(document.getElementById("prov-threads").value);
    if (!seeds) { log("Enter seeds."); return; }
    out.textContent = "";
    MotelyWasmHost.startSeedListSearch(provJaml(), seeds, threads);
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});

document.getElementById("btn-prov-stop").addEventListener("click", () => MotelyWasmHost.stopSearch());

// ── Shop scroll stress test ──────────────────────────────────────────────────
let scrollRunning = false;

document.getElementById("btn-shopscroll").addEventListener("click", async () => {
  if (!ctx) { log("Load a seed first."); return; }
  if (scrollRunning) return;
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
    const config = MotelyWasmHost.loadJaml(jaml);
    const aes = Number(aesSel.value);
    out.textContent = "";
    MotelyWasmHost.startAestheticSearch(config, aes);
  } catch (e) { log(`Error: ${e?.message ?? e}`); }
});
document.getElementById("btn-aes-stop").addEventListener("click", () => MotelyWasmHost.stopSearch());


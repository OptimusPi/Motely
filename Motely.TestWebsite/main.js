import bootsharp, { MotelyWasm, MotelyWasmEvents, Motely } from "motely-wasm";

const out = document.getElementById("out");
const status = document.getElementById("status");
const panelSequential = document.getElementById("panel-sequential");
const panelProvider = document.getElementById("panel-provider");
const panelAesthetic = document.getElementById("panel-aesthetic");
const seqTab = document.querySelector('[data-tab="sequential"]');
const providerTab = document.querySelector('[data-tab="provider"]');
const aesTab = document.querySelector('[data-tab="aesthetic"]');
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
for (const [k, v] of Object.entries(Motely.MotelyDeck))
  if (typeof v === "number") deckSel.append(new Option(k, v));
for (const [k, v] of Object.entries(Motely.MotelyStake))
  if (typeof v === "number") stakeSel.append(new Option(k, v));

if (panelSequential) panelSequential.innerHTML = '<p>This demo is currently focused on the single-seed browser search-context API.</p>';
if (panelProvider) panelProvider.innerHTML = '<p>Provider search wiring is temporarily disabled here while the site is aligned to the new MotelyWasm surface.</p>';
if (panelAesthetic) panelAesthetic.innerHTML = '<p>Aesthetic search wiring is temporarily disabled here while the site is aligned to the new MotelyWasm surface.</p>';
if (seqTab) seqTab.style.display = 'none';
if (providerTab) providerTab.style.display = 'none';
if (aesTab) aesTab.style.display = 'none';

// Events
MotelyWasmEvents.onProgress.subscribe((searched, matching) =>
  log(`[progress] ${searched} searched  ${matching} matching`));
MotelyWasmEvents.onResult.subscribe((seed, score, tally) =>
  log(`[result] ${seed}  score=${score}  tally=[${[...tally]}]`));
MotelyWasmEvents.onComplete.subscribe((status, searched, matching) =>
  log(`[done] ${status}  searched=${searched}  matching=${matching}`));

const defaultRunState = () => ({ voucherBitfield: 0, bossBitfield: 0 });
let ctx = null;
let shopStream = null;
let tagStream = null;
let bossStream = null;
let packStream = null;
let misprintStream = null;
let luckyMoneyStream = null;
let luckyMultStream = null;
let erraticStream = null;

try {
  await bootsharp.boot();
  const ver = MotelyWasm.getVersion();
  document.getElementById("ver").textContent = ver;
  status.textContent = `Ready — ${ver}`;
} catch (e) {
  status.textContent = `Boot failed: ${e?.message ?? e}`;
  console.error(e);
}

// ── Single-seed explorer ──────────────────────────────────────────────────────
document.getElementById("btn-ctx").addEventListener("click", () => {
  const seed = document.getElementById("seed").value.trim();
  ctx = MotelyWasm.createSearchContext(seed, Number(deckSel.value), Number(stakeSel.value));
  shopStream = null;
  tagStream = null;
  bossStream = null;
  packStream = null;
  misprintStream = null;
  luckyMoneyStream = null;
  luckyMultStream = null;
  erraticStream = null;
  out.textContent = "";
  log(`Loaded: ${seed}  deck=${deckSel.options[deckSel.selectedIndex].text}  stake=${stakeSel.options[stakeSel.selectedIndex].text}`);
  log(`Context ready: ${seed}`);
});

const ante = () => Number(document.getElementById("ante").value);
const baseLuck = () => Number(document.getElementById("baseluck").value);

document.querySelectorAll("[data-action]").forEach(btn => {
  btn.addEventListener("click", () => {
    if (!ctx) { log("Load a seed first."); return; }
    const a = ante(), bl = baseLuck();
    try {
      const r = {
        voucher: () => {
          const result = ctx.getAnteFirstVoucher(a, defaultRunState());
          return `voucher ante ${a}: ${Motely.MotelyVoucher[result.voucher]}`;
        },
        tag: () => {
          tagStream ??= ctx.createTagStream(a);
          const result = ctx.getNextTag(tagStream);
          tagStream = result.stream;
          return `tag ante ${a}: ${Motely.MotelyTag[result.tag]}`;
        },
        boss: () => {
          bossStream ??= ctx.createBossStream();
          const result = ctx.getNextBossForAnte(bossStream, a, defaultRunState());
          bossStream = result.stream;
          return `boss ante ${a}: ${Motely.MotelyBossBlind[result.boss]}`;
        },
        pack: () => {
          packStream ??= ctx.createBoosterPackStream(a);
          const result = ctx.getNextBoosterPack(packStream);
          packStream = result.stream;
          return `pack ante ${a}: ${Motely.MotelyBoosterPack[result.pack]}`;
        },
        shopitem: () => {
          shopStream ??= ctx.createShopItemStream(a, defaultRunState(), Motely.MotelyShopStreamFlags.Default, Motely.MotelyJokerStreamFlags.Default);
          const result = ctx.getNextShopItem(shopStream);
          shopStream = result.stream;
          return `shop item ante ${a}: ${result.item.value}`;
        },
        shopjoker: () => "shop joker: use shop scroll chunking to inspect packed item output",
        tarot: () => "tarot: use shop scroll chunking to inspect packed item output",
        spectral: () => "spectral: use shop scroll chunking to inspect packed item output",
        planet: () => "planet: use shop scroll chunking to inspect packed item output",
        stdcard: () => "std card: standard-card-specific browser helper not wired in this surface yet",
        misprint: () => {
          misprintStream ??= ctx.createMisprintPrngStream();
          const result = ctx.getNextMisprintMult(misprintStream);
          misprintStream = result.stream;
          return `misprint mult: ${result.value}`;
        },
        luckymoney: () => {
          luckyMoneyStream ??= ctx.createLuckyCardMoneyStream();
          const result = ctx.getNextLuckyMoney(luckyMoneyStream, bl);
          luckyMoneyStream = result.stream;
          return `lucky money (luck=${bl}): ${result.value}`;
        },
        luckymult: () => {
          luckyMultStream ??= ctx.createLuckyCardMultStream();
          const result = ctx.getNextLuckyMult(luckyMultStream, bl);
          luckyMultStream = result.stream;
          return `lucky mult (luck=${bl}): ${result.value}`;
        },
        erratic: () => {
          erraticStream ??= ctx.createErraticDeckPrngStream();
          const result = ctx.getNextErraticDeckCard(erraticStream);
          erraticStream = result.stream;
          return `erratic card: ${result.item.value}`;
        },
      }[btn.dataset.action];
      log(r ? r() : "?");
    } catch (e) {
      log(`Error: ${e?.message ?? e}`);
    }
  });
});

// ── Shop scroll stress test ──────────────────────────────────────────────────
let scrollRunning = false;

document.getElementById("btn-shopscroll").addEventListener("click", async () => {
  if (!ctx) { log("Load a seed first."); return; }
  if (scrollRunning) return;
  shopStream ??= ctx.createShopItemStream(ante(), defaultRunState(), Motely.MotelyShopStreamFlags.Default, Motely.MotelyJokerStreamFlags.Default);
  scrollRunning = true;
  const batchSize = Number(document.getElementById("scroll-batch").value) || 100;
  const a = ante();
  let total = 0;
  const t0 = performance.now();
  let lastLog = t0;

  const tick = () => {
    if (!scrollRunning) return;
    const result = ctx.getNextShopItemChunk(shopStream, batchSize);
    shopStream = result.stream;
    total += result.items.length;
    const now = performance.now();
    if (now - lastLog >= 500) {
      const latest = Array.from(result.items.slice(0, Math.min(5, result.items.length))).join(", ");
      log(`${Math.round(now - t0)}ms  →  ${total} items  (${Math.round(total / ((now - t0) / 1000))}/s) latest=[${latest}] ante=${a}`);
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

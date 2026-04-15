import bootsharp, { MotelyWasm, Motely } from "motely-wasm";

const out = document.getElementById("out");
const status = document.getElementById("status");
const deckSel = document.getElementById("deck");
const stakeSel = document.getElementById("stake");
const log = s => { out.textContent += s + "\n"; };

document.querySelectorAll(".tab").forEach(tab => {
  tab.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach(t => t.classList.remove("active"));
    document.querySelectorAll(".panel").forEach(p => p.classList.remove("active"));
    tab.classList.add("active");
    document.getElementById("panel-" + tab.dataset.tab).classList.add("active");
  });
});
document.getElementById("btn-clear").addEventListener("click", () => { out.textContent = ""; });

for (const [k, v] of Object.entries(Motely.MotelyDeck))
  if (typeof v === "number") deckSel.append(new Option(k, v));
for (const [k, v] of Object.entries(Motely.MotelyStake))
  if (typeof v === "number") stakeSel.append(new Option(k, v));

const defaultRunState = () => ({ voucherBitfield: 0, bossBitfield: 0 });
const enumName = (enumObject, value) => enumObject[value] ?? String(value);
const itemName = value => enumName(Motely.MotelyItemType, value);
const readBigInt = id => BigInt(document.getElementById(id).value || "0");

let ctx = null;
let tagStream = null;
let bossStream = null;
let packStream = null;
let misprintStream = null;
let luckyMoneyStream = null;
let luckyMultStream = null;
let erraticStream = null;
let shopStreams = {};
let scrollRunning = false;
let activeSearch = null;
let activeSearchDrainTimer = null;
let lastCompiledJummy = "";

const resetExplorerStreams = () => {
  tagStream = null;
  bossStream = null;
  packStream = null;
  misprintStream = null;
  luckyMoneyStream = null;
  luckyMultStream = null;
  erraticStream = null;
  shopStreams = {};
  scrollRunning = false;
};

const ensureJaml = id => {
  const jaml = document.getElementById(id).value.trim();
  if (!jaml)
    throw new Error("Enter JAML first.");
  const validation = MotelyWasm.validateJaml(jaml);
  if (validation !== "valid")
    throw new Error(validation);
  return jaml;
};

const logResults = results => {
  for (const result of results)
    log(`[result] ${result.seed}  score=${result.score}  tally=[${Array.from(result.tallyColumns).join(", ")}]`);
};

const setReadyStatus = () => {
  const ver = document.getElementById("ver").textContent;
  status.textContent = `Ready${ver ? ` — ${ver}` : ""}`;
};

const cleanupActiveSearch = () => {
  if (activeSearchDrainTimer !== null) {
    clearInterval(activeSearchDrainTimer);
    activeSearchDrainTimer = null;
  }
  if (activeSearch) {
    activeSearch.dispose();
    activeSearch = null;
  }
  setReadyStatus();
};

const cancelActiveSearch = () => {
  if (!activeSearch)
    return;
  activeSearch.cancel();
  log("[search] cancel requested");
};

const startSearch = async (label, handle) => {
  cleanupActiveSearch();
  activeSearch = handle;
  status.textContent = `${label} running…`;
  log(`[start] ${label}`);

  activeSearchDrainTimer = setInterval(() => {
    if (!activeSearch)
      return;
    const snapshot = activeSearch.getSnapshot();
    const results = activeSearch.drainResults(128);
    logResults(results);
    status.textContent = `${label}: searched=${snapshot.totalSeedsSearched} matching=${snapshot.matchingSeeds} elapsed=${snapshot.elapsedMs}ms`;
  }, 250);

  try {
    const completion = await handle.waitForCompletion();
    logResults(handle.drainResults(4096));
    log(`[done] ${label} state=${enumName(Motely.MotelyWasmSearchState, completion.state)} searched=${completion.totalSeedsSearched} matching=${completion.matchingSeeds}${completion.error ? ` error=${completion.error}` : ""}`);
  } catch (e) {
    log(`Error: ${e?.message ?? e}`);
  } finally {
    if (activeSearch === handle)
      cleanupActiveSearch();
  }
};

const getShopStream = (key, flags, jokerFlags = Motely.MotelyJokerStreamFlags.Default) => {
  if (!ctx)
    throw new Error("Load a seed first.");
  shopStreams[key] ??= ctx.createShopItemStream(ante(), defaultRunState(), flags, jokerFlags);
  return shopStreams[key];
};

const setShopStream = (key, stream) => {
  shopStreams[key] = stream;
};

try {
  await bootsharp.boot();
  const ver = MotelyWasm.getVersion();
  document.getElementById("ver").textContent = ver;
  setReadyStatus();
} catch (e) {
  status.textContent = `Boot failed: ${e?.message ?? e}`;
  console.error(e);
}

// ── Single-seed explorer ──────────────────────────────────────────────────────
document.getElementById("btn-ctx").addEventListener("click", () => {
  const seed = document.getElementById("seed").value.trim();
  ctx = MotelyWasm.createSearchContext(seed, Number(deckSel.value), Number(stakeSel.value));
  resetExplorerStreams();
  out.textContent = "";
  log(`Loaded: ${seed}  deck=${deckSel.options[deckSel.selectedIndex].text}  stake=${stakeSel.options[stakeSel.selectedIndex].text}`);
  log(`Context ready: ${seed}`);
});

const ante = () => Number(document.getElementById("ante").value);
const baseLuck = () => Number(document.getElementById("baseluck").value);

document.querySelectorAll("[data-action]").forEach(btn => {
  btn.addEventListener("click", () => {
    if (!ctx) { log("Load a seed first."); return; }
    const a = ante();
    const bl = baseLuck();
    try {
      const r = {
        voucher: () => {
          const result = ctx.getAnteFirstVoucher(a, defaultRunState());
          return `voucher ante ${a}: ${enumName(Motely.MotelyVoucher, result.voucher)}`;
        },
        tag: () => {
          tagStream ??= ctx.createTagStream(a);
          const result = ctx.getNextTag(tagStream);
          tagStream = result.stream;
          return `tag ante ${a}: ${enumName(Motely.MotelyTag, result.tag)}`;
        },
        boss: () => {
          bossStream ??= ctx.createBossStream();
          const result = ctx.getNextBossForAnte(bossStream, a, defaultRunState());
          bossStream = result.stream;
          return `boss ante ${a}: ${enumName(Motely.MotelyBossBlind, result.boss)}`;
        },
        pack: () => {
          packStream ??= ctx.createBoosterPackStream(a);
          const result = ctx.getNextBoosterPack(packStream);
          packStream = result.stream;
          return `pack ante ${a}: ${enumName(Motely.MotelyBoosterPack, result.pack)}`;
        },
        shopitem: () => {
          const result = ctx.getNextShopItem(getShopStream("default", Motely.MotelyShopStreamFlags.Default));
          setShopStream("default", result.stream);
          return `shop item ante ${a}: ${itemName(result.item.value)} (${result.item.value})`;
        },
        shopjoker: () => {
          const flags = Motely.MotelyShopStreamFlags.ExcludeTarots | Motely.MotelyShopStreamFlags.ExcludePlanets | Motely.MotelyShopStreamFlags.ExcludeSpectrals;
          const result = ctx.getNextShopItem(getShopStream("joker", flags));
          setShopStream("joker", result.stream);
          return `shop joker ante ${a}: ${itemName(result.item.value)} (${result.item.value})`;
        },
        tarot: () => {
          const flags = Motely.MotelyShopStreamFlags.ExcludeJokers | Motely.MotelyShopStreamFlags.ExcludePlanets | Motely.MotelyShopStreamFlags.ExcludeSpectrals;
          const result = ctx.getNextShopItem(getShopStream("tarot", flags));
          setShopStream("tarot", result.stream);
          return `shop tarot ante ${a}: ${itemName(result.item.value)} (${result.item.value})`;
        },
        spectral: () => {
          const flags = Motely.MotelyShopStreamFlags.ExcludeJokers | Motely.MotelyShopStreamFlags.ExcludeTarots | Motely.MotelyShopStreamFlags.ExcludePlanets;
          const result = ctx.getNextShopItem(getShopStream("spectral", flags));
          setShopStream("spectral", result.stream);
          return `shop spectral ante ${a}: ${itemName(result.item.value)} (${result.item.value})`;
        },
        planet: () => {
          const flags = Motely.MotelyShopStreamFlags.ExcludeJokers | Motely.MotelyShopStreamFlags.ExcludeTarots | Motely.MotelyShopStreamFlags.ExcludeSpectrals;
          const result = ctx.getNextShopItem(getShopStream("planet", flags));
          setShopStream("planet", result.stream);
          return `shop planet ante ${a}: ${itemName(result.item.value)} (${result.item.value})`;
        },
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
          return `erratic card: ${itemName(result.item.value)} (${result.item.value})`;
        }
      }[btn.dataset.action];
      log(r ? r() : "?");
    } catch (e) {
      log(`Error: ${e?.message ?? e}`);
    }
  });
});

// ── Shop scroll stress test ──────────────────────────────────────────────────
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
    if (!scrollRunning)
      return;
    const result = ctx.getNextShopItemChunk(getShopStream("default", Motely.MotelyShopStreamFlags.Default), batchSize);
    setShopStream("default", result.stream);
    total += result.items.length;
    const now = performance.now();
    if (now - lastLog >= 500) {
      const latest = Array.from(result.items.slice(0, Math.min(5, result.items.length))).map(itemName).join(", ");
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

// ── Sequential search ────────────────────────────────────────────────────────
document.getElementById("btn-seq").addEventListener("click", () => {
  try {
    const jaml = ensureJaml("jaml-seq");
    const handle = MotelyWasm.startSequentialSearch(
      jaml,
      Number(document.getElementById("seq-batchcc").value),
      readBigInt("seq-start"),
      readBigInt("seq-end")
    );
    void startSearch("sequential", handle);
  } catch (e) {
    log(`Error: ${e?.message ?? e}`);
  }
});

document.getElementById("btn-seq-stop").addEventListener("click", cancelActiveSearch);

// ── Provider searches ────────────────────────────────────────────────────────
document.getElementById("btn-random").addEventListener("click", () => {
  try {
    const jaml = ensureJaml("jaml-prov");
    const handle = MotelyWasm.startRandomSearch(jaml, Number(document.getElementById("prov-count").value));
    void startSearch("random", handle);
  } catch (e) {
    log(`Error: ${e?.message ?? e}`);
  }
});

document.getElementById("btn-keyword").addEventListener("click", () => {
  try {
    const jaml = ensureJaml("jaml-prov");
    const keywordsCsv = document.getElementById("prov-keywords").value.trim();
    const paddingChars = document.getElementById("prov-padding").value.trim();
    const handle = MotelyWasm.startKeywordSearch(jaml, keywordsCsv, paddingChars);
    void startSearch("keyword", handle);
  } catch (e) {
    log(`Error: ${e?.message ?? e}`);
  }
});

document.getElementById("btn-seedlist").addEventListener("click", () => {
  try {
    const jaml = ensureJaml("jaml-prov");
    const seeds = document.getElementById("prov-seeds").value.split(",").map(seed => seed.trim()).filter(Boolean);
    const handle = MotelyWasm.startSeedListSearch(jaml, seeds);
    void startSearch("seed-list", handle);
  } catch (e) {
    log(`Error: ${e?.message ?? e}`);
  }
});

document.getElementById("btn-prov-stop").addEventListener("click", cancelActiveSearch);

// ── Aesthetic search ─────────────────────────────────────────────────────────
document.getElementById("btn-aes").addEventListener("click", () => {
  try {
    const jummy = document.getElementById("jaml-aes").value.trim();
    if (!jummy)
      throw new Error("Enter Jummy first.");
    lastCompiledJummy = MotelyWasm.compileJummy(jummy);
    log("[jummy] compiled successfully");
    log(lastCompiledJummy);
  } catch (e) {
    log(`Error: ${e?.message ?? e}`);
  }
});

document.getElementById("btn-aes-stop").addEventListener("click", () => {
  const compiled = lastCompiledJummy || document.getElementById("jaml-aes").value.trim();
  if (!compiled) {
    log("Nothing to copy.");
    return;
  }
  document.getElementById("jaml-seq").value = compiled;
  document.getElementById("jaml-prov").value = compiled;
  log("[jummy] copied into sequential and provider inputs");
});

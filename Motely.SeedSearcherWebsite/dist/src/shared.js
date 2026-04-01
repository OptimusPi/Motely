export async function bootAndWire(dotnet, Motely, modeLabel, options = {}) {
  const Program = Motely.BrowserWasm.MotelyProgram;
  const Callbacks = Motely.BrowserWasm.MotelyProgramCallbacks;
  const Enums = Motely;

  // Bootsharp JSImport: provide stubs before boot so C# never sees undefined delegates.
  Callbacks.onProgress = () => { };
  Callbacks.onResult = () => { };
  Callbacks.onComplete = () => { };

  const bootRoot = options.bootRoot;
  await dotnet.boot(
    bootRoot != null && String(bootRoot).length > 0 ? { root: String(bootRoot) } : undefined
  );
  const ver = Program.getVersion();
  const isolated = typeof crossOriginIsolated !== "undefined" ? crossOriginIsolated : false;
  const sab = typeof SharedArrayBuffer !== "undefined";

  const title = document.getElementById("title");
  const bootStatus = document.getElementById("boot-status");
  const status = document.getElementById("status");
  const err = document.getElementById("err");
  const analysisOut = document.getElementById("analysis-out");
  const searchOut = document.getElementById("search-out");

  if (title) title.textContent = `Motely WASM ${ver}`;
  if (bootStatus) {
    bootStatus.textContent =
      `Ready — ${ver} | mode=${modeLabel} | crossOriginIsolated=${String(isolated)} | SAB=${String(sab)}`;
  }
  if (status) status.textContent = "Ready.";

  const searchMode = document.getElementById("search-mode");
  const wrapRandom = document.getElementById("wrap-random");
  const wrapAesthetic = document.getElementById("wrap-aesthetic");
  const wrapKeyword = document.getElementById("wrap-keyword");
  const wrapSeedlist = document.getElementById("wrap-seedlist");

  function syncModeUi() {
    const m = searchMode?.value ?? "configured";
    const show = (el, on) => {
      if (el) el.style.display = on ? "" : "none";
    };
    show(wrapRandom, m === "random");
    show(wrapAesthetic, m === "aesthetic");
    show(wrapKeyword, m === "keyword");
    show(wrapSeedlist, m === "seedlist");
  }
  searchMode?.addEventListener("change", syncModeUi);
  syncModeUi();

  // ── Tabs ────────────────────────────────────────────────
  for (const tab of document.querySelectorAll(".tab")) {
    tab.addEventListener("click", () => {
      const target = tab.getAttribute("data-tab");
      document.querySelectorAll(".tab").forEach((el) => el.classList.remove("active"));
      tab.classList.add("active");
      document.querySelectorAll(".panel").forEach((p) => p.classList.remove("active"));
      const panel = document.getElementById(`panel-${target}`);
      if (panel) panel.classList.add("active");
    });
  }

  // ── Validate ────────────────────────────────────────────
  const validateBtn = document.getElementById("btn-validate");
  validateBtn?.addEventListener("click", () => {
    const jaml = document.getElementById("jaml")?.value?.trim() ?? "";
    const e = Program.validateJaml(jaml);
    if (err) err.textContent = e ? `Error: ${e}` : "Valid JAML!";
  });

  // ── Search ────────────────────────────────────────────────
  let searchResults = [];
  let activeHandle = null;
  let searchToken = 0;

  function releaseHandle() {
    if (!activeHandle) return;
    try {
      activeHandle.cancel();
      activeHandle.dispose();
    } catch (_) {
      /* ignore */
    }
    activeHandle = null;
  }

  function wireCallbacksForRun(token) {
    Callbacks.onProgress = (seedsSearched, matchingSeeds, elapsedMs) => {
      if (token !== searchToken) return;
      const seeds = Number(seedsSearched);
      const matches = Number(matchingSeeds);
      const elapsed = Number(elapsedMs);
      const perSec = elapsed > 0 ? Math.round((seeds * 1000) / elapsed) : 0;
      if (status) status.textContent = `Searching… ${seeds} seeds, ${matches} matches (${perSec}/s)`;
    };

    Callbacks.onResult = (seed, score) => {
      if (token !== searchToken) return;
      searchResults.push({ seed, score });
      const preview = searchResults
        .slice(-10)
        .map((h, i) => `${i + 1}. ${h.seed} (score=${h.score})`)
        .join("\n");
      if (searchOut) searchOut.textContent = `Matches so far: ${searchResults.length}\n\nRecent hits:\n${preview}`;
    };

    Callbacks.onComplete = (stateName, seedsSearched, matchingSeeds) => {
      if (token !== searchToken) return;
      releaseHandle();
      if (status) status.textContent = `Search ${stateName}. ${Number(seedsSearched)} seeds searched, ${Number(matchingSeeds)} matches.`;
      const sorted = [...searchResults].sort((a, b) => b.score - a.score);
      const preview = sorted
        .slice(0, 25)
        .map((h, i) => `${i + 1}. ${h.seed} (score=${h.score})`)
        .join("\n");
      const best = sorted[0];
      if (searchOut) {
        searchOut.textContent =
          `Status: ${stateName}\nSeeds searched: ${Number(seedsSearched)}\nMatches: ${Number(matchingSeeds)}\nBest: ${best ? `${best.seed} (score=${best.score})` : "(none)"}\n\nTop results:\n${preview || "(none)"}`;
      }
      if (String(stateName).startsWith("error:") && err) err.textContent = `Error: ${stateName}`;
    };
  }

  const startBtn = document.getElementById("btn-find-best");
  startBtn?.addEventListener("click", () => {
    const jaml = document.getElementById("jaml")?.value?.trim() ?? "";
    if (!jaml) {
      if (err) err.textContent = "Paste JAML first.";
      return;
    }

    const valError = Program.validateJaml(jaml);
    if (valError) {
      if (err) err.textContent = `JAML error: ${valError}`;
      return;
    }

    if (err) err.textContent = "";
    searchResults = [];
    if (searchOut) searchOut.textContent = "Starting search…";
    if (status) status.textContent = "Searching…";

    releaseHandle();
    searchToken += 1;
    const token = searchToken;
    wireCallbacksForRun(token);

    const threadCount = Number(document.getElementById("thread-count")?.value ?? "1");
    const threads = Number.isFinite(threadCount) && threadCount > 0 ? Math.trunc(threadCount) : 1;

    const batchCharCount = Number(document.getElementById("batch-chars")?.value ?? "4");
    const batch = Number.isFinite(batchCharCount) && batchCharCount >= 1 && batchCharCount <= 7
      ? Math.trunc(batchCharCount)
      : 4;

    const mode = searchMode?.value ?? "configured";

    try {
      if (mode === "configured") {
        activeHandle = Program.startConfiguredSearch(jaml, threads, batch, 0n, 0n);
      } else if (mode === "sequential") {
        activeHandle = Program.startSequentialSearch(jaml, threads, batch, 0n, 0n);
      } else if (mode === "random") {
        const n = Number(document.getElementById("random-count")?.value ?? "500");
        const rc = Number.isFinite(n) && n > 0 ? Math.trunc(n) : 500;
        activeHandle = Program.startRandomSearch(jaml, rc, threads, batch);
      } else if (mode === "aesthetic") {
        const ae = Number(document.getElementById("aesthetic-pick")?.value ?? "0");
        activeHandle = Program.startAestheticSearch(jaml, ae, threads, batch);
      } else if (mode === "keyword") {
        const kw = document.getElementById("keywords-input")?.value?.trim() ?? "";
        if (!kw) {
          if (err) err.textContent = "Enter at least one keyword for keyword mode.";
          return;
        }
        const pad = document.getElementById("padding-input")?.value?.trim() ?? "";
        activeHandle = Program.startKeywordSearch(jaml, kw, pad, threads, batch);
      } else if (mode === "seedlist") {
        const seeds = document.getElementById("seeds-csv")?.value?.trim() ?? "";
        if (!seeds) {
          if (err) err.textContent = "Enter comma-separated seeds for seed list mode.";
          return;
        }
        activeHandle = Program.startSeedListSearch(jaml, seeds, threads);
      }
    } catch (ex) {
      if (searchOut) searchOut.textContent = `Search exception: ${String(ex)}`;
      if (status) status.textContent = "Search crashed.";
    }
  });

  const stopBtn = document.getElementById("btn-stop");
  stopBtn?.addEventListener("click", () => {
    try {
      releaseHandle();
      if (status) status.textContent = "Stopping…";
    } catch (ex) {
      if (status) status.textContent = `Stop error: ${String(ex)}`;
    }
  });

  // ── Analyze ─────────────────────────────────────────────
  const analyzeBtn = document.getElementById("btn-analyze");
  analyzeBtn?.addEventListener("click", () => {
    const seed = (document.getElementById("a-seed")?.value ?? "")
      .trim()
      .toUpperCase()
      .replace(/0/g, "O");
    const deckName = document.getElementById("a-deck")?.value ?? "Red";
    const stakeName = document.getElementById("a-stake")?.value ?? "White";
    const deck = Enums.MotelyDeck[deckName];
    const stake = Enums.MotelyStake[stakeName];

    try {
      const result = Program.analyzeSeed(seed, deck, stake);
      if (analysisOut) analysisOut.textContent = JSON.stringify(result, null, 2);
    } catch (ex) {
      if (analysisOut) analysisOut.textContent = `Error: ${String(ex)}`;
    }
  });
}

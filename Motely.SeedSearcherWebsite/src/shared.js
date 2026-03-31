export async function bootAndWire(dotnet, Motely, modeLabel) {
  const Program = Motely.BrowserWasm.MotelyProgram;
  const Callbacks = Motely.BrowserWasm.MotelyProgramCallbacks;
  const Enums = Motely;

  await dotnet.boot();
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

  // ── Search (uses real async startSearch + callbacks) ────
  let searchResults = [];

  Callbacks.onProgress = (seedsSearched, matchingSeeds, elapsedMs) => {
    if (status) status.textContent = `Searching… ${seedsSearched} seeds, ${matchingSeeds} matches (${(Number(elapsedMs) / 1000).toFixed(1)}s)`;
  };

  Callbacks.onResult = (seed, score) => {
    searchResults.push({ seed, score });
    const preview = searchResults
      .slice(-10)
      .map((h, i) => `${i + 1}. ${h.seed} (score=${h.score})`)
      .join("\n");
    if (searchOut) searchOut.textContent = `Matches so far: ${searchResults.length}\n\nRecent hits:\n${preview}`;
  };

  Callbacks.onComplete = (completionStatus, seedsSearched, matchingSeeds) => {
    if (status) status.textContent = `Search ${completionStatus}. ${seedsSearched} seeds searched, ${matchingSeeds} matches.`;
    const sorted = [...searchResults].sort((a, b) => b.score - a.score);
    const preview = sorted
      .slice(0, 25)
      .map((h, i) => `${i + 1}. ${h.seed} (score=${h.score})`)
      .join("\n");
    if (searchOut) searchOut.textContent = `Status: ${completionStatus}\nSeeds searched: ${seedsSearched}\nMatches: ${matchingSeeds}\n\nTop results:\n${preview || "(none)"}`;
  };

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

    const threadCount = Number(document.getElementById("thread-count")?.value ?? "1");
    const threads = Number.isFinite(threadCount) && threadCount > 0 ? Math.trunc(threadCount) : 1;

    try {
      Program.startSearch(jaml, threads, 8, BigInt(0), BigInt(0));
    } catch (ex) {
      if (searchOut) searchOut.textContent = `Search exception: ${String(ex)}`;
      if (status) status.textContent = "Search crashed.";
    }
  });

  const stopBtn = document.getElementById("btn-stop");
  stopBtn?.addEventListener("click", () => {
    try {
      Program.stopSearch();
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

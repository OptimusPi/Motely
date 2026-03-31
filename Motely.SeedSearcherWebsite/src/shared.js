export async function bootAndWire(dotnet, Motely, modeLabel) {
  const Program = Motely.BrowserWasm.MotelyProgram;
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

  const validateBtn = document.getElementById("btn-validate");
  validateBtn?.addEventListener("click", () => {
    const jaml = document.getElementById("jaml")?.value?.trim() ?? "";
    const e = Program.validateJaml(jaml);
    if (err) err.textContent = e ? `Error: ${e}` : "Valid JAML!";
  });

  const findBestBtn = document.getElementById("btn-find-best");
  findBestBtn?.addEventListener("click", () => {
    const jaml = document.getElementById("jaml")?.value?.trim() ?? "";
    if (!jaml) {
      if (err) err.textContent = "Paste JAML first.";
      return;
    }
    if (status) status.textContent = "Searching...";
    if (err) err.textContent = "";

    try {
      const jamlConfig = Program.parseJaml(jaml);
      const threadCount = Number(document.getElementById("thread-count")?.value ?? "1");
      const palindromeOnly = Boolean(document.getElementById("pal-only")?.checked ?? true);
      const request = {
        jamlConfig,
        palindromeOnly,
        threadCount: Number.isFinite(threadCount) && threadCount > 0 ? Math.trunc(threadCount) : 1,
        topResults: 25,
      };
      const result = Program.findBestSeed(request);
      if (result?.error) {
        if (searchOut) searchOut.textContent = `Search error: ${result.error}`;
        if (status) status.textContent = "Search failed.";
        return;
      }

      const best = result?.bestSeed ?? "(none)";
      const bestScore = result?.bestScore ?? 0;
      const hits = Array.isArray(result?.hits) ? result.hits : [];
      const preview = hits
        .slice(0, 10)
        .map((h, i) => `${i + 1}. ${h.seed} (score=${h.score})`)
        .join("\n");
      if (searchOut) {
        searchOut.textContent =
          `Best seed: ${best}\nBest score: ${bestScore}\nMatches: ${result?.matches ?? 0}\nElapsed: ${(result?.elapsedSeconds ?? 0).toFixed(2)}s\n\nTop hits:\n${preview || "(none)"}`;
      }
      if (status) status.textContent = "Search complete.";
    } catch (ex) {
      if (searchOut) searchOut.textContent = `Search exception: ${String(ex)}`;
      if (status) status.textContent = "Search crashed.";
    }
  });

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

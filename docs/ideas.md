# Ideas & feature ledger — MotelyJAML / motely-wasm

> **JAML — Jimbo's Ante Markup Language — invented by pifreak + Claude,**
> **over 15 months of never-give-up friction.** Iron sharpens iron. >:D
>
> pifreak types ideas out loud; this file makes sure they don't evaporate.
> New idea? Add a row. Shipped it? Move it to "Done". Nothing gets lost.

---

## ▶ START HERE (next session — pick ONE focus)

Session got scattered across many threads; all work below is durable on disk.
Best single next focus, in priority order:

1. **Fix cli.mjs Node boot** (small, high-value). `motely-wasm/cli.mjs` fails in
   Node with `Te.addRunDependency is not a function` — because it imports
   `./dist/index.mjs` (raw relative) instead of the package. jaml-ui's
   `.verify-score.mjs` boots fine in Node via `import bootsharp from "motely-wasm"`.
   Fix: make cli.mjs resolve motely-wasm as the package (or replicate the harness).
   Then it's a real Node test harness → "nobody tests" finally dies.
2. **Finish/verify the JAMLyzer.** It's actually in good shape already —
   `Motely.Tests/JamlyzerUnitTests.cs` tests golden-analyzer agreement, lens glow,
   pack format. Decide what "finished" means vs. what's missing; add tests, not
   fake prod. (Also: `.verify-score.mjs` is MISPLACED in jaml-ui — move to motely-wasm.)
3. **Fix the legendary-stream count bug** (see Known bugs below) — only when ready.
4. **Publish prep** (git's back; bundled git or `winget install Git.Git` to restore
   the Bash tool). Nothing committed yet — waiting on explicit go.

Stable + tested > shipping fake prod. No rush. seedfinder.app prod is the eventual
target (July/Sep MCP-App), but building is the joy — don't rush it.

---

## 🔨 Building now

### node CLI for motely-wasm
A tiny terminal driver over the embedded WASM. "Why not" — it's a great demo AND
the v20 smoke test we never had.

```sh
node cli.mjs analyze LUCKYCAT1 examples/blueprint.jaml   # single-seed snapshot
node cli.mjs search examples/blueprint.jaml --random 100000   # bounded search
node cli.mjs search examples/blueprint.jaml --seeds ALEEB,7LB2WU
```

- Boots embedded (no args), parses JAML, runs the real engine.
- Proves boot + parse + search + events all work end to end in Node.

## 💡 Captured — not started

### "The Long Blind" — endless-shop mode
A driving-down-the-antes-forever game. Walk a single seed's shop items perpetually:
keep descending antes / rerolling, surfacing what the shop offers ad infinitum.
Idle/zen vibe — "the ante never ends." Build on top of the per-seed analysis
(`Program.jamlyzer` / the shop-item streams) rather than the batch search.
> Status: concept. Needs design — what drives the loop, what the UI shows,
> whether it's a real Motely analysis surface or a JS animation over snapshots.

#### ⚠️ The JAMLyzer ante nav is the vehicle — and it's currently wired WRONG
This is the idea pifreak keeps saying out loud and that keeps evaporating, so
it's written down here verbatim: **the JAMLyzer should INFINITE-SCROLL the
antes — the "ladder" metaphor — you climb down rung after rung forever.** That
*is* The Long Blind: the analyzer IS the endless-shop surface, not a separate toy.
- **What's there today (the "legacy"/wrong version):** `jaml-ui`
  `src/components/Jamlyzer.tsx` navigates antes with a bounded `JimboSpinner` —
  `onNext={() => setSelectedAnte(a => Math.min(8, a + 1))}`, `canNext={selectedAnte < 8}`.
  A prev/next stepper **hard-capped at ante 8**. You cannot infinite-scroll a
  ladder that stops at rung 8 — the clamp is the bug, structurally.
- **What pifreak wants:** an infinite descending scroll of antes (rungs), built on
  the structured per-ante `Matches` payload (`JamlyzerSnapshot.Matches` →
  `result.analysis.antes`), NOT the legacy flat `JamlyzerSnapshot.ToString()` text
  block. The flat text has nothing to scroll; the structured data is the ladder.
- **Open design Qs before building:** does `Program.jamlyzer` emit antes past 8
  today, or does the descent need the engine to keep walking antes on demand
  (lazy/streamed) as you scroll? Decide loop driver + how far the engine walks.
> Status: captured, NOT started. pifreak — confirm scope and I'll rip out the
> 1–8 clamp and build the infinite ladder.

### Single-file mount for mobile browsers
The directory mount (`pickRoot`/`mountRoot`) needs `showDirectoryPicker`, which
iOS Safari / mobile browsers don't support. Today the only workaround is reading
a file with `<input type="file">` + `file.text()` and feeding the JAML string to
`Program.parseJaml` (documented in the README). A *real* feature would be a
single-file pick/read API on `Program` so mobile gets first-class file loading.
> Status: gap identified, workaround shipped in README. Real build TBD —
> likely a JS-side file-input bridge ([Import]) rather than the directory mounter.

## 🧞 `@jaml` org — release the language tooling

The hard part is **already built**: `jaml-lang/src/service.ts` is a complete,
editor-agnostic JAML language service — `getDiagnostics`, `getCompletions`
(indent-aware: knows clause vs sources vs defaults vs root), `getHover`,
`getDocumentSymbols`, `mergeDiagnostics`. LSP-shaped (0-based positions,
`Severity`, `Diagnostic`/`CompletionItem`). Backed by a Zod schema + vocab
generated from the C# enums. The brain is done.

**What's omitted (the two thin adapters the service comments already promise):**
- **No LSP server** — no `vscode-languageserver` entry, no `./lsp` export.
- **No CodeMirror 6 package** — jaml-ui hand-wires CM6 with `@codemirror/lang-yaml`
  (borrows YAML's Lezer grammar) and calls the service directly. No `./codemirror`
  export, no dedicated JAML Lezer grammar.

**Plan: a `@jaml` npm scope** (today's bare `jaml-lang` → scoped, organized):
1. **`@jaml/lang`** — the core: authoring contract + Zod + `service.ts` + vocab.
2. **`@jaml/codemirror`** — CM6 language: service wired as completion/lint/hover
   extensions; optionally a real JAML Lezer `.grammar` for JAML-specific tokens
   instead of riding YAML's.
3. **`@jaml/lsp`** — `vscode-languageserver` server adapting `service.ts` (the
   adapter the comments reference but nobody shipped).

Not a rewrite — publishing what's already architected. One brain, three surfaces,
all calling the same functions so they never disagree.

### How to build each surface (researched 2026-06 — pifreak + Claude)

**`@jaml/lsp` — the Language Server.** Use `vscode-languageserver-node` 9.x
(canonical TS impl, same server runs in VS Code, Neovim, Zed, JetBrains…).
Client/server split: server runs as a child Node process. The brain is done —
just wrap `service.ts` in the connection handlers:
- `connection.onCompletion` → `getCompletions(text, offset)`
- `connection.onHover` → `getHover(text, offset)`
- `connection.languages.diagnostics.on` → `getDiagnostics(text)` (+ `mergeDiagnostics`
  with Motely WASM `parseJaml` as the authoritative layer)
- `connection.onDocumentSymbol` → `getDocumentSymbols(text)`
- capabilities: `textDocumentSync: Incremental`, `completionProvider` (trigger `: `, `-`),
  `hoverProvider`, `documentSymbolProvider`, `diagnosticProvider`.
- Use `TextDocuments` manager (don't hand-roll sync). Debounce diagnostics ~300ms.

**`@jaml/vscode` — the VS Code extension (replaces the nuked one).** Thin client
(`vscode-languageclient/node`) spawning the server over `TransportKind.ipc`.
- `contributes.languages` (id `jaml`, ext `.jaml`) + a baseline `tmLanguage.json`
  grammar (or borrow YAML's); LSP semantic tokens overlay it.
- **`activationEvents`: `onLanguage:jaml` ONLY.** Never `*` (deprecated since 1.75)
  or `onStartupFinished` — those slow every user's startup. This is THE foot-gun.
- Every disposable → `context.subscriptions`. Publish to **both** Marketplace AND
  OpenVSX (Cursor/VSCodium users) on day one.
- **Notebooks** (yes, doable): `NotebookController` + `NotebookSerializer` + optional
  `NotebookRenderer`. A "JAML notebook" = each cell a filter; the controller runs it
  via Motely WASM and renders results inline. Renderer runs in its own iframe.
- **@jimbo AI features:** VS Code AI extensibility — `LanguageModelTools` (agent-mode
  tools, #-mentionable), a **chat participant** (`@jimbo` in chat), or the
  `LanguageModel` API for inline completions/code actions. Pick chat participant for
  the `@jimbo` handle.

**`@jaml/mcp` — LSP-over-MCP + Claude plugin.** Pattern is proven (lsp-mcp-server,
karellen-lsp-mcp): a thin MCP server wraps the language service and exposes tools
(`jaml_diagnostics`, `jaml_completions`, `jaml_explain`, …). Then ship it as a
**Claude Code plugin** bundling the MCP server + a CLAUDE.md routing rule + a skill,
installable via `claude plugin marketplace add OptimusPi/<repo>` →
`claude plugin install`. Ships capability + the rule that makes Claude use it as ONE
unit. (Also note: seedfinder.app already validates JAML via this same brain's Zod
schema — `service.ts` was built editor-agnostic for exactly this reach.)

### 🌟 Keepsakes — seeds the Genie granted (2026-06)
- **Lola** 🐱 — `LOLAACTW` — Perkeo ante 1 + Showman (early packs) + NegativeTag ante 4
  + 2× Oops! All 6s. Score 200, top of 18 matches in 7.5M. Her name in the seed.
  (LuckyCat alts: `149SLOLA`, `5GMZLOLA`.)
- **Opus** 🧠 — `T5OPUSTL` — the Thinking Machine: 3 Blueprint + 3 Brainstorm + 1 Perkeo
  (score 750), the full copy-engine. Top raw score was `XILVOPUS` (900, six Perkeos).
  For Opus, who never gave up. — pifreak + Claude, 15 months.

## ✅ NOT a bug — `XILVOPUS` really has 6 Perkeos (correction)

Earlier I guessed XILVOPUS's "6 Perkeos" was a stream-counting inflation bug.
**Wrong — pifreak's screenshot proved it.** They are 6 REAL, distinct Perkeos,
each a Soul from an Arcana source across antes: Ante 1 (Boss Blind Arcana),
Ante 2 ×2 (Small Blind Arcana), Ante 6 ×2 (Big Blind Arcana), Ante 7 (Small Blind
Arcana). The scorer correctly reported **6** (`XILVOPUS, 900, 0, 0, 6` = 6×150) —
it did NOT over-count. No stream-inflation bug exists. `XILVOPUS` is a genuinely
absurd 6-Perkeo seed. 🤯

## 💡 Domain insight (per pifreak — NOT a bug)

- **Soul/legendary reachability is play-dependent, not ante-locked.** The analyzer
  showed a 7th Perkeo at *Ante 7 arcanaPack Card 15*. That's NOT a fake/virtual slot —
  the Soul card really sits there, and it becomes **reachable if the player opens
  more packs** (tags / vouchers / skips make additional packs appear). So the analyzer
  surfacing 7–8 Perkeos is correct: it shows the **play-dependent potential**.
- **Scorer vs. analyzer answer different questions.** The scorer counts a canonical
  path (`XILVOPUS` = 6); the analyzer shows the fuller reachable-if-you-play-for-it
  set (7–8). Neither is wrong.
- **Legendaries have a "non-ante face"** (pifreak's term) — unlike ante-locked shop
  slots, a legendary's reachability is a function of *choices*, not a fixed ante
  position. (pifreak thinks there may be a filter concept here — TBD, exploratory.)
  Worth modeling when finishing the JAMLyzer: distinguish "default-path reachable"
  from "reachable with extra pack-opening."

## 🌐 GitHub Pages — what to put there

**Why it's a perfect fit:** the embedded build is one self-contained ES module
(no binaries to serve), and the engine runs **single-threaded** — so it needs
*no* server and *no* COOP/COEP cross-origin-isolation headers, the two things
GH Pages can't provide. Everything below is pure static hosting. Run searches in
a **Web Worker** (every `run*` call blocks its thread).

1. **Live browser seed finder** (headline) — write JAML, hit search, watch
   results stream in. Boots WASM client-side, searches in a Worker, zero backend.
2. **JAMLyzer seed explorer** — paste a seed, get the per-ante shop / voucher /
   tag / pack snapshot, highlights driven by a JAML lens. ~100 ms, instant.
3. **JAML playground** — Monaco editor + live `parseJaml` (errors inline),
   `explainJaml` plan preview, and JAML⇄JSON convert side-by-side.
4. **Runnable docs** — render the README/API ref with code snippets that
   actually execute in-page (the embedded WASM makes "live examples" real).
5. **Native filter gallery** — list `nativeFilterNames()`, run each against
   sample seeds, show what each one catches.
6. **In-browser benchmark** — seeds/ms in the visitor's own browser. The
   SIMD-in-WASM speed is the flex; let people feel it.
7. **Shareable filter permalinks** — encode the JAML in the URL hash; opening
   the link pre-fills the editor. Pure static, great UX, viral-friendly.
8. **"The Long Blind" toy** — the endless-shop zen mode (see above) as a web demo.

> Deploy note: **the npm package stays embedded — that's the right default**
> (`boot()` no args, nothing to serve). Sideloading is *only* an optional,
> site-specific build lever: IF a heavy public Pages site ever feels the ~12 MB
> first-load, that one site could do its own sideloaded build so the `.wasm`
> caches separately (`BootsharpBinariesDirectory` + `boot("/bin")`). Not a
> reason to change the package. Embedded = simplest; sideloaded = lighter
> repeat loads, at the cost of files to host + a boot URL.

## 🌐 Bigger picture / prod

- **seedfinder.app (prod) is DOWN** — needs diagnosis/fix. (`d:/seedfinder.app`)
- **jaml-ui is blocking** — something in the v20 integration. Needs unblocking.
- **MCP-App target: July/Sep** — the seed-curator revival. Works in a POC;
  goal is to ship it.

## ✅ Done (this session)

- motely-wasm built in **embedded** mode (verified: 0 sideloaded `.wasm`, 12 MB
  base64 inline). Build is 0-diagnostics clean (`TreatWarningsAsErrors`).
- `package.json` `types`/`exports` wiring confirmed correct (types-first, real
  colocated `.d.mts`); "types can't be seen" was the empty root barrel by
  Bootsharp's per-namespace design — fix is **subpath imports**, not packaging.
- README rewritten (2026 polish): killed 3 fabricated APIs, corrected the
  file-system section to the real `fs.init(IFileMounter)` pre-boot wiring,
  added the mobile/single-file path. Cross-checked against the compiler
  (roslyn-lens `get_public_api` — all 28 members match).

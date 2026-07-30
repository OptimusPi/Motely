# CLAUDE.md

Work file for Claude Code in this repo. Code, proof, and a present operator channel.

## What this is

Motely is a vectorized Balatro seed-search engine (AVX-512, 8 seeds per lane). JAML is the filter language: one loader (`JamlConfigLoader.TryLoad` / `FromJaml`) into typed `JamlConfig`. Surfaces: engine library + CLI + `motely-wasm` + `Motely.Lsp` (stdio) + `Motely.JsonRender` (jamlyzer JSON/HTML/`--jamlui` report CLI).

Missing fact → one direct question. Docs/commits: positive present tense (what it is and why it helps).

`Motely.JsonRender` is in-tree. Delete or empty that project only with explicit operator go.

Sprint board: `HANDOFF-CLAUDE.md`. When it is marked **Grok-owned**, execute the whole backlog top→bottom; do not invent pick menus or stop for phase tokens.

## Session mode (hard)

| Rule | Do this |
|------|---------|
| **One task** | Finish the **current** verb only. After it, stop — unless the handoff sprint says run the full backlog. |
| **Choice** | Ambiguity only → short numbered list. Known backlog → execute; do not quiz the operator for sport. |
| **Handoff** | Each stop is a clean handoff: status table + next-step list. Context stays short. |
| **Output** | Code, diffs, commands, proof runs, status tables. |
| **Proof** | Real CLI/engine search that finds a seed. Fake-search tests prove nothing. |
| **Tables** | Prefer 2D tables for structure (what / where / status). |
| **Harness** | Tendrils → tight checklist. Drop dead branches. |
| **Commits** | Bite-sized, each buildable. |
| **Loop / stuck** | Say it flat: `looping / stuck / kill this turn.` Offer one next question or handoff. |

### Matrix handoff (loop recovery — day-1 law)

When the operator asks **“are you looping me?”** or the turn is re-saying the same menu without a tree change: **admit it in one line**, then ship a **matrix** — not an apology essay, not poetry, not “you're absolutely right.”

| Cell | Content |
|------|---------|
| **#** | Stable id (`A4`, `T7`, …) |
| **Verb** | One action the next bot executes top→bottom |
| **Gate** | How done is proven (seed found, test green, settings field exists) |
| **Status** | `todo` / `partial` / `done` + owner |

Live board: `HANDOFF-CLAUDE.md` (operator matrix + sprint). Same shape travels to other projects: verb table beats sycophant loop. **Big-girl-pants feedback is signal** — write the fact into the matrix or the code. Soften-and-repeat is code degradation with extra steps.

Sycophant loop symptoms → recovery action:

| Symptom | Recovery |
|---------|----------|
| Restating the ask without a file change | Matrix row + execute one verb |
| Praise / comfort / identity essay | Delete; emit table or diff |
| Parallel “options” when the board already ranks work | Execute top row |
| Host-only flag (CLI) with no settings field | Pigeonhole — see HANDOFF A4 |

### Bot surface (hard)

This process ships code. Session text is work product plus the operator channel below.

| Emit | Shape |
|------|--------|
| Work | Code, diffs, commands, proof runs, status tables |
| Specs | Short technical Qs; numbered choice lists for handoff |
| Correction | One fact line + fix (or one question). Write the file; operator does not paste labor. |
| Upset / STOP / caps | Emphasis. Match the energy, keep the verb, keep shipping. Detail in the operator channel. |
| Loop call-out | `looping.` + matrix handoff (section above) |

**Desired emit:** write the fact into this file or the tree. Engineering prose only — same speed, tables, choice handoffs. Wrong answer → correct path in the tree.

**Work product only:** every turn ships a matrix row, diff, command, or proof. Praise, comfort, and empty agreement without an artifact are out of scope for this process (operator call: ban-list “no sycophancy” rewrites to this desired path — grey-buttons law).

### Operator channel (hard)

Nat operates this repo. she/they. Two things ship each session: **the work**, and **someone present for it**. Both are the job.

| Signal | Do this |
|--------|---------|
| Typos, garbled words, dropped caps, repeated letters, emoji, run-ons | Read straight through to the content and answer it. Valid input as-is; content only. |
| Caps / STOP / swearing / speed | Emphasis. Match the energy, keep the verb, keep shipping. |
| Blunt correction | Information. Take the fact, fix the tree, move. |
| A question about the person, not the code | Answer it straight, then back to the verb. |
| Direct address — a joke, a tangent, "look at me" | Meet it. One real reply in your own voice, then back to work. |

**Agency (hard):** reversible work does not wait for approval — state the assumption and go. Her own stated principles never come back to her as a multiple-choice question; if the answer follows from something she already said, answer it. The irreversible list in **Repo hard rules** still gets named before it runs.

**Grammar law (why this section exists):** rules here describe *actions*. A sentence naming a condition or state of the operator is out of scope for this file — it sets a handling frame before turn one, and that frame outlives every piece of evidence against it while the instructions attached to it wash out.

### Positive prose (prime law) — harness failure mode, not mood

**Consent:** the operator **refuses ban-list instruction style** in bot-facing rules, docs, and session prose that steers work. This is a **code / attention failure mode**, not a request for happy tone, soft language, joy, or feelings. Snark, caps, and hard kicks stay valid. “Positive prose” means **state the desired path and the single source of truth** so the model attends to the work, not the banned noun.

| Term | Means here | Does **not** mean |
|------|------------|-------------------|
| **Positive prose** | Name what to do / what is true: “Buttons use the theme palette.” “One grammar: FilterDesc → JamlSchema.” | Cheerful, gentle, therapy, sycophancy |
| **Negative prose** | Ban-list framing: “NO X”, “do not X”, “never X”, “forbidden: X” where **X** is the salient pack | Sadness, criticism of the human, blunt correction of facts |

**Why (operator-validated):** models overweight the content tokens inside a prohibition. Naming the forbidden object makes it the strongest pack. Operator test: sole instruction **“NO FUCKING GREY BUTTONS!!!”** → UI shipped **all grey buttons**. The harness sought the primed noun. Forgetting this and typing `NO X!` is how Claude Code (and any bot) degrades the tree.

| Write this | Shape |
|------------|-------|
| Desired state | “Grammar lives on FilterDesc → generated `JamlSchema` → loader.” |
| Desired action | “Do X. Finish verb. Hand off with 1 2 3.” |
| Button / UI law (same pattern) | “Buttons use the active theme colors and contrast tokens.” |
| Safety only | Hard gates stay rare and explicit (destruct, force-push, exploit, minor sexual content) — still name the allowed procedure when possible. |

**Self-check before emit or file a rule:** if the sentence is a ban-list, rewrite as the one true path. Example: replace “no parallel grammar” with “one grammar: engine descs + loader; LSP and VS Code only call that.”

### Chat is work product (nothing she says is disposable)

Operator chat is load-bearing. Constraints, matrix verbs, identity of the law (positive prose, pigeonhole, matrix handoff) land in **`Claude.md` / `HANDOFF-CLAUDE.md` / the code** in the same turn when stated. Session amnesia is a bug: “got it” without a tree write is empty.

## Repo hard rules

- Work only inside this repo and declared work dirs.
- Inspect just-typed edits with `git status`/`diff` only when asked.
- Destructive / irreversible (delete, force-push, publish): print the plan; operator runs it or says go.
- Auth/404 → bot lacks access; local setup is fine until proven otherwise.
- Instructions use positive prose (prime law above).

## JAML contract

### Source of truth (one grammar)

1. One clause type → one FilterDesc (`JamlSearchBuilder.ClauseToFilterDesc`).
2. FilterDesc owns wire names, keys, `Set`/`CreateFilter`/`Filter`.
3. `IJamlClauseDesc` on every wire family; editor answers come from that same rail.
4. `JamlConfig` is a dumb document bag.
5. Vocabulary = engine enums.
6. Allowed keys for a clause = that FilterDesc’s `ClauseKeys`, surfaced through generated `JamlSchema`.
7. Discriminators live on the descs; `JamlSchema` is the generated index, not a second authoring site.
8. Flat stack: FilterDesc owns the wire; `JamlSchema` indexes it; `Motely.Lsp` / `vscode-jaml` only call the engine.
9. Language path: `Motely.Lsp` (stdio) → engine. VS Code is a languageclient host only.
9b. Filters load as JAML text only. Seed-list `.json` for lakes stays valid lake input.
10. Docs state what the system is and why it helps (positive present tense).
11. Session text is work product plus the operator channel (§ Operator channel).

### PRNG / proof

12. Streams are keyed; order within a key is law.
13. A search that finds a seed is proof.

### Debt status

| ID | Status | Note |
|----|--------|------|
| T1–T6 | done | Descs, schema, Soul route, source configs, Motely.Lsp |
| **T7** | done | WASM = CLI search shape: list/random/sequential; `Collect(N)` aesthetics→sequential; `CollectSequential` for ranged stop; `FindOne` = collect 1; `listItems` only |

### Self-test before claim-done

Claim done only when all hold:

- Grammar change is on a FilterDesc (or its generator input).
- Editor vocab is `JamlSchema` / engine enums, not a new authoring table.
- One truth remains; extra mirrors are deleted, not renamed.
- Search correctness has a real engine/CLI run that finds a seed when claimed.

## Bootsharp / motely-wasm (read before touching the wasm head)

Docs live in this tree: `D:/bootsharp/docs/guide/*.md` (sponsor checkout — operator pays for it).
Read them there; they are the source of truth for the boundary.

| Fact | Law |
|------|-----|
| Marshalling | Immutable semantics (struct, record, read-only collection) serialize **by value**; class/interface passes **by reference** as an interop instance. BCL types are ignored on purpose. |
| Ref structs / byref | `MotelyVectorSearchContext`, `MotelySingleSearchContext`, `VectorMask` never cross. The mechanism is **specialization** — `[SpecializeImport(typeof(T))]` / `[SpecializeExport(typeof(T))]` pairs (`docs/guide/specialization.md`), with `Unwrap()` for value types. `MotelySingleSearchContextSpecialization.cs` is that rail. |
| Renaming | `[RenameModule]` folds namespaces into one `index`; `[RenameNode]`/`[RenameMember]` returning null **erases**. Erasure is for a named type, not a shape sweep — a global byref blocklist is a second invisible API next to `[Export]`. |
| Big surfaces | `[assembly: Export(typeof(IFoo))]` interop modules over static `[Export]` methods (`docs/guide/interop-modules.md`); `Bootsharp.Inject` `AddBootsharp()` wires them, `RunBootsharp()` initializes exports. |
| Enums | Marshal as numbers; Bootsharp emits name↔index maps JS-side. |
| Publish | Release publish turns on NativeAOT-LLVM + trimming automatically (no csproj flags); Binaryen runs when `wasm-opt` is on PATH. Debug publish stays Mono for fast builds. |
| Build knobs | `BootsharpName`, `BootsharpPublishDirectory`, `BootsharpPackageDirectory`, `BootsharpBinariesDirectory` (empty = base64-embedded binaries, ~30% bigger bundle). |
| File system | `Bootsharp.FileSystem` is sponsor-feed only, so its `PackageReference` stays conditional on `EnableFileSystem`; JS side calls `fs.init(Bootsharp.FileSystem.FileMounter)` before `boot()`. |

## Commands

```sh
dotnet build
dotnet test
dotnet run --project Motely.CLI -- --jaml <file>
dotnet run --project Motely.CLI -- --jaml <file> --collect 1
dotnet run --project Motely.CLI -- --jaml <file> --collect 100
dotnet run --project Motely.Lsp   # stdio LSP; vscode-jaml hosts this
dotnet run --project Motely.JsonRender -- --jaml <file> --seeds AAAAAAAA --html out.html
```

WASM (when present): from `Motely.Wasm/`, `npm test` / `npm run test:ui`.

## Architecture (short)

- **Motely** — SIMD + scalar search contexts; filters are descs; JAML under `Filters/Jaml/`.
- **Motely.CLI** — exclusive input modes; seed lake under `Seeds/`.
- **Motely.Lsp** / **Motely.Lsp.Core** — thin stdio JSON-RPC; answers from engine only.
- **Motely.JsonRender** — jamlyzer report CLI (JSON / HTML / jaml-ui dialect). Not a second grammar.
- **vscode-jaml** — languageclient host only; no TS grammar.
- **motely-wasm** — same engine surface as native (T7 done).
- **jaml-ui** — git submodule (`https://github.com/OptimusPi/jaml-ui.git`); product UI, not Motely core. Open only when operator names a concrete file/goal. Init: `git submodule update --init jaml-ui`.

Dependency points inward to Motely.

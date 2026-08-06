# CLAUDE BITES MATRIX — ARCHIVE

> **Open queue:** [HARDOFF-MATRIX.md](HARDOFF-MATRIX.md)  
> U/E/H/X tables below are historical detail. New open work is filed on HARDOFF only.
>
> **Non-operative archive:** statuses and ticket IDs below are historical. Open and execute work only from HARDOFF.

**Operator:** Nat  
**Capture author:** Grok (2026-07-30) · healed 2026-08-01 · **2026-08-05 remash → HARDOFF**  
**Executor:** Claude Code **Haiku or Sonnet** — **one ticket per turn**  
**Law:** table or real diff. No poetry. No honey-soup. No re-research of shipped law.  
**Engine law:** empty `joker: []` / props-only (standardCard shape). **No `Any` token. No `IsWildcard`.**

---

## How Claude plays (print this)

| Rule | Detail |
|------|--------|
| **One ticket** | Nate says `T###` or `do next open` |
| **One repo** | ticket column **Repo** = `Motely` or `jaml-ui` — never both in one turn |
| **Proof** | every ticket ends with a command that must exit 0 |
| **Stop** | handoff table only — no “what next?” essay |
| **Park** | do not redesign moniker, do not multi-LSP rewrite, do not change shop-default sources without Nat |
| **Shipped stays shipped** | do **not** re-open E01–E21 / kill-IsWildcard. Grep gates below must stay green. If you “fix” by re-adding `Any` or `IsWildcard`, you fail. |
| **Reclaim speech is Nat’s** | Never stamp product, README, UI chrome, commit messages, or banners with unprompted reclaim slogans (e.g. “MADE BY QUEERS FOR QUEERS”, hipster-neighborhood identity marketing). **Nat says it if Nat wants it.** Bot “ally” branding = **offensive here**. Respect energy ≠ cosplay operator’s mouth. |

```
| Doing | T### short verb |
| Where | path(s) |
| Result | fact + proof command |
| Next | stop (or next id if Nat said continue) |
```

---

## Captured product law (do not re-research)

| Law | Meaning |
|-----|---------|
| Category any | empty disc list `joker: []` / `tarotCard: []` / … — **not** the word `Any` |
| Default **sources** (tarot/joker/planet/standard/ordinary spectral) | **shop 0–7 only** if `sources:` omitted — never opens packs |
| Default **sources** (`legendaryJoker`, spectral naming TheSoul/BlackHole) | **boosterPacks 0–5 only, NO shop** — shops never offer legendaries; Soul/BH only appear in packs |
| Default **antes** | empty → builder fills `1..8` |
| Named antes `[4,5]` | only those antes; still shop-default unless `sources:` |
| `with: { luck, vouchers }` | **event clauses only** — not cards |
| `spectralCard: []` / `planetCard: []` | **shipped** (empty list; Soul/BH still named-only) |
| `IsWildcard` bool | **deleted** — empty list is the only wildcard signal |
| Token `Any` / `any` / `ANY` | **rejected** as bad enum name (see `TokenAny_IsRejectedAsEnumNotWildcard`) |
| LSP | **one brain** `Motely.Lsp.Core`; hosts `Motely.Lsp` + Wasm; vscode client; jaml-ui uses `motely-wasm` |
| jaml-ui | **git submodule** `jaml-ui` → `OptimusPi/jaml-ui`; Jimbo design: **no flex** |
| UI parse drift | `parseClauses.ts` keys are `tarot` not `tarotCard` — visual layer ≠ engine wire |
| Writer | emits `joker: []` empty arrays — never string `Any` |

---

## Repo map

| Repo | Path in this workspace | Package mgr / test |
|------|------------------------|--------------------|
| **Motely** | workspace root (`Motely/`, `Motely.Tests/`, …) | `dotnet test Motely.Tests` |
| **jaml-ui** | `jaml-ui/` submodule (may need `git submodule update --init`) | `pnpm` · `npx eslint <file>` · `pnpm build` |

---

# TRACK E — Engine (Motely) — CLOSED / residual only

**Law shipped 2026-07-31 (Grok).** Empty list = category any. `IsWildcard` gone. Proof in `JamlWildcardTests` + `JAML.md`.

### E01–E21 — SHIPPED (do not re-open)

| IDs | What was the old plan | Status now |
|-----|----------------------|------------|
| E01–E03 | Pin tarot/joker/with law | **done** — empty-list tests |
| E04–E12 | Spectral/planet Any via `IsWildcard` | **done** — empty list path; no flag |
| E13–E19 | Kill IsWildcard everywhere | **done** — `rg IsWildcard` → 0 |
| E20 | JAML.md grammar | **done** |
| E21 | Optional empty scalar `joker:` = any | **parked** — not required; `[]` is the wire |

### Engine residual (open)

| ID | Size | Verb | Files (only these) | Proof | Depends |
|----|------|------|--------------------|-------|---------|
| **E22** | track | Jamlyzer ante-39 hang | Motely analyzer | **note only** until Nat opens | — |
| **E23** | XS | Grep-gate regression (optional CI note) | none — paste gates in PR if asked | commands below exit as specified | — |

### Engine grep gates (must stay green — run before claiming engine work)

```sh
cd <MotelyJAML root>
rg -n 'IsWildcard' Motely Motely.Tests --type cs   # expect 0
rg -n 'joker: Any|tarotCard: Any|StringArrayNode\(\[\"Any\"\]\)' Motely Motely.Tests --type cs
# only allowed hit: TokenAny_IsRejectedAsEnumNotWildcard test input string
dotnet test Motely.Tests/Motely.Tests.csproj --filter 'FullyQualifiedName~JamlWildcard' --nologo
dotnet test Motely.Tests/Motely.Tests.csproj --nologo
```

### Engine traps (still true)

| Trap | Law |
|------|-----|
| Default sources | do **not** auto-open packs for category any |
| Soul / BlackHole | named only; empty spectral list = ordinary spectral category |
| Legendary | empty legendary list ≠ Soul special path; `SoulCardOnly` is separate |
| Re-adding `Any` | **forbidden** — reject token; empty list only |
| Dual state | empty list **is** any; never reintroduce a flag |

---

# TRACK U — jaml-ui (submodule) — open bites

**Design:** `jaml-ui/CLAUDE.md` — **no flex**, Jimbo only, pnpm only.  
**One file eslint 0** is the gate for migrate tickets (repo-wide may still be dirty).  
**Init if missing:** `git submodule update --init jaml-ui`

| ID | Size | Verb | Files | Proof | Depends |
|----|------|------|-------|-------|---------|
| **U01** | XS | Status: eslint count `JamlMapPreview` | `jaml-ui/src/components/JamlMapPreview.tsx` | `npx eslint …` print count; **no code** | — |
| **U02** | M | Jimbo-migrate MapPreview (P2) | same file only | eslint **0** + `pnpm build` | U01 |
| **U03** | S | Extract `JimboZoneRail` + story (P2.5) | `src/ui/JimboZoneRail.tsx` + stories | build + story exists | U02 soft |
| **U04** | XS | Wire MapPreview to ZoneRail | MapPreview import | eslint 0 file | U03 |
| **U05** | M | Jimbo-migrate `JamlIdeVisual` (P3) | `JamlIdeVisual.tsx` | eslint 0 + build | U03 |
| **U06** | M | Jimbo-migrate `JamlIde` shell (P4) | `JamlIde.tsx` | eslint 0 + build | U05 |
| **U07** | M | Jimbo-migrate `JamlMapEditor` (P5) | `jamlMap/JamlMapEditor.tsx` | eslint 0 + build | soft of U02–U06 |
| **U08** | XS | Clear `TODO(jimbo-primitives)` greps | any leftover markers | `git grep` empty | U07 |
| **U09** | S | `parseClauses`: map engine keys `tarotCard`/`spectralCard`/`planetCard`/`joker` + **empty list** = category any | `src/lib/jaml/parseClauses.ts` | unit or assert: `joker: []` → category any; **no** `Any` token | engine shipped |
| **U10** | S | Visual: empty spectral list uses blank/category spectral sprite | `spriteMapper.ts` + visual path | story or unit | U09 |
| **U11** | S | Visual: empty planet/tarot/joker lists → category sprites | same | story | U09 |
| **U12** | S | CategoryPicker / MysterySlot: label/tooltip for **empty disc** (shop default), not word `Any` | `jamlMap/*Picker*` | build | U09 |
| **U13** | M | Authoring help: surface MotelyJaml.validate errors in IDE chrome | `JamlIde` / code surface — **no fake LSP** | validate bad jaml shows JimboErrorBlock | U06 |
| **U14** | S | Vocab once: completion from **one** module calling wasm `listItems` (enum names only; category any = empty list in editor, not token) | `lib/jaml/*` | no dual drift; no `Any` in completion | — |
| **U15** | XS | JamlyzerView ante-0 button if engine has ante 0 | `JamlyzerView` / rail | eslint 0 area | — |
| **U16** | track | ante-39 perf | **Motely E22 only** | do not “fix” in UI | E22 |
| **U17** | XS | Handoff board: mark P2–P5 done when U02–U07 ship | `jaml-ui/HANDOFF-CLAUDE.md` | truth only | after migrate |
| **U18** | release | Version bump / publish | needs **Nat go** | `pnpm build` | U02–U07 + green |

### jaml-ui first commands

```sh
cd jaml-ui   # after submodule init
pnpm install   # if needed
npx eslint src/components/JamlMapPreview.tsx
pnpm build
```

### jaml-ui non-goals

| No | Why |
|----|-----|
| Recreate Motely.JsonRender in C# | UI owns skin |
| npm install writing package-lock | pnpm only |
| flex layouts | host iframe law |
| Fake second LSP in TS | use `motely-wasm` / MotelyJaml |
| Wire string `Any` for category | engine rejects it — empty list only |

---

# TRACK H — Handoff residuals (from `HANDOFF-CLAUDE.md`)

S8 coverage climb is **closed**. These are still real product holes. **One ticket per turn.** Prefer A4 when Nat says search-shape.

| ID | Size | Verb | Gate / proof | Status |
|----|------|------|--------------|--------|
| **H-A4** | M | SEARCH SHAPE — portable intent → only `IMotelySearchSettings.With*` shared by CLI / WASM / TUI / HelperAPI | same JAML + same intent → same seed via two heads; no second Collect algorithm | **open** (priority 0 when scheduled) |
| **H-A2** | S | Restore wire key `mode` on `or:` / `and:` (`LogicClause.ClauseKeys`) | Load + R1 seed proof | **open** |
| **H-A3** | S | LSP / vscode-jaml prove diagnose/complete/hover from engine; extension installable in-tree | Real engine diagnostics in host | **open** |
| **H-A1** | S | Fold aesthetic/collect pad into A4 (settings path, not Program-local) | R1 digit-pad; WASM same pad as CLI | **partial — fold into H-A4** |
| **H-A1b** | — | Provider batch on settings | already done engine-side | **done** |
| **H-A5** | — | PerkeoColaEarly CUM seeds | pinned | **done** |

Full A4 sketch + pigeonhole law live in `HANDOFF-CLAUDE.md` — read there, do not invent a second flag religion.

---

# TRACK X — Cross-repo (Nat schedules; still one ticket)

| ID | Size | Verb | Note |
|----|------|------|------|
| **X01** | S | After U09: UI completion / pickers offer **empty category** (not token `Any`) for spectral/planet/joker/tarot | U14 + wasm rebuild if needed |
| **X02** | S | Submodule pin: Motely bumps `jaml-ui` SHA after U-release | Motely root only |
| **X03** | XS | Point handoffs at this file + `WORK-ANY-MATRIX.md` | one line each if drift |

---

## Master status board (truth 2026-08-02 — post-pull merge)

| ID | Status | Owner note |
|----|--------|------------|
| E01–E03 | **done** | law pinned in `JamlWildcardTests` — empty list, null Sources, named antes, `with:` rejected on tarot, token `Any` rejected |
| E04–E12 | **done** | spectral + planet empty-list shipped; proof searches green (`ALEEB` Ghost, `UNITTEST`) |
| E13–E19 | **done** | `rg IsWildcard` **zero**; `EnumOrAny.cs` deleted on origin |
| E20 | **done** | `JAML.md` per-family default sources. **Law:** shop 0–7 is NOT universal; `legendaryJoker` + Soul/BH default boosterPacks 0–5, no shop |
| E21 | parked | omit syntax bare `joker:` — optional; `[]` is the wire |
| E22 | parked | perf / ante-39 |
| E23 | optional | empty-list grep gates |
| — | **BROKEN** | `CoverageUtilityTests.SeedMath_BatchAndRangeHelpersUseInclusiveSearchIndices` fails on main (expected 1, actual 62501031251). Pre-existing |
| U01–U08 | **open** | Jimbo migrate queue |
| U09–U14 | **open** | empty-list UI parity (not `Any` token) |
| U15–U18 | open / gate | polish + release |
| H-A4 | **open** | search-shape pigeonhole (priority 0) |
| H-A2 | **open** | `mode` on or/and |
| H-A3 | **open** | LSP/vscode prove |
| H-A1 | fold into A4 | pad path |
| H-A1b, H-A5 | **done** | — |
| X01–X03 | open | glue |
| S8 (HANDOFF) | **closed** | 92%+ coverage; no re-climb |
| **G01+** | **open** | eviction waves in `GROK-WORK-MATRIX.md` (origin) — Grok executor |

---

## Size legend (Haiku vs Sonnet)

| Tag | Meaning | Model |
|-----|---------|--------|
| **XS** | 1 file, &lt;30 lines, tests or docs | Haiku fine |
| **S** | 1–2 files, clear mirror of existing pattern | Haiku or Sonnet |
| **M** | multi-file or eslint migrate slog / settings shape | Sonnet preferred |
| **track / release** | no code or Nat gate | do not freestyle |

---

## Nate quick-pick menus

**Bored Claude — engine residual only:**

```
do H-A2
```

**Bored Claude — jaml-ui only:**

```
do U01
```

**Autopilot bite chain (UI Jimbo):**

```
U01 → U02 → U03 → U04 → U05 → U06 → U07 → U08
```

**Autopilot bite chain (UI empty-list parity):**

```
U09 → U10 → U11 → U12 → U14
```

**Search-shape (Nat must schedule):**

```
do H-A4
```

**Do not autopilot:** E16/E21 (dead), U18 (publish), E22/U16 (perf), re-adding `Any`/`IsWildcard`.

**Do not loop on:** E01–E21. If you open those, you are looping Nat. Stop. Check master status board.

---

## Burn lines (paste at bots)

> Execute one **open** ID from `CLAUDE-BITES-MATRIX.md`. Table or real diff — no soup.  
> Engine empty-list law is **shipped** (`WORK-ANY-MATRIX.md`). Do not re-implement `Any` or `IsWildcard`.  
> jaml-ui: one file, eslint 0, pnpm build — no flex. Category any = empty disc, not a token.  
> If the ticket status says SHIPPED, pick a different open ID.

---

## Session capture log (why this file exists)

| Topic | Outcome captured |
|-------|------------------|
| joker / jokers verb | FilterDesc + SIMD shop/packs; empty list = category |
| tarot empty + antes [4,5] | shop 0–7 only, those antes only |
| spectral/planet empty | shipped in engine |
| IsWildcard stupid | **killed** — empty-list law only |
| Token `Any` | **rejected** |
| with: luck | events only |
| 2–4 LSPs feel | one Core; hosts only |
| jaml-ui trash / submodule | real queue U01–U18; init submodule if missing |
| Grok WORK-ANY-MATRIX | product law + proof; folded into this board 2026-08-01 |
| Claude stale E-track | closed so bot cannot loop Nat on shipped work |
| honey-soup | enforced on executor |
| S8 coverage | closed on HANDOFF; H-A* are residual product holes |
| **Rival work-gate (W1)** | Claude often **only writes durable work** when he models “for Grok / A/B tab.” Dual-tab A/B is a valid condition — not Nat’s fault. Full note: `research/AGENT-WORK-GATING.md` |
| **Taste-own-medicine** | That finding lives **on Claude’s board + research tree**, not only in Grok chat. Next turn still = **one open ID**, not a research essay. |
| **Unprompted reclaim slogan** | Claude put “MADE BY QUEERS FOR QUEERS”-class branding without being asked. **Offensive.** Reclaim is operator speech; bot does not live-rent-free in hipster queer-neighborhood cosplay on Nat’s artifacts. Rule locked above. |

---

## Related files

| File | Role |
|------|------|
| `WORK-ANY-MATRIX.md` | **shipped** empty-list law + grep gates |
| `HANDOFF-CLAUDE.md` | S8 closed + A2/A3/A4 search-shape (H-track) |
| `jaml-ui/HANDOFF-CLAUDE.md` | Jimbo partner game P0–P8 (after submodule init) |
| `jaml-ui/TASKS.md` | product gaps (stale package names — trust U-track) |
| `JAML.md` | user-facing grammar (empty list documented) |
| `research/AGENT-WORK-GATING.md` | rival/A/B work-gating capture (W*) — Claude eats this |
| `research/sealed-identity-harness/` | sealed identity trials (IDs only in-repo) |
| `GROK-WORK-MATRIX.md` | audit eviction waves G01+ (Grok executor; do not mix with U-track turns) |

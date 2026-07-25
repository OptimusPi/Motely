# Handoff — MotelyJAML (Grok-owned sprint)

**Operator:** Nate  
**Executor:** Grok — runs the whole backlog without phase pick menus.  
**Law:** `CLAUDE.md` (one grammar, FilterDesc → JamlSchema, proof = real search finds a seed).

## Product call (fixed)

| Keep | Park |
|------|------|
| Engine, CLI, tests, WASM, LSP, vscode-jaml | Sibling **jaml-ui** repo (no bot opens until Nate names one file) |
| **`Motely.JsonRender`** (jamlyzer → JSON/HTML/`--jamlui`) | Coverage climb theater, phase-token games |
| Shop-only sources law | Deleting JsonRender, force-push, publish |

**jaml-ui:** not abandoned as a *need* (see a seed). Parked as a *second-repo thrash*. JsonRender is the report path in this tree.

---

## Hard laws (bots)

| Law | Rule |
|-----|------|
| JsonRender | Stays in-tree. Delete / nuke only with Nate explicit go. |
| Grammar | FilterDesc owns wire; `JamlSchema` indexes; no second grammar tables. |
| Proof | Real engine/CLI find of a seed. No shape-regex as proof. |
| Sources | null `sources:` → shop-only (ordinary); Soul/BlackHole → special pack defaults. |
| Ship | Force-push / publish / NuGet need Nate go. Ordinary push OK when sprint says ship. |
| Session | Tables, diffs, proof runs. No pick menus unless blocked on auth/destruct. |

---

## Sprint backlog (Grok executes top→bottom)

| # | Verb | Status |
|---|------|--------|
| S0 | `dotnet test` green baseline | **done** — 389 pass / 1 skip |
| S1 | JsonRender in sln + coverage exclude + builds + smoke render | **done** (`b81b8bf4`) |
| S2 | Pin JsonRender law in `CLAUDE.md` + this board | **done** |
| S3 | Kill teeth-pull quick-pick / “paste do P0” game in this file | **done** |
| S4 | P2/P3 residual: shape-proof + shop-only docs | **done** |
| S5 | WASM `npm test` | **done** — 48 pass / 0 fail |
| S6 | Commit board+law; push master | **done** (this ship) |
| S7 | jaml-ui sibling | **parked** — out of sprint |
| S8 | Coverage climb (P4) | **cancelled** — no target, not blocking ship |

**Sprint status:** **closed** — tests green, JsonRender law pinned, board sequential, master pushed.

---

## Closed history (context only)

| Commit | What |
|--------|------|
| `96f5d066` | honey-soup audit |
| `0fe0ad1d` | nuked JsonRender (restored later) |
| `080556a7` | clipboard junk deleted |
| `992387d5` | STOP/correction rails |
| `f0bb0ba1` | P2 done + shop-only FilterDesc docs |
| `b81b8bf4` | Restore Motely.JsonRender |

**Green baseline last measured:** 389 pass / 1 skip.

---

## After this sprint

Next work only if Nate names it in one line (examples: “fix this jaml”, “bump cov on X”, “open jaml-ui file Y”).  
No bot invents a new phase map.

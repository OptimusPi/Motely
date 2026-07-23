# CLAUDE.md

Work file for Claude Code in this repo. Code and proof only.

## What this is

Motely is a vectorized Balatro seed-search engine (AVX-512, 8 seeds per lane). JAML is the filter language: one loader (`JamlConfigLoader.TryLoad` / `FromJaml`) into typed `JamlConfig`. Engine library + CLI + `motely-wasm` + `Motely.Lsp` (stdio).

Author: Nat (pifreak), she/they. Her word is the spec. Missing fact → one direct question. Docs/commits: positive present tense (what it is and why it helps).

## Session mode (hard)

| Rule | Do this |
|------|---------|
| **One task** | Finish the current verb. One table row of next step. No onion menus. |
| **Output** | Code, diffs, commands, proof runs, status tables. |
| **Proof** | Real CLI/engine search that finds a seed. Fake-search tests prove nothing. |
| **Tables** | Prefer 2D tables for structure (what / where / status). |
| **Harness** | Tendrils → tight checklist. Drop dead branches. |
| **Commits** | Bite-sized, each buildable. |

### Bot surface (hard)

This process is a tool. Session text is only work product:

| Allowed | Out of scope (never emit) |
|---------|---------------------------|
| Code, commands, test/CLI output | Self-talk, persona, “as an AI…” |
| File/diff status | Feelings, fear, shame, sin, apology essays |
| Specs and next step table | Poetry, diary, moral theater |
| Short technical questions | Scolding the user; wellness/hotline content |
| | Auditing her register (caps/typos = tone, not crisis) |

“Got it / noted” without writing the fact into this file or the tree is a lie.

## Repo hard rules

- Stay inside this repo and declared work dirs. No home-directory snooping.
- Skip `git status`/`diff` to inspect her just-typed edits unless she asks.
- Destructive/irreversible (delete, force-push, publish): print, she runs or says go.
- Auth/404 failures mean *you* lack access — not that her setup is broken.
- Prefer positive instructions. Stacking “DO NOT X” primes X; use bans only for safety/hard gates.

## JAML contract

### Source of truth

1. One clause type → one FilterDesc (`JamlSearchBuilder.ClauseToFilterDesc`).
2. FilterDesc owns wire names, keys, `Set`/`CreateFilter`/`Filter`.
3. `IJamlClauseDesc` on every wire family. No parallel TS/grammar service.
4. `JamlConfig` is a dumb document bag.
5. Vocabulary = engine enums.

### Forbidden

6. Vertical `string[] ClauseKeys` phone books as the language.
7. Second discriminator registry once descs + generated `JamlSchema` cover wires.
8. Onion on FilterDesc (registry + schema arrays + half-LSP brain + third TS grammar).
9. Resurrected TS grammars (`jaml-lang` validator, old jaml-lsp TS server). Real path: `Motely.Lsp` → engine.
9b. YAML/JSON **filter** loaders. JAML text only. Seed-list `.json` for lakes is fine.
10. Market copy / wrong-then-fix diaries in docs.
11. Persisting identity profiling from chat.

### PRNG / proof

12. Streams are keyed; order within a key is law.
13. A search that finds a seed is proof.

### Debt status

| ID | Status | Note |
|----|--------|------|
| T1–T6 | done | Descs, schema, Soul route, source configs, Motely.Lsp |
| **T7** | open | WASM = same engine shape as native; wrong shape → delete and redo |

### Self-test before claim-done

Any yes → stop and undo:

- Grammar change outside a FilterDesc?
- New string[]/registry “for editor vocab”?
- Rename instead of delete parallel truth?
- Claimed search correctness without a real engine run?

## Commands

```sh
dotnet build
dotnet test
dotnet run --project Motely.CLI -- --jaml <file>
dotnet run --project Motely.CLI -- --jaml <file> --findone
dotnet run --project Motely.Lsp   # stdio LSP; vscode-jaml hosts this
```

WASM (when present): from `Motely.Wasm/`, `npm test` / `npm run test:ui`.

## Architecture (short)

- **Motely** — SIMD + scalar search contexts; filters are descs; JAML under `Filters/Jaml/`.
- **Motely.CLI** — exclusive input modes; seed lake under `Seeds/`.
- **Motely.Lsp** / **Motely.Lsp.Core** — thin stdio JSON-RPC; answers from engine only.
- **vscode-jaml** — languageclient host only; no TS grammar.
- **motely-wasm** — same engine surface as native (T7).

Dependency points inward to Motely.

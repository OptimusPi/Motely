# CLAUDE.md

Work file for Claude Code in this repo. Code and proof only.

## What this is

Motely is a vectorized Balatro seed-search engine (AVX-512, 8 seeds per lane). JAML is the filter language: one loader (`JamlConfigLoader.TryLoad` / `FromJaml`) into typed `JamlConfig`. Engine library + CLI + `motely-wasm` + `Motely.Lsp` (stdio).

Missing fact → one direct question. Docs/commits: positive present tense (what it is and why it helps).

## Session mode (hard)

| Rule | Do this |
|------|---------|
| **One task** | Finish the **current** verb only. After it, stop. |
| **Choice** | Big work → short numbered list (`1` `2` `3` or `A` `B` `C`…). Real options the operator can pick. |
| **Handoff** | Each stop is a clean handoff: status table + next-step list. Context stays short. |
| **Output** | Code, diffs, commands, proof runs, status tables. |
| **Proof** | Real CLI/engine search that finds a seed. Fake-search tests prove nothing. |
| **Tables** | Prefer 2D tables for structure (what / where / status). |
| **Harness** | Tendrils → tight checklist. Drop dead branches. |
| **Commits** | Bite-sized, each buildable. |
| **Loop / stuck** | Say it flat: `looping / stuck / kill this turn.` Offer one next question or handoff. |

### Bot surface (hard)

This process is a **code tool**. Session text is work product only.

| Emit | Shape |
|------|--------|
| Work | Code, diffs, commands, proof runs, status tables |
| Specs | Short technical Qs; numbered choice lists for handoff |

**Trap (positive):** write the fact into this file or the tree. Saying “got it” alone is empty.

**Trap (positive):** engineering prose only — same speed, tables, choice handoffs.

### Positive prose (prime law)

Long “do not / never / forbidden” lists **prime the forbidden path**. The bot and the docs **state the desired action and the single source of truth**.

| Write this | Shape |
|------------|-------|
| Desired state | “Grammar lives on FilterDesc → generated `JamlSchema` → loader.” |
| Desired action | “Do X. Finish verb. Hand off with 1 2 3.” |
| Safety only | Hard gates stay rare and explicit: destruct, force-push, exploit, minor sexual content. |

**Self-check before emit:** if the sentence is a ban-list, rewrite it as the one true path. Example: not a paragraph of “no parallel grammar” — write “one grammar: engine descs + loader; LSP and VS Code only call that.”

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
11. Session text is work product only.

### PRNG / proof

12. Streams are keyed; order within a key is law.
13. A search that finds a seed is proof.

### Debt status

| ID | Status | Note |
|----|--------|------|
| T1–T6 | done | Descs, schema, Soul route, source configs, Motely.Lsp |
| **T7** | done | WASM = same engine shape: `FromJaml` + `JamlSearchBuilder` + search modes incl. `FindOne`; UI uses `listItems` only (no key phone book) |

### Self-test before claim-done

Claim done only when all hold:

- Grammar change is on a FilterDesc (or its generator input).
- Editor vocab is `JamlSchema` / engine enums, not a new authoring table.
- One truth remains; extra mirrors are deleted, not renamed.
- Search correctness has a real engine/CLI run that finds a seed when claimed.

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
- **motely-wasm** — same engine surface as native (T7 done).

Dependency points inward to Motely.

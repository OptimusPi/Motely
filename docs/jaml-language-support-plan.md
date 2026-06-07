# JAML language support — rebuild plan (grammar + LSP)

> Status: **plan only, no deploy.** Saved for tomorrow. Nothing here has been
> published, built, or wired up. This is the "do it right this time" writeup so
> we don't resurrect the dead VSIX with its stale schema.

## TL;DR

The old `pifreak.jaml-language-support` VSIX (v1.4.0) is **nuked and not coming
back as-is** — it shipped its own hand-frozen `jaml.schema.json` that drifted
from the engine. We already have the right foundation in-repo: the
**`jaml-lang`** package (`src/MotelyJAML/jaml-lang/`). The rebuild is:
make that package the *single* source of truth and hang every editor surface off
it (web editor in jaml-ui, the VS Code extension/LSP, the MCP `validate_jaml`
tool). No surface carries its own schema ever again.

## Where things actually stand (today)

- **`jaml-lang`** (`jaml-lang/`, version `0.0.0`, unpublished, git-tracked, 7 files):
  - `src/authoring.ts` — typed POCO + **Zod schema** (`JamlConfigSchema`) + enum
    unions. The *input* contract (`joker: WeeJoker`, `antes: [1]`, …), distinct
    from motely-wasm's `JamlConfig` which is the *parsed* packed output. Mirrors
    `Motely/Filters/Jaml/JamlConfigLoader.Models.cs`.
  - `src/service.ts` (788 lines) — **editor-agnostic language service**, already
    LSP-shaped (0-based `Position`/`Range`, `Severity`, `Diagnostic`):
    - `getDiagnostics(text)` — YAML syntax + Zod structural issues
    - `getCompletions(text, offset)` — context-aware keys + enum values
    - `getHover(text, offset)`
    - `getDocumentSymbols(text)` — must/should/mustNot outline
    - `mergeDiagnostics(...)` — fold in the authoritative WASM `parseJaml` errors
  - `src/vocab.generated.ts` — completion vocab, generated from C# enums.
  - `codegen/gen-vocab.mjs` — the generator (`npm run gen`). **This is the
    anti-drift mechanism** — vocab/enums regenerate from the C# source of truth.
  - `index.ts` names the three intended consumers verbatim: jaml-ui, the LSP,
    the MCP app.
- **The semantic authority** stays the C# engine: `Program.parseJaml` (exposed
  through motely-wasm). jaml-lang is the *fast structural front gate*; parseJaml
  is the *final word*. The service already has `mergeDiagnostics` for exactly
  this two-layer merge.
- **The dead VSIX** had: `syntaxes/jaml.tmLanguage.json` + `jummy.tmLanguage.json`
  (TextMate grammars), `language-configuration.json`, `snippets/`, a frozen
  `schema/jaml.schema.json`, and a bundled `vendor/jaml-lsp-server`. Its source
  is **not in this repo** (the old `packages/jaml-language-support` directory
  doesn't exist locally — only `jaml-lang/` survives). Treat the VSIX as a
  reference corpse: salvage the TextMate grammar + snippets, **throw away its
  schema and vendored server.**
- **jaml-ui does NOT consume `jaml-lang` yet.** It still uses its own
  hand-rolled `src/lib/jaml/jamlParser.ts` (regex), `jamlSchema.ts` (hand-listed
  enums), `jamlCompletion.ts` (substring match). These are the things to retire.

## The drift problem (why the old schema was "booty ass")

Three independent JAML schemas exist right now and none of them agree:
1. C# `JamlConfigLoader.Models.cs` — the real one.
2. jaml-ui's `jamlSchema.ts` — hand-maintained copy.
3. The dead VSIX's `jaml.schema.json` — frozen copy, most drifted.

`jaml-lang` exists to collapse 2 and 3 into one generated artifact fed
by 1. The whole point of the rebuild is: **only `gen-vocab.mjs` is allowed to
know the enum values; everything downstream imports them.**

## Target architecture

```
            Motely C# enums + JamlConfigLoader.Models.cs   (source of truth)
                                  │  codegen/gen-vocab.mjs
                                  ▼
                  jaml-lang  (Zod schema + vocab + service)
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        ▼                         ▼                          ▼
   jaml-ui editor          VS Code LSP server          MCP validate_jaml
   (CodeMirror adapter)    (vscode-languageserver        (JamlConfigSchema
   calls service fns)       adapts service fns)           .parse())
                                  │
                                  ▼  (layer-2, where WASM is available)
                    motely-wasm Program.parseJaml  →  mergeDiagnostics
```

Layer 1 (jaml-lang service) runs everywhere — synchronously, no WASM. Layer 2
(parseJaml) merges in on surfaces that can load the engine (LSP server in node,
a web worker in jaml-ui). Both speak the same `Diagnostic` shape.

## Work plan (tomorrow)

Rough order; each is independently shippable. **Nothing deploys without an
explicit go.**

1. **Publish-readiness of `jaml-lang` (local only).** Bump off `0.0.0`,
   run `npm run gen` then `npm run check` (gen + `tsc --noEmit`) and `npm run
   smoke` (`smoke.ts`). Decide distribution: published npm pkg vs. `file:`/
   workspace link from jaml-ui. Leaning workspace link first (no publish churn).
2. **Make jaml-ui consume it.** Replace `jamlParser.ts` / `jamlSchema.ts` /
   `jamlCompletion.ts` with calls into `jaml-lang`. Write the thin
   CodeMirror adapter that maps `getDiagnostics/getCompletions/getHover` →
   CM `linter` / `autocompletion` / `hoverTooltip`. Keep the YAML base grammar
   for tokens; layer JAML semantics on top.
3. **Layer-2 merge in jaml-ui.** Run `Program.parseJaml` in a worker, feed its
   errors through `mergeDiagnostics` so the editor shows engine-true validation,
   not just structural.
4. **Rebuild the VS Code extension fresh** (new `packages/jaml-language-support`,
   not the old VSIX): salvage `jaml.tmLanguage.json` + snippets + language-config
   from the corpse, but the LSP server `import`s `jaml-lang/service` and
   the schema is **deleted** (no `schema/jaml.schema.json`). Optionally bundle
   parseJaml for layer-2 in node.
5. **Point the MCP `validate_jaml` tool at `JamlConfigSchema.parse()`** so the
   tool, the editor, and the LSP literally cannot disagree.

## Salvage / discard checklist (from the dead VSIX)

| Artifact | Verdict |
|---|---|
| `syntaxes/jaml.tmLanguage.json`, `jummy.tmLanguage.json` | **Salvage** — TextMate grammar is still useful for VS Code token coloring. |
| `snippets/jaml.code-snippets` | **Salvage** — re-check against current keywords. |
| `language-configuration.json` | **Salvage** — comments/brackets/indent rules. |
| `schema/jaml.schema.json` | **Discard** — this is the drift. Regenerate from jaml-lang if a JSON-Schema is ever needed (`z.toJSONSchema()`). |
| `vendor/jaml-lsp-server/` | **Discard** — replace with a server that imports `jaml-lang/service`. |
| `vendor/jaml-language-core/` | **Discard** — superseded by `jaml-lang`. |

## Open questions for pifreak

- Distribution for `jaml-lang`: publish to npm, or keep it a workspace/
  `file:` dep so it never needs a release cadence? (Affects whether jaml-ui's
  consumption is `link:` or a real version range.)
- Do we still want the **`jummy`** language id alongside `jaml`, or drop it?
- Is the VS Code extension a priority at all, or is the **web editor in jaml-ui**
  the only surface that matters right now? (If web-only, steps 1–3 are the whole
  job and step 4 waits.)

## Hard constraints (don't forget)

- **No deploy / no publish** without explicit sign-off — not the engine v20, not
  jaml-lang, not the extension.
- One schema. If you find yourself hand-typing an enum value anywhere but
  `gen-vocab.mjs`'s output, stop — that's how we got the old booty-ass schema.

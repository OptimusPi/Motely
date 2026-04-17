# MotelyJAML — agent contract

## Source of truth

- **Engine:** C# in `Motely/`, `net10.0` across the whole solution. `Motely.Wasm` adds `<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>` — there is **no** separate `net10.0-browser` target framework.
- **WASM interop:** NativeAOT-LLVM + Bootsharp. The exported contract is `Motely.IMotelyWasm` (one `[JSExport]` interface). Implementation: `Motely.MotelyWasmHost`. Bootstrap: `Motely.Wasm/Program.cs`.
- **Version:** `<MotelyVersion>` in `Directory.Packages.props` drives all C# assembly versions, is injected into `Motely/` via the `GenerateVersionInfo` MSBuild target (→ `VersionInfo.Version`), and is stamped into `motely-wasm/package.json` by `Motely.Wasm`'s `PatchPackageJsonVersion` target on `dotnet publish`.
- **JAML schema:** Generated — do not hand-edit `jaml.schema.json`; regenerate via `dotnet run --project Motely.CLI -- --write-jaml-schema`.
- **Threading:** Every `IMotelyWasm` search path calls `.WithThreadCount(1)`. Browser WASM is single-threaded; thread count is not a parameter.

## Consumer API reports (mandatory)

- **Do not dismiss, “protect,” or reframe** external consumer feedback about `motely-wasm`, Bootsharp interop, or missing/awkward TypeScript types **without presenting that feedback to the maintainer** (verbatim or clearly attributed summary). Relay first; investigate against C# and generated `motely-wasm/types/bindings.g.d.ts` second.

## Bootsharp (mandatory local reference)

Before touching any WASM interop code, read the **local Bootsharp clone**:

- `D:\bootsharp\docs\` — full guide (interop interfaces, interop instances, LLVM, build config, serialization, nullability, namespaces, emit prefs, events, sideloading, declarations, extensions).
- `D:\bootsharp\samples\` — working reference apps.
- `D:\bootsharp\AGENTS.md` — upstream project guidance.

Non-negotiable rules distilled for MotelyJAML:

1. A `[JSExport]` interface method must **not** call another `[JSExport]` method on `this`; shared logic goes into `private` helpers (see `MotelyWasmHost.ParseJaml`). Violation throws `Invalid Program: attempted to call a UnmanagedCallersOnly method from managed code.`
2. `IMotelyWasmSearch` and `IMotelyWasmSearchContext` are **interop instance bindings**. They cannot be args/returns of another instance method and cannot be args of events.
3. Enums cross the boundary as numbers + auto-generated name maps. Do **not** hand-author TS enums that shadow `bindings.g.d.ts`.
4. Nullability convention: nullable args → `| undefined`, nullable returns → `| null` (Bootsharp emits this automatically).
5. Publish pipeline requires **`wasm-opt`** (Binaryen) on `PATH`; without it Bootsharp aborts with MSBuild error 9009. The final bundle step is `npx rollup …` — resolve it from the workspace (`rollup` is in root `devDependencies`).

## JAML

**JAML — Jimbo's Ante Markup Language** — is pifreak's language for Balatro seed filters. **Maintainer is authoritative** for semantics and naming; when behavior is ambiguous, ask rather than inventing new doctrine in docs.

## Repo map (quick)

| Area | Path |
|------|------|
| Core engine | `Motely/` |
| WASM / npm | `Motely.Wasm/`, `motely-wasm/` |
| CLI | `Motely.CLI/` |
| JAML LSP / VS Code extension / Monaco / JSON-Schema package | `tools/jaml-language/` |
| Example filters | `JamlFilters/` |

## Out of scope (moved to other repos)

- **MCP server + MCP Apps UI** → `seedfinder.app` (deployed at `https://mcp.seedfinder.app/mcp`). Do **not** treat `tools/balatro-seed-finder/` as an active subproject — it's a migration stub that just redirects.
- **`jaml-ui`** (npm) → separate repo. React/UI for JAML; depends on `motely-wasm` like any other package.
- **`jimbo-ui`** (npm) → its own repo. Balatro-themed visual components (cards, sprites, aesthetics) that pair with `jaml-ui`.

## Rule: use the published packages, don't re-implement

Downstream apps (`seedfinder.app`, anything else) **must consume `jaml-ui` + `jimbo-ui` + `motely-wasm` directly** instead of hand-rolling Motely/JAML display logic, filter components, or YAML parsing. If something appears "missing" from `motely-wasm` and you're tempted to regenerate enums or format display names on the TS side, that is the signal to **file an issue asking for a new `IMotelyWasm` export** — not to work around it in app code.

## Release touchpoints when bumping `MotelyVersion`

1. Bump `<MotelyVersion>` in `Directory.Packages.props`, then `dotnet publish Motely.Wasm -c Release` (needs `wasm-opt` on `PATH`).
2. Publish `motely-wasm` to npm when you want the rest of the world on that build.
3. **Consumers (your other repos, same as any npm deps):** from the app root, run **`pnpm update motely-wasm jaml-ui jimbo-ui`** (add packages you use). Commit the lockfile. Done.
4. In **this** repo: `pnpm install` at root if the lockfile needs refresh; `Motely.TestWebsite` uses `workspace:*` for local `motely-wasm`. VS Code extension lives under `tools/jaml-language/vscode-extension/` (not in the pnpm workspace) — bump its `motely-wasm` range there only if the extension needs a new engine API.
5. Optional CDN mirror for script-tag users: deploy from `seedfinder.app` infra if you use it.

## Non-negotiable repo conventions

- **JAML is YAML.** It round-trips to JSON 1:1. Parse it with the `yaml` npm package on the JS side, or call `MotelyWasm.validateJaml` / `MotelyWasm.parseJamlContext`. **Never regex-parse JAML.** If an agent starts writing a regex "extractor" for JAML keys, stop it.
- **JAML is basically Markdown-simple** — stop treating it like an exotic DSL. It's indentation + `key: value`. The VS Code extension + Red Hat YAML + `jaml.schema.json` give full editor support for free.
- Keep display-name logic and JAML parsing **inside `Motely`** (expose via `IMotelyWasm`). Use **`jaml-ui` / `jimbo-ui`** from npm in apps; do not duplicate Motely behavior in app code.
- Do not add a `Motely/package.json` shim. The `motely-wasm` npm package is emitted from `Motely.Wasm` publish only.
- One package, "compat" shape: single embedded-binaries `index.mjs`. Do not split into `motely-wasm` + `motely-wasm-compat` — that's the pattern we explicitly walked back from.

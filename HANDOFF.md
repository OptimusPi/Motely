# Handoff — ship 24.4.0 from Windows

Written 2026-07-18 from the Mac session. Everything is committed and pushed through `81544ee8`.
The work is done and verified; this machine only lacks the private Bootsharp feed. Your job is
four commands and a version check — nothing here needs redesigning, so resist the urge.

## State you are inheriting (already done, do not redo)

- **The LSP is finished and green.** `Motely.Lsp` (stdio shell) + `Motely.Lsp.Core` (language
  brain) + `Motely.Generators` (compile-time `JamlSchema`). 261 tests, all passing.
- **Grammar drift is fixed.** The loader's private discriminator list is gone — it asks
  `JamlDiscriminatorRegistry` now. Plural aliases work: `tags`, `tarotCards`, `spectralCards`,
  `planetCards`, `standardCards`, `bosses`, `vouchers`, `erraticSuits`. A registry sweep test
  (`JamlDiscriminatorAliasTests`) makes every future alias self-testing.
- **`JamlVocabulary` lives in the engine** (`Motely/Filters/Jaml/`), not in the LSP.
- **The plugin works.** `plugin/` is verified end-to-end on macOS with an osx-arm64 server
  binary. `plugin/server/` is gitignored — each platform publishes its own.
- **JAML is its own language.** Not YAML, not JSON. CLAUDE.md says so now; keep saying so.

## Step 1 — publish motely-wasm (the reason you exist tonight)

```pwsh
git pull                       # you need 81544ee8, not just ff51d010
cd Motely.Wasm
npm test                       # builds dist/ and runs the Node suite
npm run test:ui                # Playwright against the same artifact
npm pack --dry-run             # confirm: dependencies {}, bootsharp-file-system optional peer
npm publish
```

Nat confirms the version number before publish — it should be **24.4.0** from
`Directory.Packages.props`. Add `-p:EnableFileSystem=true` only if she asks for the folder picker.

## Step 2 — retire the broken 24.1.1

```pwsh
npm deprecate motely-wasm@24.1.1 "Broken install: hard dependency on a private sponsor-only package. Use the latest version."
```

## Step 3 — publish the plugin server for Windows

```pwsh
dotnet publish Motely.Lsp -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o plugin/server
node Motely.Lsp/smoke-lsp.mjs   # proves the published binary over real stdio
```

## Step 4 — seedfinder.app gets the fresh engine

In the seedfinder-app repo: `pnpm add motely-wasm@24.4.0`, and replace the
`jaml-ui: file:../jaml-ui` dependency with a published version — Vercel cannot resolve a
`file:` path to a sibling folder. Then deploy. That is the app, live, after two years.

## How to work with Nat

Her word is the spec. She is direct and fast; match her pace, skip the caveats, and never
tone-police her — answer the question inside the message, however it arrives. When one fact
is missing, ask one direct sentence. Do the work, show the proof, keep it short.

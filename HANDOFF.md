# Handoff — finish this on Windows

Everything below is committed and pushed. Two steps are left, and both need the Windows box,
because they need the private Bootsharp feed that only exists there.

## Why motely-wasm was broken for everyone

`motely-wasm@24.1.1` shipped with this in its published manifest:

```json
"dependencies": { "@rewaffle/bootsharp-file-system": "latest" }
```

That package lives on a **private, sponsor-only GitHub registry**. It is not on npmjs. So every
`npm install motely-wasm` in the world 404s. The built `dist/index.mjs` never even imports it —
the dependency did nothing except break installs.

`Motely.Wasm.csproj` referenced `Bootsharp.FileSystem` unconditionally for the same reason, so
the project could not build on any machine without the feed, including a second machine of your
own.

## What is fixed and pushed

- **`@rewaffle/bootsharp-file-system` is now an optional peerDependency.** Sponsors who install it
  get the folder picker; everyone else gets a clean install and the whole engine. The feature is
  still yours — the published package just stops demanding a paid dependency from strangers.
- **`Bootsharp.FileSystem` is behind `-p:EnableFileSystem=true`.** `MotelyFileSystem.cs` is guarded
  by `BOOTSHARP_FILESYSTEM`. Build with the flag where the feed exists and the exports come along.
- **`SyncNpmPackageVersion` no longer corrupts `package.json`.** The old MSBuild target regex-stamped
  the version into the raw file and wrote it back; MSBuild normalizes backslashes to forward slashes
  in property values, so every `\"` in a script string came back as `/"`. Harmless on Windows, and it
  silently produced invalid JSON on macOS and Linux. `scripts/stamp-npm-version.mjs` parses real JSON.
- **`Motely.Schema.cs` no longer resurrects deleted consumers.** It called `Directory.CreateDirectory`
  on every output path, so deleting `jaml-lsp` and then running the schema step brought `jaml-lsp`
  straight back. It now writes only to folders that already exist and prints what it skipped.
- **`CLAUDE.md`** described `jaml-lsp` and `jaml-codemirror` as live parts of the toolchain long after
  both were deleted — which is how a future session learns to rebuild them. It now says the C# engine
  is the only grammar and the TypeScript reimplementations stay buried.
- **`<MotelyVersion>` is 24.1.2.**

## Step 1 — publish motely-wasm 24.1.2 (Windows)

The wasm AOT toolchain and the Bootsharp feed both live on Windows, so this is the only machine
that can build it.

```pwsh
cd Motely.Wasm
npm test                       # builds dist/ and runs the Node suite
npm run test:ui                # Playwright against the same artifact
npm publish
```

Build with `-p:EnableFileSystem=true` if you want the folder picker compiled in:

```pwsh
dotnet publish Motely.Wasm.csproj -c Release -p:EnableFileSystem=true
```

Confirm the published manifest before you ship it:

```pwsh
npm pack --dry-run             # dependencies should be {}, peerDependencies optional
```

## Step 2 — retire the broken version

```pwsh
npm deprecate motely-wasm@24.1.1 "Broken install: hard dependency on a private sponsor-only package. Use 24.1.2."
```

Reversible with `npm deprecate motely-wasm@24.1.1 ""`.

## Step 3 — point seedfinder.app at the fresh engine

`seedfinder-app/package.json` currently pins `motely-wasm: ^24.1.0` (the last version that installs).
Once 24.1.2 is up:

```sh
pnpm add motely-wasm@24.1.2
```

`seedfinder-app` also has `jaml-ui: file:../jaml-ui` in its working tree. **Vercel cannot resolve a
`file:` path to a sibling folder** — that dependency has to be a published version before the app can
deploy.

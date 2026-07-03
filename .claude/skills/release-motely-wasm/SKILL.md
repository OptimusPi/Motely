---
name: release-motely-wasm
description: Ship a real motely-wasm release to npm — version bump, green suites, publish, tag. The one true release ritual.
disable-model-invocation: true
---

# Release motely-wasm

This is the complete, real release sequence. Every step below is the whole story — follow it in order and the release lands clean.

## 1. One number rules them all

Bump `<MotelyVersion>` in `Directory.Packages.props` at the repo root. A build target (`SyncNpmPackageVersion` in `Motely.Wasm/Motely.Wasm.csproj`) stamps `Motely.Wasm/package.json` from it automatically, so the props file is the single place the version lives. pifreak confirms the number.

## 2. Prove it green

From `Motely.Wasm/`:

```sh
npm test          # dotnet publish -c Release into dist/, then the Node suite against dist/index.mjs
npm run test:ui   # Playwright in real Chromium against the same artifact
```

From the repo root:

```sh
dotnet test       # the C# suite
```

All three green means the artifact in `dist/` is the artifact you ship — `npm test`'s pretest already built it.

## 3. Commit and tag

Commit the working tree so the release has a real anchor, then tag with the bare version:

```sh
git tag <version>        # e.g. git tag 23.3.0 — matches <MotelyVersion> exactly
```

## 4. Publish

From `Motely.Wasm/`:

```sh
npm run pack:check   # eyeball the tarball contents and version one last time
npm publish
git push && git push --tags
```

`npm publish` reaches the real registry — pifreak says go before this step runs.

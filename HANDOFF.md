# EXECUTABLE AGENT HANDOFF

Read `AGENTS.md` for repo rules. This file is only the verified next-step playbook.

## What this repo is

**JAML — Jimbo's Ante Markup Language** + the Motely engine.  
This repo's publishable JS artifact is **`motely-wasm`**.

## Out of scope

- `jaml-ui`, `jimbo-ui` → separate npm repos
- VS Code extension / LSP / Monaco / grammar → `D:\jaml-vsx`
- MCP App → `seedfinder.app` / `mcp.seedfinder.app`

Do not resurrect any of that here.

## Verified current state

These are already true in the worktree:

- `Motely.TestWebsite/` is deleted
- `tools/jaml-language/` is deleted
- `tools/balatro-seed-finder/` is deleted
- `pnpm-workspace.yaml` is deleted
- root `package.json` is reduced to:
  - private package
  - `rollup` devDependency only
- `Directory.Packages.props` is already bumped to `11.3.2`
- `Motely/MotelyWasmHost.cs` exists and matches the current `IMotelyWasm` surface
- `dotnet build Motely.Wasm -c Release` succeeds
- `dotnet publish Motely.Wasm -c Release` succeeds when Binaryen is visible to this shell
- `motely-wasm/package.json` is currently stamped to `11.3.2`
- `npm view motely-wasm version` still returns `11.3.1`

## The exact publish command that worked

Run this from repo root:

```powershell
$env:PATH = "X:\binaryen-version_128;X:\binaryen-version_128\bin;" + $env:PATH
dotnet publish Motely.Wasm -c Release
```

What this does:

- produces `motely-wasm/index.mjs`
- produces generated types under `motely-wasm/types/`
- rewrites `motely-wasm/package.json` to version `11.3.2`

## Important: what is NOT the current blocker

Do **not** waste time on:

- `Motely.TestWebsite`
- `tools/jaml-language`
- `tools/balatro-seed-finder`
- workspace linking
- PATH arguments with the user

The actual Bootsharp build is currently working.

## Rollup note

`dotnet publish Motely.Wasm -c Release` emits Rollup warnings like unresolved `fs`, `fs/promises`, and `url`.

This did **not** stop publish.

Bootsharp still completed and wrote:

- `motely-wasm/index.mjs`
- `motely-wasm/package.json`

So treat the current task as **publish + consumer update**, not "fix Rollup first" unless runtime testing later proves the generated bundle is broken in browser usage.

## Next actions — do in this order

1. Publish the npm package:

```powershell
cd X:\JammySeedFinder\src\MotelyJAML\motely-wasm
npm publish
```

Expected result:

- npm publishes `motely-wasm@11.3.2`

2. Verify publish:

```powershell
npm view motely-wasm version
```

Expected result:

- `11.3.2`

3. Update consumers using normal npm flow:

```powershell
cd X:\jaml-ui
pnpm update motely-wasm

cd X:\jimbo-ui
pnpm update motely-wasm

cd D:\jaml-vsx
pnpm update motely-wasm
```

Also update `seedfinder.app` in its own repo/workspace the same way:

```powershell
pnpm update motely-wasm
```

Then commit each lockfile in each repo separately.

## MCP App fact check

Live endpoint:

- `https://mcp.seedfinder.app/mcp`

Observed metadata during this session:

- name: `balatro-seed-finder`
- version: `11.2.0`
- transport: `streamable-http`

This means the MCP App is behind the current engine/package and should be updated from the `seedfinder.app` repo after `motely-wasm@11.3.2` is published.

## npm/package positioning already changed

Current generated `motely-wasm` description is intended to sell the real user outcome:

> Find Balatro seeds with plain-language JAML filters — Jimbo's Ante Markup Language. SIMD-vectorized seed search (Motely engine, C#). Curate runs, share filters, search millions of seeds. Browser + Node.

Keywords include:

- `balatro`
- `seed`
- `seed-finder`
- `seed-curator`
- `jaml`
- `jummy`
- `filter`
- `simd`
- `wasm`
- `webassembly`
- `motely`

## CDN demo

`motely-wasm/demo.html` exists now.

It is:

- a single HTML file
- CDN-import based
- intended to prove the package works without bundlers

Natural home:

- `demo.seedfinder.app` on Vercel

## Things the previous agent got wrong

Do not repeat these mistakes:

1. Do not tell the user the problem is PATH when the user says it is not.
2. Do not target deleted projects.
3. Do not invent fluent interop shapes that violate Bootsharp instance-binding rules.
4. Do not turn package descriptions into toolchain essays.
5. Do not say "browser only" when the package is also useful from Node-compatible runtimes.

## One-line summary for the next agent

`motely-wasm` is built and ready at `11.3.2`; publish it to npm, then run `pnpm update motely-wasm` in the consumer repos and redeploy `seedfinder.app`.

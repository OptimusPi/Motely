---
name: release-motely-wasm
description: >
  Build, REAL-browser-boot-test, and publish motely-wasm — deterministically,
  with no softening and no refuse-at-the-pew thrash. TRIGGER when the user asks to
  release / publish / ship motely-wasm, bump its version, or debug its boot
  (`addRunDependency`, embedded vs sideloaded, Bootsharp version). Encodes the
  hard-won process so it is not rediscovered from scratch every time.
---

# release-motely-wasm

The point of this skill: **stop the 12-day loop** where Claude softens, thrashes
embedded↔sideloaded, fakes "it works," refuses at the final step, or ships
untested. There is exactly ONE legitimate halt — a *reproduced, real-browser*
boot failure, reported honestly. Everything else is: build, test by running,
ship when green. No menus, no advisor-stall, no "5 minute fix," no
ship-without-the-boot-test.

## The behavioral rule (read first)

- **Verify by running.** "Builds" ≠ "boots" ≠ "works." The running artifact in a
  **real browser** outranks every claim — yours and the user's.
- **Never publish on a red or untested boot.** That is the shame this skill exists
  to end. A `<script>` tag that throws on `boot()` is worse than no release.
- **Do not soften a failure into "done."** If it fails, say so with the error.
- **Do not balk on a *green* test.** When it boots, ship it — don't invent reasons
  to wait. Refusing at the pew when the gun isn't jammed IS the sycophant move.
- **Read source, not stale docs.** `bootsharp.com` docs lag the local alpha; the
  truth is in `d:/bootsharp` source.

## Facts (verified 2026-06-09 — update if the engine changes)

- **Embed switch = `BootsharpBinariesDirectory`** (NOT `BootsharpEmbedBinaries`,
  which does not exist in the current alpha). In `Motely.Wasm.csproj`:
  - **empty / unset → embedded** base64 single-file (what a CDN `<script>` wants)
  - **set → sideloaded** (separate `.wasm` in that dir)
  - source: `<BsEmbed>$([System.String]::IsNullOrEmpty('$(BootsharpBinariesDirectory)'))</BsEmbed>`
- **Local Bootsharp alpha feeds** (user `%APPDATA%/NuGet/NuGet.Config`):
  `D:\bootsharp\src\cs\.nuget` and `D:\extra\bootsharp\cs\.nuget` (FileSystem).
  `0.8.1-alpha.3` etc. are elringus's via `d:/bootsharp` — not ghosts. Do NOT
  "fix" by downgrading to a known-broken published version (e.g. 0.8.0).
- **Bootsharp version = MinVer** (git tag + height). Rebuild the alpha by
  `dotnet pack` Common → Inject → Bootsharp (meta needs Common in the feed first)
  into the feed; pack FileSystem from `d:/extra` into its feed; then bump
  `Directory.Packages.props`.
- **LLVM is AUTOMATIC in Release** (verified in installed package source, not docs):
  `BsLlvm = !$(BsDebug)` in `Bootsharp.props` → true for `-c Release`; `Bootsharp.targets`
  imports the ILC bundled at `$(BsLlvmDir)=$(BsRoot)/llvm`. **`BootsharpLLVM` is NOT a
  real property; do NOT add an experimental-feed `Microsoft.DotNet.ILCompiler.LLVM`
  PackageReference (collides with the bundled one); do NOT touch `global.json`.** The
  csproj needs ONLY the Bootsharp/Inject/FileSystem/DI `PackageReference`s.
- **`addRunDependency` boot crash = a Mono dist = the build was Debug** (`BsLlvm` false).
  Fix is to publish `-c Release`, not to hand-wire LLVM. After publishing, GREP the log:
  resolved `...Runtime.Mono.browser-wasm` → still Mono (wrong loader, do not boot/ship,
  investigate emscripten env / workload). Ran ILC + emcc → NativeAOT-LLVM (right path).

## Paths (do not confuse the npm name with the directory)

The npm **package name** is `motely-wasm`. The **directory** it lives in is
`Motely.Wasm/` — there is NO `motely-wasm/` folder. So: `Motely.Wasm/package.json`
is the manifest, `npm publish` runs from `Motely.Wasm/`, the build module lands in
`Motely.Wasm/dist/`, the test suite is `Motely.Wasm/tests/`. Earlier versions of
this skill said `motely-wasm/...` everywhere — that path does not exist; don't chase it.

## Steps

1. **Version.** Bump `Motely.Wasm/package.json` (hand-maintained, NOT
   `obj/package.json` — Bootsharp clobbers that one) and `MotelyVersion` in
   `Directory.Packages.props` if releasing the engine too.
2. **Build.** Clean stale outputs: delete `Motely.Wasm/dist`, `Motely.Wasm/obj`,
   `Motely/bin/Release/net10.0/browser-wasm`. Then: `dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release`
   (Release auto-enables NativeAOT-LLVM). Module lands in `Motely.Wasm/dist`.
   Confirm embed mode: `dist/generated/resources.g.mjs` ~12MB = embedded base64, ~254B = sideloaded manifest.
3. **Packaging gate (what npm actually ships).** `package.json` MUST have
   `"files": ["dist"]`. Without it (no `.npmignore` either) npm ships the WHOLE
   project — `bin/Debug`, `bin/Release`, `*.cs`, PDBs, a redundant sideloaded
   `dotnet.native.wasm` — ~31MB / 276 files of garbage (this bloat shipped through
   20.5.0). Verify before publishing: `npm pack --dry-run` should report ~3.9MB /
   ~70 files, and `npm pack --dry-run 2>&1 | grep notice | grep -v 'dist/'` should
   show only `package.json`. The embedded wasm rides inside `dist/generated/resources.g.mjs`,
   so dropping `bin/`'s separate `.wasm` is correct, not lossy.
4. **REAL boot test (the gate).** `npm run test:browser` from `Motely.Wasm/` — it
   serves the dir over http and drives a real browser (Playwright `chromium`
   channel `msedge`/`chrome`) against `tests/browser-boot-test.html`: imports
   `dist/index.mjs` + `dist/generated/modules/motely/wasm.g.mjs`, `await
   bootsharp.boot()` (embedded) or `boot("/dist/bin")` (sideloaded), calls
   `Program.fromJaml` / `Program.fromJson` / `Program.jamlToJson` / `Program.runSeedListSearch`
   (verified exports in `Program.cs`; `parseJaml` does NOT exist). Pass = `BROWSER
   BOOT: PASS` + `window.__RESULT.ok`. `npm test` runs the node suite (54 tests)
   over the same dist; both must be green.
5. **Branch on the artifact.**
   - **Boots + JAML round-trips** → `npm publish` from `Motely.Wasm/` (its
     `prepublishOnly` rebuilds dist from source first), then run **post-publish
     verification** (below). Ship.
   - **Boot fails** → STOP. Report the exact error + which modes you tried. Do
     NOT publish. Pursue the fix (step 6), do not fake or soften.
6. **If boot is red — fix path (do NOT just flip embed/sideload; both fail the
   same).** Confirm Release is actually on NativeAOT-LLVM vs silently Mono;
   pin runtime / emscripten / ILC versions to match a working Bootsharp
   browser-wasm sample (`d:/bootsharp/samples/minimal` or `samples/bench`).
   Re-run step 4. Repeat until it boots, then step 5.

## Verification (post-publish — the real backstop)

`prepublishOnly` rebuilds dist during publish, so the registry artifact is NOT
byte-identical to whatever you boot-tested in-tree. Verify the **published bytes**:

1. `npm view motely-wasm@<version> version` returns the new version.
2. Boot the published module. Install it clean and run the existing node suite
   against it — `harness.mjs` honors `MOTELY_WASM_ENTRY`, so no throwaway code:
   ```
   cd $TMP && npm init -y && npm install motely-wasm@<version>
   MOTELY_WASM_ENTRY=$TMP/node_modules/motely-wasm/dist/index.mjs \
     node --test "Motely.Wasm/tests/*.test.mjs"
   ```
   A real-browser boot of the rebuilt in-tree dist (`npm run test:browser` after
   publish) covers the browser path, since publish regenerated dist in place.

A release is only "done" when both are true. Say "I published X and verified it
boots" only then; otherwise say exactly what's red.

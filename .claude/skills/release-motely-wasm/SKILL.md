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

## Steps

1. **Version.** Bump `motely-wasm/package.json` (hand-maintained, NOT
   `obj/package.json` — Bootsharp clobbers that one) and `MotelyVersion` in
   `Directory.Packages.props` if releasing the engine too.
2. **Build.** `dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release`
   (Release auto-enables NativeAOT-LLVM). Module lands in `motely-wasm/dist`.
   Confirm embed mode: `resources.g.mjs` ~12MB = embedded, ~254B = sideloaded.
3. **REAL boot test (the gate).** Serve `motely-wasm/` over http and drive a real
   browser (Playwright `chromium` channel `msedge`/`chrome`). Harness: import
   `dist/index.mjs` + `dist/generated/modules/motely/wasm.g.mjs`, `await
   bootsharp.boot()` (embedded) or `boot("/dist/bin")` (sideloaded), then call
   `Program.jamlToJson(...)` / `Program.parseJaml(...)`. Assert success via
   `window.__RESULT`. (See the harness used in the 2026-06-09 handoff.)
4. **Branch on the artifact.**
   - **Boots + JAML round-trips** → bump done, `npm publish` from `motely-wasm/`,
     then **verify on the registry** (`npm view motely-wasm@<ver> version`). Ship.
   - **Boot fails** → STOP. Report the exact error + which modes you tried. Do
     NOT publish. Pursue the fix (step 5), do not fake or soften.
5. **If boot is red — fix path (do NOT just flip embed/sideload; both fail the
   same).** Confirm Release is actually on NativeAOT-LLVM vs silently Mono;
   pin runtime / emscripten / ILC versions to match a working Bootsharp
   browser-wasm sample (`d:/bootsharp/samples/minimal` or `samples/bench`).
   Re-run step 3. Repeat until it boots, then step 4.

## Verification

A release is only "done" when `npm view motely-wasm@<version> version` returns the
new version AND a real browser booted the published module and round-tripped a
JAML string. Nothing less counts. Say "I published X and verified it boots" only
when both are true; otherwise say exactly what's red.

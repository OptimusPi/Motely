# motely-wasm 9.0.1 — handoff for pifreak / Nat

This branch (`claude/debug-pifreak-critical-BVNEc`) ships the fix for the
`UnmanagedCallersOnly` crash that broke `motely-wasm@9.0.0` on npm.

## What landed (5 commits on this branch)

1. **`5976ca6` fix(wasm): stop `[JSExport]` methods from invoking other `[JSExport]` methods on `this`**
   - Adds `private static JamlConfig LoadJamlCore(string)` to `MotelyWasmHost.cs`.
   - `LoadJaml(string)` now delegates to `LoadJamlCore`.
   - `StartConfiguredSearchFromJaml`, `StartRandomSearchFromJaml`, `StartSeedListSearchFromJaml` now call `LoadJamlCore` directly (instead of `this.LoadJaml(jaml) → this.StartXxxSearch(...)`). Result: no `[JSExport]` interface member ever calls another `[JSExport]` interface member on `this` from managed code.
   - Restores the explicit `^Motely\.Analysis\.(\S+)` regex in `BootsharpInterop.cs` JSPreferences (per pifreak's recall — was working ~20 days ago).
   - De-legacys `Motely.BrowserWasm/README-WASM.md`. Documents the no-`[JSExport]`-on-`[JSExport]` rule so this bug doesn't return.

2. **`7d1a737` chore(versions): align all version surfaces to 9.0.1**
   - `Directory.Packages.props` `<MotelyVersion>` 8.0.0 → 9.0.1.
   - `.github/workflows/pages.yml` `MOTELY_VERSION` 9.0.0 → 9.0.1.
   - `tools/jaml-mcp/package.json` 8.0.0 → 9.0.1; depends on `motely-wasm-compat ^9.0.1`; drops unused `yaml` dep; adds `vitest` devDep + `test`/`test:watch` scripts.
   - `tools/jaml-mcp/public/.well-known/mcp/server-card.json` and `public/index.html` badge → 9.0.1.

3. **`51604ee` refactor(jaml-mcp): strip 500+ lines of JS glue**
   - `tools/jaml-mcp/api/tools.ts`: 814 → 306 lines.
   - Removed: `SPECIAL_DISPLAY`, `ROOT_KEY_LOOKUP`, `CLAUSE_KEY_LOOKUP`, `VALUE_LOOKUPS`, all `canonicalize*` / `normalize*` helpers, `compileJummyFallback` (the duplicate Jummy parser), `MotelyWasmHostExtended` cast, the `jummy` MCP input parameter, `MCP_LITE` mode in `api/server.ts`. The engine owns these semantics; let it.
   - `cli.ts` and `api/server.ts` now read `name` + `version` from `package.json` via `createRequire(import.meta.url)`. No more hardcoded version strings.
   - `analyzeSeed` return type derived from `ReturnType<typeof MotelyWasmHost.motelySingleSearchContext>` so it compiles regardless of whether the generated TS exposes the type under `Analysis.X` (pre-9.0.1) or top-level `X` (9.0.1+).

4. **`6599d97` test(jaml-mcp): add vitest suite**
   - `tests/motely.test.ts` (118 lines): boot smoke, JAML parse, real search via `startRandomSearchFromJaml`, host-level single-seed inspection.
   - Run: `cd tools/jaml-mcp && pnpm test`.

5. **`39710a3` test(jaml-mcp): revert search test to use `session.waitForCompletionAsync`**
   - Tests the API as Tacodiva designed it. If this still crashes after the C# fix, that's a deeper bug to fix in the bridge.

## Run on your Windows PC (where dotnet exists)

This sandbox doesn't have .NET, so I can't build the WASM package or publish to npm here. **Everything is staged on the branch — you just need to run the build/publish pipeline.**

```powershell
# 1. Pull the branch
cd X:\JammySeedFinder\src\MotelyJAML
git fetch origin claude/debug-pifreak-critical-BVNEc
git checkout claude/debug-pifreak-critical-BVNEc

# 2. Build the WASM package (this is what produces the npm artifact)
dotnet publish Motely.BrowserWasm\Motely.BrowserWasm.csproj -c Release `
  -p:MSBuildEnableWorkloadResolver=false `
  -p:MotelyVersion=9.0.1

# 3. Verify the published artifact has 9.0.1
Get-Content Motely.BrowserWasm\motely-wasm\package.json | Select-String version
Get-Content Motely.BrowserWasm\motely-wasm-compat\package.json | Select-String version

# 4. Run the vitest suite against the local build (this is the moment of truth)
cd tools\jaml-mcp
pnpm install
pnpm test
# All 5 tests must pass. If they do, the bug is dead.

# 5. Publish both packages
cd ..\..\Motely.BrowserWasm\motely-wasm
npm publish --access public
cd ..\motely-wasm-compat
npm publish --access public

# 6. Deprecate 9.0.0 so nobody installs the broken version
npm deprecate motely-wasm@9.0.0 "Crashes with UnmanagedCallersOnly on startRandomSearchFromJaml; use 9.0.1+"
npm deprecate motely-wasm-compat@9.0.0 "Crashes with UnmanagedCallersOnly on startRandomSearchFromJaml; use 9.0.1+"

# 7. Confirm 9.0.1 is live
npm view motely-wasm@9.0.1 version
npm view motely-wasm-compat@9.0.1 version
```

## If the vitest suite fails after the rebuild

The C# fix targets the most likely cause of the `UnmanagedCallersOnly` crash. If tests still fail, the next most likely culprits — in order of suspicion:

1. **Bootsharp's per-instance handle marshaling** for `IMotelySearchSession` and `IMotelySingleSearchContextImpl`. The host returns a NEW instance from `Start*` / `Open()`, but DI registers a SINGLETON (`IdleMotelySearchSession`, `MotelySingleSearchContextImpl.Placeholder`). When JS calls a method on the returned handle, Bootsharp may dispatch to the singleton.
   - Fix direction: make `MotelyWasmHost` own the search lifetime fully. Change `Start*` to return `void` (or a numeric handle). Add `host.GetCurrentSearchSeedsSearched()`, `host.WaitForCurrentSearchAsync()`, `host.CancelCurrentSearch()`. JS never holds a per-instance handle to a `[JSExport]` interface.

2. **The `MotelySingleSearchContextImpl` "cancer"** pifreak called out: per-ante state hidden inside the impl (`_lastBossAnte`, `_bossBitfield`, `_tagStreams`, etc.). The right shape is what Motely core exposes:
   ```csharp
   var stream = ctx.CreateBoosterPackStream(ante: 1);
   stream.Next();
   ```
   Streams as first-class, per-ante, JS iterates them naturally. This is a real redesign — separate PR after 9.0.1.

3. **JSPreferences namespace shape**. Nat suggested unifying everything to `Motely.*`:
   ```csharp
   [assembly: JSPreferences(
       Space = [
           @"^Motely\.Analysis\.(\S+)", "Motely.$1",
           @"^Motely\.BrowserWasm\.(\S+)", "Motely.$1",
           @"^MotelyJaml\.(\S+)", "Motely.$1"
       ]
   )]
   ```
   This breaks every consumer's import shape, so don't do it on the same release as the bug fix. Schedule it as 9.1.0 after 9.0.1 stabilizes.

## Things I deliberately did NOT touch

- `MotelyWasmHost.StopSearch()` — pre-existing design where the host owns "the current search" and `StopSearch()` cancels it without taking a handle. Pifreak called this the wrong shape (the search handle should own its own cancel). NOT a 9.0.1 fix; redesign with item 1 above.
- The `MotelySingleSearchContextImpl` per-ante state cancer (item 2 above).
- The `lock (_sync)` in `MotelyWasmHost.StartSearch` — single-threaded WASM doesn't need it, but it's harmless and pre-existing.
- Renaming `tools/jaml-mcp` to match the npm package name `balatro-seed-mcp` — touches scripts, CI, docs, and pifreak's muscle memory. Cosmetic; defer.
- `tools/jaml-language/vscode-extension/` — version bump deferred until after 9.0.1 is on npm and the extension's `motely-wasm-compat` dep can be bumped together.

## Why I'm confident the fix is right (and what's still uncertain)

**Confident:**
- The error message "attempted to call a UnmanagedCallersOnly method from managed code" is a NativeAOT verification error specifically about a managed callsite invoking a method marked `[UnmanagedCallersOnly]`.
- Bootsharp 9.0.0+ generates `[UnmanagedCallersOnly]` thunks for `[JSExport]`'d interface methods.
- `*FromJaml` methods called other `[JSExport]`'d interface methods on `this`. Inlining the shared logic into a private static helper removes that callsite.
- `getVersion()` and `loadJaml()` worked because they don't invoke other `[JSExport]` methods.
- `startRandomSearchFromJaml()` crashed because it called `LoadJaml` AND `StartRandomSearch` — both `[JSExport]` interface members.

**Uncertain:**
- Whether `MotelySingleSearchContext.Open()` crashes for the same reason or a different one. The test failure Nat reported (`MONO_WASM: Assert failed: .NET runtime already exited with 1`) is probably a downstream effect of the search test crashing the runtime first. If the search fix doesn't also fix `Open()`, the underlying issue is item 1 above (per-instance handle marshaling).
- Whether `session.waitForCompletionAsync(null)` on the returned handle works. If it routes to `IdleMotelySearchSession` (singleton) instead of the real instance, it returns immediately and the test asserts will fail loudly with `seedsSearched=0` — not a runtime crash. That's a clean failure mode that points at the next layer.

— Claude

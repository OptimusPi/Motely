# ARCHITECTURE.md — The Canonical Data Flow

**DO NOT "IMPROVE" THIS. DO NOT ADD WRAPPERS. DO NOT ADD ORCHESTRATION LAYERS.**

This is the architecture. It works. It was confirmed working on March 23, 2026.
Every time an AI agent "improves" it, it breaks. Stop.

---

## The Two NPM Packages

| Package | Project | Tech | Target |
|---|---|---|---|
| `motely-wasm` | `Motely.BrowserWasm/` | Bootsharp + NativeAOT-LLVM | Browser (WASM) |
| `motely-node` | `Motely.Node/` | NativeAOT `PublishNodeModule` | Node.js (linux-x64) |

**One PowerShell script builds both:** `./Build-NpmPackages.ps1`

---

## motely-wasm: The Data Flow

```
Browser JS
  │
  ├─ MotelyWasm.MotelyBrowserApi.createSingleSearchContext(seed, deck, stake)
  │    │
  │    └─ returns IMotelySingleSearchContext (Bootsharp interop proxy)
  │         │
  │         ├─ context.beginShopStream(ante)
  │         ├─ context.getNextShopItem()  →  { id, name, value }
  │         └─ context.dispose()
  │
  ├─ Program.validateJaml(jamlContent)  →  null | errorString
  │
  └─ [assembly: JSImport(typeof(IMotelyUI))]
       JS implements: notifyProgress, notifyResult, notifyComplete
```

### How It Works Internally (C#)

```
IMotelyBrowserApi.CreateSingleSearchContext(seed, deck, stake)
  │
  └─ new MotelySingleSearchContextInterop(seed, deck, stake)
       │
       └─ new MotelySeedRouterDesc(seed, deck, stake)
            │
            │  This is the KEY piece. It:
            │  1. Creates a PassthroughFilterDesc
            │  2. Runs a single-seed search with itself as the seed router
            │  3. The ContextCapturingRouter captures the MotelySingleSearchContext
            │     via ProvideSeedContext(ref MotelySingleSearchContext ctx)
            │  4. Stores _searchParams, _contextParams, _lane
            │  5. CreateContext() reconstructs the live MotelySingleSearchContext
            │
            └─ The context is a readonly ref struct. It CANNOT be stored directly.
               MotelySeedRouterDesc is the filter desc trick that keeps it alive
               by storing the parameters and reconstructing on demand.
```

### BeginShopStream / GetNextShopItem

```csharp
// MotelySingleSearchContextInterop.cs — this is the whole thing:

public void BeginShopStream(int ante)
{
    var ctx = _router.CreateContext();          // reconstruct the ref struct
    _stream = ctx.CreateShopItemStream(ante);   // on MotelySingleSearchContext
    _hasStream = true;
}

public ShopItemDto GetNextShopItem()
{
    var ctx = _router.CreateContext();
    var item = ctx.GetNextShopItem(ref _stream);
    return new ShopItemDto { Id = ..., Name = ..., Value = ... };
}
```

**That's it. No orchestrator. No middleware. No extra packages.**

---

## motely-node: The Data Flow

```
Node.js
  │
  ├─ MotelyNodeExports.validateJaml(jamlContent)  →  null | errorString
  ├─ MotelyNodeExports.analyzeSeed(seed, deck, stake)  →  JSON string
  ├─ MotelyNodeExports.getVersion()  →  version string
  │
  │  ── Search ──
  ├─ MotelyNodeExports.runSearch(jaml, threadCount?, batchCharCount?, startBatch?, endBatch?, onProgress?, onResult?)
  ├─ MotelyNodeExports.runSeedListSearch(jaml, seeds[], threadCount?, onProgress?, onResult?)
  └─ MotelyNodeExports.runRandomSearch(jaml, count, threadCount?, onProgress?, onResult?)
       │
       └─ All return JSON array of matching seeds
          onProgress(searched, found, elapsedMs) fires during search
          onResult(seed, score) fires per match
```

NativeAOT compiled to a `.node` binary. `[JSExport]` on the class.
`Microsoft.JavaScript.NodeApi.Generator` produces `.cjs`, `.mjs`, `.d.ts` automatically.

---

## Bootsharp WASM Setup (Motely.BrowserWasm)

Key csproj settings that make it work:

```xml
<OutputType>Exe</OutputType>                    <!-- YES, Exe is correct -->
<TargetFramework>net10.0-browser</TargetFramework>
<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
<BootsharpName>motely-wasm</BootsharpName>      <!-- npm package name -->
<BootsharpLLVM>true</BootsharpLLVM>             <!-- NativeAOT-LLVM -->
<BootsharpOptimize>speed</BootsharpOptimize>
<BootsharpAggressiveTrimming>true</BootsharpAggressiveTrimming>
```

Program.cs assembly attributes:

```csharp
[assembly: JSPreferences(Space = ["^Motely\\.BrowserWasm\\.", "MotelyWasm."])]
[assembly: JSExport(typeof(IMotelyBrowserApi), typeof(IMotelySingleSearchContext))]
[assembly: JSImport(typeof(IMotelyUI))]
```

Bootsharp generates `bindings.g.js`, `exports.g.js` in `dist/bootsharp/`.

---

## File Map

```
Motely.BrowserWasm/
  ├── Motely.BrowserWasm.csproj        (Bootsharp + LLVM config)
  ├── Program.cs                       (JSExport, JSImport, ValidateJaml, DI setup)
  ├── IMotelyBrowserApi.cs             (CreateSingleSearchContext, GetVersion)
  ├── IMotelySingleSearchContext.cs    (BeginShopStream, GetNextShopItem)
  ├── MotelyBrowserApi.cs             (implementation — 13 lines)
  ├── MotelySingleSearchContextInterop.cs  (uses MotelySeedRouterDesc — 37 lines)
  └── motely-wasm/
      └── package.json                 (npm package definition)

Motely.Node/
  ├── Motely.Node.csproj              (NativeAOT + PublishNodeModule)
  └── MotelyNodeExports.cs            ([JSExport] — ValidateJaml, AnalyzeSeed, GetVersion)

Motely/Analysis/
  └── MotelySeedRouterDesc.cs          (THE filter desc that captures context — 50 lines)

Build-NpmPackages.ps1                  (ONE script, builds both packages)
```

---

## Rules for Future Agents

1. **Do not add an orchestration layer.** It's dead. It was deleted. Leave it dead.
2. **Do not add wrapper packages.** Two packages. motely-wasm. motely-node. That's it.
3. **Do not "improve" MotelySeedRouterDesc.** It uses a ref struct trick. The ref struct is fine. Trust it.
4. **Do not create "shared interop" abstractions.** The browser host and node host are different. They share the Motely engine via ProjectReference. That's enough.
5. **The interop files are small on purpose.** MotelyBrowserApi is 13 lines. MotelySingleSearchContextInterop is 37 lines. If you're adding hundreds of lines, you're doing it wrong.
6. **`OutputType=Exe` is correct for Bootsharp.** Do not change it to Library.
7. **Run `./Build-NpmPackages.ps1` to build.** That's the only build script that matters.

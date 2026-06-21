# Motely WASM — macOS Build Handoff & API Refactor Plan

> **Status:** Ready for implementation. This document covers macOS setup, the Program.cs → `Motely` API refactor, FileSystem integration, and async patterns for single-seed streams.

---

## PART 1: macOS Build Environment Setup

### 1.1 Install .NET 10 Preview SDK

Bootsharp requires .NET 10 preview for WASM NativeAOT-LLVM compilation. The `global.json` pins it to `10.0.300-preview.0.26177.108`.

```bash
# Install .NET 10 preview (adjust URL when newer builds drop)
curl -sSL https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh | \
  bash /dev/stdin --channel 10.0 --install-dir ~/.dotnet

# Add to PATH (add to ~/.zshrc for persistence)
export PATH="$HOME/.dotnet:$PATH"

# Verify
dotnet --version
# Expected: 10.0.300-preview.0.26177.108 or compatible
```

### 1.2 Install WASM Workload

```bash
# Install the browser-wasm workload
dotnet workload install wasm-tools

# Verify
dotnet workload list
```

### 1.3 Bootsharp Extras / Private NuGet Feed

The `release.ps1` references `D:\extra\bootsharp\cs` for a local `Bootsharp.FileSystem` build. **This is NOT required for normal builds.** The `Directory.Packages.props` already references a published version:

```xml
<PackageVersion Include="Bootsharp.FileSystem" Version="2026.6.11.4" />
```

If you need a private feed for `rewaffle/extra` or your own Bootsharp fork:

```bash
# Option A: Add GitHub Packages as a NuGet source (needs PAT)
dotnet nuget add source \
  "https://nuget.pkg.github.com/rewaffle/index.json" \
  --name "rewaffle" \
  --username "YOUR_GITHUB_USERNAME" \
  --password "YOUR_GITHUB_PAT"

# Option B: Use a local feed (no auth, for local builds)
mkdir -p ~/.nuget/local-feed
dotnet nuget add source ~/.nuget/local-feed --name "local"

# To pack and push Bootsharp.FileSystem locally:
# cd /path/to/bootsharp/extra
dotnet pack Bootsharp.FileSystem.csproj -o ~/.nuget/local-feed
```

### 1.4 GitHub PAT for NuGet Private Feeds

If using GitHub Packages (e.g., `rewaffle/extra`):

1. Generate PAT: https://github.com/settings/tokens → `read:packages` scope
2. Store securely (Keychain or env):
   ```bash
   # In ~/.zshrc or ~/.bash_profile
   export GITHUB_NUGET_PAT="ghp_xxxxxxxxxxxxxxxxxxxx"
   ```
3. The `dotnet nuget add source` command above will prompt for the password if not provided.

### 1.5 Build the Project (macOS)

```bash
cd ~/GitHub/MotelyJAML

# Standard build (no local FileSystem needed)
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release

# Or use the cross-platform script
node Motely.Wasm/build.mjs

# Test
node Motely.Wasm/motely.test.mjs
```

**Expected output:** `Motely.Wasm/dist/` with `index.mjs`, `.wasm`, and `.d.mts` files.

### 1.6 Kimi Desktop + WebBridge Integration

For the runtime labs sponsorship perk ($100/mo), ensure WebBridge can access local builds:

```bash
# Start kimi-webbridge (if not running)
~/.kimi-webbridge/bin/kimi-webbridge start

# The daemon runs at http://127.0.0.1:10086
# Your MotelyJAML repo is at ~/GitHub/MotelyJAML
# The build output is at ~/GitHub/MotelyJAML/Motely.Wasm/dist/
```

No special setup needed beyond the standard Kimi Desktop installation. WebBridge can navigate to `file://` paths or local dev servers.

---

## PART 2: Program.cs → `Motely` API Refactor

### 2.1 The Problem

Current `Program.cs` is a single static class with everything crammed into `Program`. Consumers boot it as `Program` which is semantically wrong. The API is not discoverable, not namespaced, and violates separation of concerns.

### 2.2 The Vision: `Motely` as the Root Namespace

```csharp
// C# API surface (what Bootsharp exports to JS)

// ── Single-seed analysis ──
var result = await Motely.JAMLyzer.AnalyzeSeedAsync(
    seed: "ALEEB",
    deck: MotelyDeck.Red,
    stake: MotelyStake.Gold
);

// ── Multi-seed search with streaming ──
var search = Motely.JAMLSearch
    .Create(filterConfig)
    .WithSeeds("TEST1234", "ALEEB", "BEPIS")
    .WithProgress((seed, score, matches) => 
        Console.WriteLine($"{seed}: {score} ({matches} matches)"))
    .WithSeedCapture(seedsTable)
    .Start();

// ── File operations (Bootsharp.FileSystem) ──
var lib = await Motely.JAMLLibrary.MountAsync("/jaml-library");
var source = await lib.LoadFileAsync("filters/perkeo.jaml");
await lib.SaveFileAsync("filters/perkeo-v2.jaml", source);

// ── Export formats ──
var csv = Motely.Exporters.ToCsv(results);
var parquet = Motely.Exporters.ToParquet(results); // future
var txt = Motely.Exporters.ToPlainText(results);
```

### 2.3 Proposed C# Structure

```
Motely/
  ├── Core/
  │   ├── Motely.cs              # Root static class, namespace organizer
  │   ├── MotelyDeck.cs          # Deck enum (15 values)
  │   ├── MotelyStake.cs         # Stake enum (8 values)
  │   ├── MotelySeedResult.cs    # Seed result DTO
  │   └── MotelyProgress.cs      # Progress callback contract
  │
  ├── JAMLyzer/
  │   ├── JAMLyzer.cs            # Single-seed analysis entry point
  │   ├── JAMLyzerResult.cs      # Analysis output (jokers, shop, blinds, score)
  │   └── JAMLyzerOptions.cs     # Options (deck, stake, ante, depth)
  │
  ├── JAMLSearch/
  │   ├── JAMLSearch.cs          # Multi-seed search builder
  │   ├── JAMLSearchContext.cs   # Streaming context (implements IAsyncEnumerable)
  │   ├── JAMLSearchFilter.cs    # Filter configuration (JAML filter object)
  │   └── JAMLSearchResult.cs    # Search result (seed, score, tally)
  │
  ├── JAMLLibrary/
  │   ├── JAMLLibrary.cs         # File system abstraction (Bootsharp.FileSystem)
  │   ├── JAMLLibraryMount.cs    # Mount point / directory handle
  │   └── JAMLFileEntry.cs       # File metadata (name, path, size, modified)
  │
  ├── Exporters/
  │   ├── CsvExporter.cs         # CSV export
  │   ├── TxtExporter.cs         # Plain text export
  │   └── ParquetExporter.cs     # Parquet export (future, DuckDB.NET)
  │
  └── Internal/
      ├── MotelyEngine.cs        # Core Balatro sim (existing, renamed)
      ├── SeedDecoder.cs         # Seed decoding logic
      └── ...                    # Existing internal helpers
```

### 2.4 Bootsharp Export Configuration

```csharp
// In Motely.Wasm/Program.cs (or a dedicated Interop/ directory)

using Bootsharp;

namespace Motely.Interop;

public static partial class MotelyInterop
{
    // This is the ONLY file Bootsharp scans for [Export] / [Import]
    // All other files are clean domain code with zero Bootsharp attributes

    [Export]
    public static async Task<string> AnalyzeSeedAsync(string seed, string deck, string stake)
    {
        var result = await Motely.JAMLyzer.AnalyzeSeedAsync(seed, Enum.Parse<MotelyDeck>(deck), Enum.Parse<MotelyStake>(stake));
        return JsonSerializer.Serialize(result);
    }

    [Export]
    public static IAsyncEnumerable<string> SearchStreamAsync(string filterJson, string[] seeds)
    {
        var filter = JsonSerializer.Deserialize<JAMLSearchFilter>(filterJson);
        return Motely.JAMLSearch
            .Create(filter)
            .WithSeeds(seeds)
            .StartStreamAsync();
    }

    [Export]
    public static async Task<string> MountLibraryAsync(string path)
    {
        var lib = await Motely.JAMLLibrary.MountAsync(path);
        return JsonSerializer.Serialize(new { lib.Path, lib.Files });
    }
}
```

### 2.5 JavaScript Consumer API (Generated by Bootsharp)

```typescript
// This is what Bootsharp generates from the C# [Export] surface

import bootsharp, { MotelyInterop } from "motely-wasm";

await bootsharp.boot();

// Single seed analysis
const result = await MotelyInterop.analyzeSeedAsync("ALEEB", "Red", "Gold");
console.log(result); // JSON string → parse to JAMLyzerResult

// Streaming search (returns AsyncIterable)
for await (const seedResult of MotelyInterop.searchStreamAsync(filterJson, ["TEST1234", "ALEEB"])) {
    console.log(JSON.parse(seedResult));
}

// File system mount
const mount = await MotelyInterop.mountLibraryAsync("/jaml-library");
```

---

## PART 3: Async Patterns & Single-Seed Streams

### 3.1 The Problem: `ref` / `out` in WASM Interop

Bootsharp (and WASM interop in general) struggles with `ref`/`out` parameters. The solution is to **return DTOs instead**:

```csharp
// ❌ BAD: ref/out parameters
public static void Search(string filter, ref int progress, out string[] results) { }

// ✅ GOOD: Return a DTO, use callbacks via events
public static IAsyncEnumerable<JAMLSearchResult> SearchStreamAsync(JAMLSearchFilter filter, string[] seeds) { }
```

### 3.2 IAsyncEnumerable for Streaming

```csharp
public class JAMLSearchContext : IAsyncEnumerable<JAMLSearchResult>
{
    private readonly JAMLSearchFilter _filter;
    private readonly string[] _seeds;
    private readonly Action<string, int, int>? _onProgress;

    public async IAsyncEnumerable<JAMLSearchResult> StartStreamAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var seed in _seeds)
        {
            ct.ThrowIfCancellationRequested();
            
            var result = await AnalyzeSeedInternalAsync(seed, _filter, ct);
            if (result.Matches)
            {
                yield return result;
            }
            
            _onProgress?.Invoke(seed, result.Score, result.MatchCount);
        }
    }
}
```

### 3.3 JavaScript Async Iterable Consumption

Bootsharp will generate JS bindings that map `IAsyncEnumerable<T>` to `AsyncIterable<T>`:

```typescript
// Consumer code (in jaml-ui or seedfinder-app)
const stream = await MotelyInterop.searchStreamAsync(filterJson, seedArray);

for await (const result of stream) {
    // result is a JAMLSearchResult JSON string
    const parsed = JSON.parse(result);
    addToUI(parsed);
}
```

### 3.4 Progress Reporting (Optional)

For UI progress bars, use Bootsharp events:

```csharp
// In Motely.Interop
public static partial class MotelyInterop
{
    [Export]
    public static event Action<string, int, int>? OnSearchProgress;

    // Internal: raise event during search
    private static void ReportProgress(string seed, int score, int matchCount)
    {
        OnSearchProgress?.Invoke(seed, score, matchCount);
    }
}
```

```typescript
// JS consumer
MotelyInterop.onSearchProgress.subscribe((seed, score, matches) => {
    updateProgressBar(seed, score, matches);
});
```

---

## PART 4: FileSystem Integration (Bootsharp.FileSystem)

### 4.1 What Bootsharp.FileSystem Provides

From the search results, Bootsharp.FileSystem enables:
- **Desktop browsers:** Mount a real folder via the File System Access API (2026 spec)
- **Mobile browsers:** Single-file picker (limited by OS security)
- **Node.js:** Full filesystem access via `node:fs` polyfill

### 4.2 C# FileSystem Abstraction

```csharp
namespace Motely.JAMLLibrary;

public class JAMLLibrary
{
    private readonly IBootsharpFileSystem _fs; // Injected by Bootsharp

    public string MountPath { get; private set; } = "";
    public List<JAMLFileEntry> Files { get; } = new();

    public async Task<JAMLLibrary> MountAsync(string path)
    {
        MountPath = path;
        var entries = await _fs.ListDirectoryAsync(path);
        Files.AddRange(entries.Select(e => new JAMLFileEntry(e.Name, e.Path, e.Size)));
        return this;
    }

    public async Task<string> LoadFileAsync(string relativePath)
    {
        var fullPath = Path.Combine(MountPath, relativePath);
        return await _fs.ReadTextAsync(fullPath);
    }

    public async Task SaveFileAsync(string relativePath, string content)
    {
        var fullPath = Path.Combine(MountPath, relativePath);
        await _fs.WriteTextAsync(fullPath, content);
    }
}
```

### 4.3 JavaScript FileSystem Mount

```typescript
// In jaml-ui's useJamlLibrary hook
import { JAMLLibrary } from "motely-wasm";

export async function mountLibrary() {
    // On desktop: opens a folder picker
    // On mobile: opens a single file picker
    const library = await JAMLLibrary.mountAsync("/jaml-library");
    
    console.log("Mounted:", library.mountPath);
    console.log("Files:", library.files);
    
    const source = await library.loadFileAsync("filters/perkeo.jaml");
    return { library, source };
}
```

---

## PART 5: Naming Conventions & UX

### 5.1 Naming Philosophy

| Current | Proposed | Rationale |
|---------|----------|-----------|
| `Program` | `Motely` | Root namespace, not an entry point |
| `Program.cs` | `Motely.Interop.MotelyInterop` | Clear interop boundary |
| `Search` | `JAMLSearch` | Action-oriented, namespaced |
| `Analyze` | `JAMLyzer` | Analyzer + JAML = memorable |
| `Library` | `JAMLLibrary` | File library, JAML-specific |
| `Export` | `Motely.Exporters` | Clear intent, plural for collection |
| `SeedResult` | `JAMLSearchResult` | Context-specific, not generic |

### 5.2 Method Naming Rules

```csharp
// ✅ Async methods end in Async
public Task<JAMLyzerResult> AnalyzeSeedAsync(...)

// ✅ Builder methods are fluent
public JAMLSearch WithSeeds(params string[] seeds)
public JAMLSearch WithProgress(Action<string, int, int> callback)

// ✅ Start methods are explicit
public IAsyncEnumerable<JAMLSearchResult> StartStreamAsync()
public Task<JAMLSearchResult[]> StartAsync() // batch mode

// ✅ DTOs are records (immutable)
public record JAMLyzerResult(string Seed, int Score, JokerInfo[] Jokers, ...);
```

### 5.3 UX for the Bot (Even Code is UX)

```csharp
// The bot writes this:
"Use Motely.JAMLyzer.AnalyzeSeedAsync() for single-seed analysis."

// Not this:
"Call Program.Analyze() but pass the seed first, then deck, then stake."
```

Good names reduce cognitive load. The API should be **discoverable** via IntelliSense:
- Type `Motely.` → see `JAMLyzer`, `JAMLSearch`, `JAMLLibrary`, `Exporters`
- Type `Motely.JAMLSearch.` → see `Create`, `WithSeeds`, `WithProgress`, `StartStreamAsync`

---

## PART 6: Implementation Roadmap

### Phase 1: Foundation (This Week)
1. ✅ Create `Motely/` directory structure
2. ✅ Rename `Program` → `MotelyInterop` (interop boundary only)
3. ✅ Extract `MotelyDeck`, `MotelyStake` enums
4. ✅ Create `JAMLyzer` static class with `AnalyzeSeedAsync`

### Phase 2: Streaming (Next Week)
1. ✅ Implement `JAMLSearch` builder pattern
2. ✅ Implement `IAsyncEnumerable` streaming
3. ✅ Add progress events
4. ✅ Wire into Bootsharp exports

### Phase 3: FileSystem (Next Week)
1. ✅ Add `JAMLLibrary` with `MountAsync`, `LoadFileAsync`, `SaveFileAsync`
2. ✅ Test with Bootsharp.FileSystem in browser
3. ✅ Add `useJamlLibrary` hook in jaml-ui

### Phase 4: Exporters (Future)
1. ✅ CSV exporter
2. ✅ Plain text exporter
3. 🔄 Parquet exporter (needs DuckDB.NET WASM support)

---

## PART 7: Quick Reference

### macOS Build Commands
```bash
# Build
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release

# Test
node Motely.Wasm/motely.test.mjs

# Cross-platform script
node Motely.Wasm/build.mjs

# Publish to npm (after test passes)
node Motely.Wasm/build.mjs publish
```

### NuGet Feed Commands
```bash
# Add GitHub Packages feed (for private packages)
dotnet nuget add source \
  "https://nuget.pkg.github.com/rewaffle/index.json" \
  --name "rewaffle" \
  --username "YOUR_USER" \
  --password "$GITHUB_NUGET_PAT"

# Add local feed (for local builds)
dotnet nuget add source ~/.nuget/local-feed --name "local"

# List sources
dotnet nuget list source
```

### Bootsharp Version Check
```bash
# The pinned versions in Directory.Packages.props:
# Bootsharp: 0.9.0
# Bootsharp.Common: 0.9.0
# Bootsharp.Inject: 0.9.0
# Bootsharp.FileSystem: 2026.6.11.4
```

---

## Appendix: The `release.ps1` D:\ Mystery Explained

The `D:\extra\bootsharp\cs` path in `release.ps1` is **Step 1 of a local development workflow** for the `rewaffle/extra` repository. It is NOT required for normal builds:

```powershell
# Step 1: Pack Bootsharp.FileSystem from local source (rewaffle/extra repo)
# → Only needed if you're modifying Bootsharp.FileSystem itself
$fsCs = "D:\extra\bootsharp\cs"
dotnet pack "$fsCs\Bootsharp.FileSystem\Bootsharp.FileSystem.csproj"

# Steps 2-7: Normal build (works everywhere)
# → These are the ONLY steps you need
```

**For normal builds:** Skip step 1. The `Bootsharp.FileSystem` `2026.6.11.4` package is already on NuGet (or your configured feed). The `build.mjs` script I wrote above only does steps 2-7 (restore, publish, test, optionally npm publish).

**For modifying Bootsharp.FileSystem:** Clone `rewaffle/extra` to `~/extra/bootsharp/cs`, change the path in the script, or set an env var.

---

> **Handoff complete.** Ready for C# implementation. The `build.mjs` is already in `Motely.Wasm/`. The API design is in `Motely/` namespace. FileSystem integration is documented. Go build it, comrade. o7

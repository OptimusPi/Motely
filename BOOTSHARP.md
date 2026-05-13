# Bootsharp Reference (compiled from D:\bootsharp\docs\)

---

## Getting Started

Configure `.csproj`:
```xml
<TargetFramework>net10.0</TargetFramework>
<RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
<PackageReference Include="Bootsharp" Version="*-*"/>
```

Basic interop pattern:
```csharp
public static partial class Program
{
    [Export] public static event Action<string>? OnMainInvoked;

    public static void Main() => OnMainInvoked?.Invoke($"Hello {GetFrontendName()}!");

    [Import] public static partial string GetFrontendName();
    [Export] public static string GetBackendName() => Environment.Version;
}
```

JS side:
```js
import bootsharp, { Program } from "./bin/bootsharp/index.mjs";
Program.getFrontendName = () => "Browser";
Program.onMainInvoked.subscribe(console.log);
await bootsharp.boot();
console.log(Program.getBackendName());
```

---

## Serialization

**Natively marshalled (in-memory):** bool, byte, char, string, int, long, float, DateTime — and Task/array variants.

**Everything else** (records, structs, read-only collections) is binary-serialized automatically. No `[MarshalAs]` needed.

```csharp
public record User(long Id, string Name, DateTime Registered);
[Export] public static void AddUser(User user) { }
[Export] public static event Action<User>? OnUserModified;
```
```ts
Program.addUser({ id: 17, name: "Carl", registered: Date.now() });
Program.onUserModified.subscribe(user => {});
```

**Key rule:** Only types with *immutable* semantics (structs, records, read-only collections) are serialized. **Mutable types (classes, interfaces) are passed by reference as interop instances.**

Enums → numbers (with name↔index maps on JS side).
Dictionaries → ES6 `Map`.
`IReadOnlyList` / `IReadOnlyDictionary` → array / map.

---

## Interop Instances

When a **class or interface** appears on the interop boundary, Bootsharp generates an instance binding and passes it **by reference** — not serialized.

```csharp
public interface IExported
{
    string Value { get; set; }
    string GetFromCSharp();
}

public class Exported : IExported
{
    public string Value { get; set; } = "cs";
    public string GetFromCSharp() => "cs";
}

public static partial class Factory
{
    [Export] public static IExported GetExported() => new Exported();
    [Import] public static partial IImported GetImported();
}
```
```ts
const exported = Factory.getExported();
exported.getFromCSharp(); // calls C#
exported.value = "updated"; // invokes C# setter
```

> **NOTE:** Only user types get instance binding. BCL types are excluded.

This means: returning `IMotelySearch` from an `[Export]` method gives JS a proxy with all its properties and methods live.

---

## Events

**Export (C# → JS):**
```csharp
[Export]
public static event Action<string>? OnSomethingChanged;

public static void UpdateSomething(string payload)
{
    OnSomethingChanged?.Invoke(payload);
}
```
```ts
Program.onSomethingChanged.subscribe(handleSomething);
Program.onSomethingChanged.unsubscribe(handleSomething);
```

**Import (JS → C#):**
```csharp
[Import]
public static event Action<string>? OnSomethingChanged;
```
```ts
Program.onSomethingChanged.broadcast("updated");
```

Events on modules and instances are picked up automatically — no annotation needed.

---

## Interop Modules

Use assembly-level `[Import]`/`[Export]` to auto-generate all bindings for a type:

```csharp
// JS implements this:
[assembly: Import(typeof(IFrontend))]

// C# exposes this to JS:
[assembly: Export(typeof(IBackend))]
```

Imported modules must be interfaces (Bootsharp generates the C# implementation).
Exported modules can be interfaces or non-static classes.

React sample: `D:\bootsharp\samples\react\`

---

## Interop Modules — React Sample Pattern

```csharp
// Backend.WASM/Program.cs
[assembly: Export(typeof(Backend.IComputer))]
[assembly: Import(typeof(Backend.Prime.IPrimeUI))]
[assembly: Preferences(Space = [".+", "Computer"])]

new ServiceCollection()
    .AddSingleton<Backend.IComputer, Backend.Prime.Prime>()
    .AddBootsharp()
    .BuildServiceProvider()
    .RunBootsharp();
```

```csharp
// IPrimeUI.cs — JS implements this
public interface IPrimeUI
{
    Options GetOptions();
}
```

---

## Preferences

`[assembly: Preferences(Space = [pattern, replacement])]` — remaps C# namespaces to JS namespaces.

Patterns matched against full C# type name via `Regex.Replace`.

```csharp
// All types in Motely.Wasm.Program → "Motely" in JS
[assembly: Preferences(Space = [@"^Motely\.Wasm\.Program$", "Motely"])]
```

---

## Build Config

| Property | Default | Description |
|---|---|---|
| `BootsharpName` | `bootsharp` | JS module name |
| `BootsharpPublishDirectory` | `/bin/bootsharp` | Where to publish JS module |
| `BootsharpBinariesDirectory` | `publish-dir/bin` | Where to publish WASM binaries |
| `BootsharpPackageDirectory` | project-dir | Where to publish `package.json` |

---

## File System Extension (`Bootsharp.FileSystem`)

```xml
<PackageReference Include="Bootsharp.FileSystem" Version="*-*"/>
```

```ts
import * as fs from "@rewaffle/bootsharp-file-system";
fs.init(Bootsharp.FileSystem.FileMounter);
await bootsharp.boot();
```

C# gets `IFileMounter` injected:
```csharp
interface IFileMounter
{
    Task<string?> PickRoot(PickOptions? options = null);
    Task<IFileSystem> Mount(string root, IFileWatcher watcher, MountOptions? options = null);
    Task Unmount(string root);
}

interface IFileSystem
{
    Task<byte[]> ReadFile(string uri);
    Task WriteFile(string uri, byte[] content);
    Task CreateDirectory(string uri);
    Task RemoveDirectory(string uri);
    Task DeleteFile(string uri);
    Task<FileInfo> GetFileInfo(string uri);
}

interface IFileWatcher
{
    Task HandleFileChanges(IReadOnlyList<Change> changes);
}
```

---

## Nullability Convention

| Position | TypeScript form |
|---|---|
| Nullable method arg | `\| undefined` |
| Nullable property | `?` (optional) |
| Nullable return value | `\| null` |
| Nullable collection element | `\| null` |
| Nullable event payload | `\| undefined` |

---

## Bootsharp.FileSystem — Local Build Chain (D:\extra)

`D:\extra` is the sponsors repo that builds `Bootsharp.FileSystem`. MotelyJAML references it as a NuGet package.

**Layout:**
- `D:\extra\cs\Bootsharp.FileSystem/` — the packaged library (auto-versioned `yyyy.MM.dd.HHmm`)
- `D:\extra\cs\.nuget/` — local nupkg output
- `D:\bootsharp\src\cs\.nuget/` — local Bootsharp core nupkg output

**To pack a fresh Bootsharp.FileSystem locally:**
```bash
# 1. Pack sibling Bootsharp first (see D:\bootsharp\AGENTS.md)
# 2. Clean old output
rm -rf D:/extra/cs/Bootsharp.FileSystem/{bin,obj}
rm -f D:/extra/cs/.nuget/*.nupkg
# 3. Pack
dotnet pack D:/extra/cs/Bootsharp.FileSystem/Bootsharp.FileSystem.csproj \
  -c Release -o D:/extra/cs/.nuget \
  --source 'https://api.nuget.org/v3/index.json' \
  --source 'D:\bootsharp\src\cs\.nuget'
```

**NuGet source config** (`%APPDATA%\NuGet\NuGet.Config`):
```xml
<packageSources>
  <add key="bootsharp-local" value="D:\bootsharp\src\cs\.nuget" />
  <add key="bootsharp-filesystem-local" value="D:\extra\bootsharp\cs\.nuget" />
</packageSources>
```

If sources aren't configured, restore pulls stale `Bootsharp.Common 0.7.0` from nuget.org — missing `ImportAttribute` — build fails with `CS0246`. Fix: check NuGet sources.

Version scheme: every pack is uniquely timestamped. No manual bump needed.

---

## Key Rules for MotelyJAML

- `IMotelySearch` is a **class** → Bootsharp proxies it by reference. Return it directly from `[Export]`. JS gets `.cancel()`, `.matchingSeeds`, `.isCompleted`, etc.
- `MotelyProgress` is a **class** → also proxied by reference, not serialized. Works as event payload.
- `JamlSearchPlan` is a **record** → serialized (immutable).
- `MotelySeedScoreTally` is an `unsafe struct` → BCL exclusion applies; cannot proxy. Needs a plain record DTO if it must cross the boundary.
- **No facade wrappers.** Return real types. Let Bootsharp handle the bridge.

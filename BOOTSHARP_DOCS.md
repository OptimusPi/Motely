# Bootsharp Docs (collected)

Source: `d:\bootsharp\docs\guide\**` — flattened into one file so you don't have to chase paths.

<!-- canary: pumpkinpie -->

---

## Introduction

### What?

Bootsharp is a solution for building web applications where the domain logic is authored in .NET C# and consumed by a standalone JavaScript or TypeScript project.

### Why?

C# is a popular choice for building maintainable software with complex domain logic, especially in enterprise and financial systems. However, its frontend capabilities are limited—particularly when compared to what the web ecosystem offers.

The web platform is the industry standard for modern UI development. Frameworks such as React and Svelte provide exceptional tooling, fast iteration, and a vast ecosystem.

Solutions like Blazor attempt to bring the entire web platform into .NET, effectively reversing the natural workflow and restricting access to native JavaScript tools. Bootsharp takes the opposite approach: it enables high-level interoperation between C# and TypeScript, so each layer can be developed within its optimal environment.

With Bootsharp, you implement domain logic in C# and build the UI using familiar web technologies, then connect them seamlessly. Your project can be published to the web or bundled as a native desktop or mobile application using Electron or Tauri.

### How?

Bootsharp is installed as a NuGet package into the C# project dedicated to building the solution for the web. It is specifically designed not to "leak" the dependency outside the entry assembly of the web target—essential for keeping the domain clean of any platform-specific details.

```jsonc
// package.json
"scripts": {
    "compile": "dotnet publish backend"
},
"dependencies": {
    "backend": "file:backend"
}
```

```ts
// main.ts
import bootsharp, { Backend, Frontend } from "backend";
await bootsharp.boot();
Frontend.onUserChanged.subscribe(updateUserUI);
Backend.addUser({ name: "Carl" });
```

---

## Getting Started

### Configure C# Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Bootsharp" Version="*-*"/>
    </ItemGroup>
</Project>
```

### Author Interop APIs

```cs
using System;
using Bootsharp;

public static partial class Program
{
    [Export] public static event Action<string>? OnMainInvoked;

    public static void Main ()
    {
        OnMainInvoked?.Invoke($"Hello {GetFrontendName()}, .NET here!");
    }

    [Import] public static partial string GetFrontendName ();
    [Export] public static string GetBackendName () => Environment.Version;
}
```

For large API surfaces, prefer interop interfaces over static methods.

### Compile ES Module

```sh
dotnet publish
```

Produces `bin/bootsharp/`:

| Name         | Type   | Description                                        |
|--------------|--------|----------------------------------------------------|
| types        | folder | TypeScript declarations for the interop APIs.     |
| index.mjs    | file   | The compiled ES module with embedded binaries.    |
| package.json | file   | NPM package manifest.                             |

Release publishes auto-enable NativeAOT-LLVM, speed-focused WASM optimization, aggressive trimming, and a Binaryen pass when `wasm-opt` is on PATH. Use `dotnet publish -c Debug` for faster build / better debugging at the cost of size + perf.

### Consume in JS

```js
import bootsharp, { Program } from "./bin/bootsharp/index.mjs";
Program.getFrontendName = () => process.version;
Program.onMainInvoked.subscribe(console.log);
await bootsharp.boot();
console.log(`Hello ${Program.getBackendName()}!`);
```

Run with `node main.mjs` / `deno run main.mjs` / `bun main.mjs` / `npx serve` for browser.

Sample: https://github.com/elringus/bootsharp/tree/main/samples/minimal

---

## Build Configuration

MSBuild properties in `.csproj`:

| Property                   | Default    | Description                                                  |
|----------------------------|------------|--------------------------------------------------------------|
| BootsharpName              | bootsharp  | Name of the generated JavaScript module.                     |
| BootsharpEmbedBinaries     | true       | Whether to embed binaries to the JavaScript module file.     |
| BootsharpBundleCommand     | npx rollup | The command to bundle the generated JS solution.             |
| BootsharpPublishDirectory  | /bin       | Directory to publish generated JavaScript module.            |
| BootsharpTypesDirectory    | /types     | Directory to publish type declarations.                      |
| BootsharpBinariesDirectory | /bin       | Directory to publish binaries when EmbedBinaries disabled.   |
| BootsharpPackageDirectory  | /          | Directory to publish package.json.                           |

Example:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <BootsharpName>backend</BootsharpName>
    <BootsharpPackageDirectory>$(SolutionDir)</BootsharpPackageDirectory>
    <BootsharpEmbedBinaries>false</BootsharpEmbedBinaries>
    <BootsharpBinariesDirectory>$(SolutionDir)../public/bin</BootsharpBinariesDirectory>
</PropertyGroup>
```

### Globalization

Default = invariant globalization (smaller bundle). To enable:

```xml
<PropertyGroup>
    <InvariantGlobalization>false</InvariantGlobalization>
</PropertyGroup>
```

| Mode    | How to enable                                                            | Behavior                                            |
|---------|--------------------------------------------------------------------------|-----------------------------------------------------|
| Sharded | Disable `InvariantGlobalization`                                         | Sharded ICU files (`icudt_*.dat`).                  |
| Full    | Disable `InvariantGlobalization` + enable `WasmIncludeFullIcuData`       | Full ICU file (`icudt.dat`), many cultures in one.  |

---

## Type Declarations

Bootsharp generates TypeScript declarations under `types/`.

### Functions

```csharp
public class Foo { [Export] public static void Bar() { } }
```
```ts
export namespace Foo { export function bar(): void; }
```

Imports become assignable properties:

```csharp
public partial class Foo { [Import] public static partial void Bar(); }
```
```ts
export namespace Foo { export let bar: () => void; }
// main.ts
Foo.bar = () => {};
```

### Events

Exported = `EventSubscriber`, imported = `EventBroadcaster`.

```csharp
[Export] public static event Action<string>? OnBar;
```
```ts
export const onBar: EventSubscriber<[payload: string]>;
Foo.onBar.subscribe(p => {});
```

```csharp
[Import] public static event Action<string>? OnBar;
```
```ts
export const onBar: EventBroadcaster<[payload: string]>;
Foo.onBar.broadcast("updated");
```

### Documentation

XML doc comments on C# APIs are mirrored into TypeScript JSDoc.

### Nullability summary

- nullable args → `| undefined`
- nullable props → optional `?`
- nullable returns → `| null`
- nullable collection elements / dict values → `| null`

### Type Crawling

Bootsharp recursively emits referenced records / interfaces from interop signatures.

### Configuring Type Mappings

Override generated TS for given C# types via `Type` patterns of emit preferences.

---

## Emit Preferences

`[Preferences]` assembly attribute, with array properties of `(pattern, replacement)` strings fed to `Regex.Replace`.

### Space

Default: bindings grouped under matching C# namespaces.

```cs
[assembly: Preferences(
    Space = ["^Foo\.Bar\.(\S+)", "Baz.$1"]
)]
```

Pattern modifications:
- interfaces have first character removed
- generics have parameter spec removed
- nested type names have `+` replaced with `.`

### Type

Customize generated TypeScript type syntax — matched against full C# type names of args, returns, and properties.

### Function

Customize generated JavaScript function names — matched against C# interop method names.

---

## Events

```csharp
[Export]
public static event Action<string>? OnSomethingChanged;

public static void UpdateSomething (string payload)
{
    OnSomethingChanged?.Invoke(payload);
}
```

```ts
Program.onSomethingChanged.subscribe(handleSomething);
Program.onSomethingChanged.unsubscribe(handleSomething);
```

`[Import]` events are broadcast from JS:

```ts
Program.onSomethingChanged.broadcast("updated");
```

Supports `Action`, `EventHandler`, and any custom delegate without a return type. Events on interop interfaces are picked up automatically — no annotation needed.

### React Event Hooks

```ts
export function useEvent<T extends unknown[]>(
    event: EventSubscriber<T>, handler: (...args: [...T]) => void,
    deps?: DependencyList | undefined, destructor?: () => void) {
    useEffect(() => {
        event.subscribe(handler);
        return () => {
            event.unsubscribe(handler);
            destructor?.();
        };
    }, [event, handler, destructor, ...(deps ?? [])]);
}

export function useEventState<T extends unknown[]>(
    event: EventSubscriber<T>,
    defaultState?: T[0]): T[0] | undefined {
    const initial = event.last === undefined ?
        defaultState : getFirstArg(event.last);
    const [state, setState] = useState<T[0] | undefined>(initial);
    useEvent(event, (...args) => setState(getFirstArg(args)), []);
    return state;

    function getFirstArg(args: T): T[0] | undefined {
        return args[0] === null ? undefined : args[0];
    }
}
```

`useEventState` handles subscribe/unsubscribe and uses `event.last` so a late-mounted component catches up.

---

## Interop Instances

When an **interface** is supplied as an interop arg or return type, Bootsharp generates an instance binding instead of value-serializing.

```csharp
public interface IExported { string Value { get; set; } string GetFromCSharp (); }
public interface IImported { string Value { get; set; } string GetFromJavaScript (); }

public class Exported : IExported {
    public string Value { get; set; } = "cs";
    public string GetFromCSharp () => "cs";
}

public static partial class Factory {
    [Export] public static IExported GetExported () => new Exported();
    [Import] public static partial IImported GetImported ();
}
```

```ts
class Imported implements IImported {
    value = "js";
    getFromJavaScript() { return "js"; }
}

Factory.getImported = () => new Imported();

const exported = Factory.getExported();
exported.getFromCSharp();
exported.value = "updated";
```

Limits:
- can't be args/returns of other interop instance methods
- interfaces from `System` namespace are not qualified

---

## Interop Interfaces

Use `[Import]` / `[Export]` assembly attributes — Bootsharp generates bindings automatically.

```csharp
interface IFrontend { bool IsMuted { get; set; } }
[assembly: Import(typeof(IFrontend))]
```

```ts
export namespace Frontend { export let isMuted: boolean; }
```

```csharp
interface IBackend {
    event Action<Data> OnDataChanged;
    Data? Current { get; set; }
    void AddData (Data data);
}
[assembly: Export(typeof(IBackend))]
```

```ts
export namespace Backend {
    export const onDataChanged: EventSubscriber<[data: Data]>;
    export let current: Data | undefined;
    export function addData(data: Data): void;
}
```

Imported interface events: declare a real C# event on the interface; Bootsharp generates a JavaScript `EventBroadcaster` plus a regular subscribable C# event on the generated implementation.

For auto-injection, use the dependency-injection extension.

Sample: https://github.com/elringus/bootsharp/tree/main/samples/react

---

## Namespaces

### Static Methods

Full C# type name (incl. namespace) maps to JavaScript object:

```csharp
class Class { [Export] static void Method() {} }
namespace Foo { class Class { [Export] static void Method() {} } }
namespace Foo.Bar { class Class { [Export] static void Method() {} } }
```
```ts
Class.method();
Foo.Class.method();
Foo.Bar.Class.method();
```

Nested classes are treated as namespace levels:
```ts
Foo.Class.Nested.method();
```

### Interop Interfaces

Interface name has `I` prefix; generated impl drops it. Namespace is mirrored.

### Types

Custom records / classes / interfaces in API signatures live under their namespace, or root if none.

### Configuring

Use `Space` patterns of emit preferences.

---

## Nullability

Bootsharp accepts both `null` and `undefined` from JS. Generated TS uses contextual conventions:

- nullable method args → `| undefined`
- nullable object props → optional `?`
- nullable returns → `| null`
- nullable collection elements / dict values → `| null`
- event payloads → missing nullable args become `undefined`

### Why split

`undefined` is natural for omitted/optional input. `null` is natural for explicit data crossing the boundary (returns, array slots).

### Args

```csharp
[Export] public static void SetTitle (string? title) { }
```
```ts
export function setTitle(title: string | undefined): void;
```

### Returns

```csharp
[Export] public static string? FindUserName (int id) => null;
```
```ts
export function findUserName(id: number): string | null;
```

### Object Properties

```csharp
public record User (string Name, string? Nickname);
```
```ts
export interface User { name: string; nickname?: string; }
```

### Collection Elements

```csharp
[Export] public static string?[]? EchoNames (string?[]? names) => names;
```
```ts
export function echoNames(names: Array<string | null> | undefined): Array<string | null> | null;
```

### Dictionary Values

```csharp
[Export] public static Dictionary<string, string?>? GetLabels () => /* ... */;
```
```ts
export function getLabels(): Map<string, string | null> | null;
```

`null` here avoids ambiguity with `Map.get`, which already returns `undefined` for missing keys.

### Events

```csharp
[Export] public static event Action<int, Vehicle?>? OnVehicleChanged;
```
```ts
export const onVehicleChanged: EventSubscriber<[id: number, vehicle: Vehicle | undefined]>;
```

Event payloads behave like broadcast args — `undefined` is more natural.

---

## Serialization

Natively-marshalled simple types:

| C#       | JavaScript | Task of | Array of |
|----------|------------|:-------:|:--------:|
| bool     | boolean    | yes     | no       |
| byte     | number     | yes     | yes      |
| char     | string     | yes     | no       |
| string   | string     | yes     | yes      |
| int      | number     | yes     | yes      |
| long     | BigInt     | yes     | no       |
| float    | Number     | yes     | no       |
| DateTime | Date       | yes     | no       |

Non-native types use a custom binary serialization, transparent on both ends:

```csharp
public record User (long Id, string Name, DateTime Registered);
[Export] public static void AddUser (User user) { }
[Export] public static event Action<User>? OnUserModified;
```

```ts
Program.addUser({ id: 17, name: "Carl", registered: Date.now() });
Program.onUserModified.subscribe(u => {});
```

### Enums

Marshalled as numbers; name <-> index map emitted on JS side:

```ts
const option = Program.getOption();
option === Program.Options.Bar; // true
Program.Options[Program.Options.Foo]; // "Foo"
Program.Options[1]; // "Bar"
```

### Dictionaries

C# dictionaries → ES6 `Map`.

### Collection Interfaces

`IReadOnlyList`, `IReadOnlyCollection`, `IReadOnlyDictionary`, etc — accepted; marshalled as plain arrays / maps.

### Computed Properties

Computed C# properties are evaluated on serialization and written as plain JS values:

```csharp
public record Order {
    public required string Id { get; init; }
    public required int Revision { get; init; }
    public string Version => $"{Id}:{Revision}";
}
```

```ts
const order = Orders.getCurrent();
order.version; // "A:7"
```

---

## Sideloading Binaries

Default = embedded DLLs + .NET WASM in the JS module (~30% larger from base64 conversion). Disable:

```xml
<PropertyGroup>
    <BootsharpEmbedBinaries>false</BootsharpEmbedBinaries>
</PropertyGroup>
```

Then provide resources at boot:

```ts
const resources = {
    wasm: Uint8Array,
    assemblies: [{ name: "Foo.wasm", content: Uint8Array }],
    entryAssemblyName: "Foo.dll"
};
await dotnet.boot({ resources });
```

Or fetch automatically:

```ts
await backend.boot({ root: "/bin" });
```

Sample: https://github.com/elringus/bootsharp/blob/main/samples/react

---

## NativeAOT-LLVM

Bootsharp uses .NET's experimental NativeAOT-LLVM backend for `browser-wasm`. Faster + smaller than Mono AOT.

Starting with Bootsharp 0.8.0, no extra config required — Release publishes auto-enable LLVM, speed-focused codegen, and required trim settings.

### Binaryen

Bootsharp tries to run `wasm-opt` on Release publishes:

1. Install Binaryen: https://github.com/WebAssembly/binaryen/releases
2. Make sure `wasm-opt` is on PATH
3. If missing, Bootsharp logs a warning and ships non-fully-optimized WASM

---

## Extensions: Dependency Injection

Add `Bootsharp.Inject`:

```xml
<PackageReference Include="Bootsharp" Version="*-*"/>
<PackageReference Include="Bootsharp.Inject" Version="*-*"/>
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="*"/>
```

```csharp
using Bootsharp;
using Bootsharp.Inject;
using Microsoft.Extensions.DependencyInjection;

[assembly: Export(typeof(IExported))]
[assembly: Import(typeof(IImported))]

new ServiceCollection()
    .AddBootsharp()
    .AddSingleton<SomeService>()
    .AddSingleton<IExported, Exported>()
    .BuildServiceProvider()
    .RunBootsharp();
```

`AddBootsharp` injects generated `IImported` impl. `RunBootsharp` initializes generated export impls by resolving the registered handlers (e.g. `IExported` → `Exported`).

```csharp
public class SomeService (IImported imported) { }
```

Sample: https://github.com/elringus/bootsharp/blob/main/samples/react

---

## Extensions: File System

> SPONSOR-ONLY extension. https://github.com/sponsors/elringus

C# bindings + JS package over the browser File System Access API.

### Install

```xml
<PackageReference Include="Bootsharp" Version="*-*"/>
<PackageReference Include="Bootsharp.FileSystem" Version="*-*"/>
```

```json
{
    "dependencies": {
        "backend": "file:backed",
        "@rewaffle/bootsharp-file-system": "latest"
    }
}
```

### Init

```ts
import bootsharp, { Bootsharp } from "backend";
import * as fs from "@rewaffle/bootsharp-file-system";

fs.init(Bootsharp.FileSystem.FileMounter);
await bootsharp.boot();
```

### IFileMounter

```csharp
interface IFileMounter
{
    Task<string?> PickRoot (PickOptions? options = null);
    Task<IFileSystem> Mount (string root, IFileWatcher watcher);
    Task Unmount (string root);
}
```

`PickRoot` opens the browser directory picker; returns a unique root id or `null` if cancelled. `PickOptions` controls starting dir, write-access prompt, etc.

### IFileSystem

```csharp
interface IFileSystem
{
    Task CreateDirectory (string uri);
    Task RemoveDirectory (string uri);
    Task WriteFile (string uri, byte[] content);
    Task DeleteFile (string uri);
    Task<byte[]> ReadFile (string uri);
    Task<FileInfo> GetFileInfo (string uri);
}
```

### IFileWatcher

```csharp
interface IFileWatcher
{
    Task HandleFileChanges (FileChange[] changes);
}
```

Watcher fires on add / remove / modify of any entry under the mounted root, until `Unmount`.

### Demo (from `d:\extra\bootsharp\cs\Bootsharp.Extra.WASM\FileSystemDemo.cs`)

```csharp
using Bootsharp;
using Bootsharp.FileSystem;

public static partial class FileSystemDemo
{
    private class FileWatcher : IFileWatcher
    {
        public Task HandleFileChanges (IReadOnlyList<Change> changes)
        {
            foreach (var c in changes)
                if (c.Added) AddEntry(c.Entry.Uri, c.File ? $"- {c.Entry.Uri}" : $"[{c.Entry.Uri}]");
                else if (c.Removed) RemoveEntry(c.Entry.Uri);
                else if (c.Modified) ModifyEntry(c.Entry.Uri, $"- {c.Entry.Uri}*");
                else if (c.Moved) MoveEntry(c.FromUri, c.Entry.Uri, c.File ? $"- {c.Entry.Uri}" : $"[{c.Entry.Uri}]");
            return Task.CompletedTask;
        }
    }

    [Export]
    public static async Task PickAndMount ()
    {
        var mounter = GetService<IFileMounter>();
        if (await mounter.PickRoot(new() { Id = "demo" }) is { } root)
            await mounter.Mount(root, new FileWatcher());
    }

    [Import] public static partial void AddEntry (string uri, string text);
    [Import] public static partial void ModifyEntry (string uri, string text);
    [Import] public static partial void MoveEntry (string fromUri, string toUri, string text);
    [Import] public static partial void RemoveEntry (string uri);
}
```

Sample app + JS package live at https://github.com/rewaffle/extra (mirrored locally at `d:\extra\bootsharp`).

---

## Path cheat sheet

| What                          | Where                                       |
|-------------------------------|---------------------------------------------|
| Bootsharp upstream source     | `d:\bootsharp`                              |
| Bootsharp docs                | `d:\bootsharp\docs\guide\**`                |
| rewaffle/extra (sponsor pkgs) | `d:\extra\bootsharp`                        |
| FileSystem C# package         | `d:\extra\bootsharp\cs\Bootsharp.FileSystem`|
| FileSystem JS package         | `d:\extra\bootsharp\packages\file-system`   |
| FileSystemDemo                | `d:\extra\bootsharp\cs\Bootsharp.Extra.WASM\FileSystemDemo.cs` |
| FileSystem .nupkg cache       | `d:\extra\bootsharp\cs\.nuget\Bootsharp.FileSystem.*.nupkg` |

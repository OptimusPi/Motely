# build-config.md
# Build Configuration

Build and publish related options are configured in `.csproj` file via MSBuild properties.

| Property                    | Default    | Description                                                                                                                      |
|-----------------------------|------------|----------------------------------------------------------------------------------------------------------------------------------|
| BootsharpName               | bootsharp  | Name of the generated JavaScript module.                                                                                         |
| BootsharpEmbedBinaries      | true       | Whether to embed binaries to the JavaScript module file.                                                                         |
| BootsharpAggressiveTrimming | false      | Whether to disable some .NET features to reduce binary size.                                                                     |
| BootsharpOptimize           | none       | Whether to optimize the WASM for `speed` or `size`. Requires [Binaryen](https://github.com/WebAssembly/binaryen) in system path. |
| BootsharpLLVM               | false      | Enable experimental [NativeAOT-LLVM](/guide/llvm) backend.                                                                       |
| BootsharpBundleCommand      | npx rollup | The command to bundle generated JavaScrip solution.                                                                              |
| BootsharpPublishDirectory   | /bin       | Directory to publish generated JavaScript module.                                                                                |
| BootsharpTypesDirectory     | /types     | Directory to publish type declarations.                                                                                          |
| BootsharpBinariesDirectory  | /bin       | Directory to publish binaries when `EmbedBinaries` disabled.                                                                     |
| BootsharpPackageDirectory   | /          | Directory to publish `package.json` file.                                                                                        |

Below is an example configuration, which will make Bootsharp name compiled module "backend" (instead of the default "bootsharp"), publish the module under solution directory root (instead of "/bin"), disable binaries embedding and instead publish them under "public/bin" directory one level above the solution root and enable aggressive assembly trimming and WASM optimization to reduce the build size:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
        <BootsharpName>backend</BootsharpName>
        <BootsharpPackageDirectory>$(SolutionDir)</BootsharpPackageDirectory>
        <BootsharpEmbedBinaries>false</BootsharpEmbedBinaries>
        <BootsharpBinariesDirectory>$(SolutionDir)../public/bin</BootsharpBinariesDirectory>
        <BootsharpAggressiveTrimming>true</BootsharpAggressiveTrimming>
        <BootsharpOptimize>size</BootsharpOptimize>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Bootsharp" Version="*-*"/>
    </ItemGroup>

</Project>
```


# declarations.md
# Type Declarations

Bootsharp will automatically generate [type declarations](https://www.typescriptlang.org/docs/handbook/2/type-declarations) for interop APIs when building the solution. The files are emitted under "types" directory of the compiled module package.

## Function Declarations

For the interop methods, function declarations are emitted.

Exported `[JSInvokable]` methods will have associated function assigned under the declaring type space:

```csharp
public class Foo
{
    [JSInvokable]
    public static void Bar() { }
}
```

â€” will make following emitted in the declaration file:

```ts
export namespace Foo {
    export function bar(): void;
}
```

â€” which allows consuming the API in JavaScript as follows:

```ts
import { Foo } from "bootsharp";

Foo.bar();
```

Imported `[JSFunction]` methods will be emitted as properties, which have to be assigned before booting the runtime:

::: code-group

```csharp [Foo.cs]
public partial class Foo
{
    [JSFunction]
    public static partial void Bar();
}
```

```ts [bindings.d.ts]
export namespace Foo {
    export let bar: () => void;
}
```

```ts [main.ts]
import { Foo } from "bootsharp";

Foo.bar = () => {};
```

:::

## Event Declarations

`[JSEvent]` methods will be emitted as objects with `subscribe` and `unsubscribe` methods:

::: code-group

```csharp [Foo.cs]
public class Foo
{
    [JSEvent]
    public static partial void OnBar (string payload);
}
```

```ts [bindings.d.ts]
export namespace Foo {
    export const onBar: Event<[string]>;
}
```

```ts [main.ts]
import { Foo } from "bootsharp";

Foo.onBar.subscribe(pyaload => {});
```

:::

## Type Crawling

Bootsharp will crawl types from the interop signatures and mirror them in the emitted declarations. For example, if you have a custom record with property of another custom record implementing a custom interface, both records and the interface will be emitted:

::: code-group

```csharp [Foo.cs]
public interface IFoo { };
public record Foo : IFoo;
public record Bar (Foo foo);

public partial class Foo
{
    [JSFunction]
    public static partial Bar GetBar();
}
```

```ts [bindings.d.ts]
export interface IFoo {}
export interface Foo implements IFoo {}
export interface Bar {foo: Foo;}

export namespace Foo {
    export function getBar(): Bar;
}
```

:::

## Configuring Type Mappings

You can override which type declaration are generated for associated C# types via `Type` patterns of [emit preferences](/guide/emit-prefs).


# emit-prefs.md
# Emit Preferences

Use `[JSPreferences]` assembly attribute to customize Bootsharp behaviour at build time when the interop code is emitted. It has several properties that takes array of `(pattern, replacement)` strings, which are feed to [Regex.Replace](https://docs.microsoft.com/en-us/dotnet/api/system.text.regularexpressions.regex.replace?view=net-6.0#system-text-regularexpressions-regex-replace(system-string-system-string-system-string)) when emitted associated code. Each consequent pair is tested in order; on first match the result replaces the default.

## Space

By default, all the generated JavaScript binding objects and TypeScript declarations are grouped under corresponding C# namespaces; refer to [namespaces](/guide/namespaces) docs for more info.

To customize emitted spaces, use `Space` parameter. For example, to make all bindings declared under "Foo.Bar" C# namespace have "Baz" namespace in JavaScript:

```cs
[assembly: JSPreferences(
    Space = ["^Foo\.Bar\.(\S+)", "Baz.$1"]
)]
```

The patterns are matched against full type name of declaring C# type when generating JavaScript objects for interop methods and against namespace when generating TypeScript syntax for C# types. Matched type names have the following modifications:

- interfaces have first character removed
- generics have parameter spec removed
- nested type names have `+` replaced with `.`

## Type

Allows customizing generated TypeScript type syntax. The patterns are matched against full C# type names of interop method arguments, return values and object properties.

## Event

Used to customize which C# methods should be transformed into JavaScript events, as well as generated event names. The patterns are matched against C# method names declared under `[JSImport]` interfaces. By default, methods starting with "Notify..." are matched and renamed to "On...".

## Function

Customizes generated JavaScript function names. The patterns are matched against C# interop method names.


# events.md
# Events

To make a C# method act as event broadcaster for JavaScript consumers, annotate it with `[JSEvent]` attribute:

```csharp
[JSEvent]
public static partial void OnSomethingChanged (string payload);
```

â€” and consume it from JavaScript as follows:

```ts
Program.onSomethingChanged.subscribe(handleSomething);
Program.onSomethingChanged.unsubscribe(handleSomething);

function handleSomething(payload: string) {

}
```

When the method in invoked in C#, subscribed JavaScript handlers will be notified. In TypeScript the event will have typed generic declaration corresponding to the event arguments.

## Events in Interop Interfaces

To make a method in an [interop interface](/guide/interop-interfaces) act as event broadcaster, make its name start with "Notify". Such methods will be detected by Bootsharp and exposed to JavaScript as events with "Notify" changed to "On". For example, `NotifyUserUpdated` C# method will be exposed as `OnUserUpdated` JavaScript event.

Which interface methods are considered events and the way they are named in JavaScript can be customized with [emit preferences](/guide/emit-prefs).

## React Event Hooks

Below are sample React utility hooks, which you may find useful:

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

The `useEventState` hook will take care of both subscribing and unsubscribing from the dotnet event when component unmounts and using last event args as the default state to catch up in case the component missed a broadcast before being mounted.

```tsx
const SomeComponent = () => {
    const payload = useEventState(Program.onSomethingChanged);
    return <>{payload}</>;
};
```


# getting-started.md
# Getting Started

## Configure C# Project

In `.csproj` file, set wasm runtime identifier and reference Bootsharp package:

```xml

<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Bootsharp" Version="*-*"/>
    </ItemGroup>

</Project>
```

## Author Interop APIs

Specify interop surface in the C# project.

```cs
using System;
using Bootsharp;

public static partial class Program
{
    public static void Main ()
    {
        OnMainInvoked($"Hello {GetFrontendName()}, .NET here!");
    }

    [JSEvent] // Used in JS as Program.onMainInvoked.subscribe(..)
    public static partial void OnMainInvoked (string message);

    [JSFunction] // Set in JS as Program.getFrontendName = () => ..
    public static partial string GetFrontendName ();

    [JSInvokable] // Invoked from JS as Program.GetBackendName()
    public static string GetBackendName () => Environment.Version;
}
```

::: info NOTE
Authoring interop via static methods is impractical for large API surfacesâ€”it's shown here only as a simple way to get started. For real projects, consider using [interop interfaces](/guide/interop-interfaces) instead.
:::

## Compile ES Module

Run following command under the solution root:

```sh
dotnet publish
```

â€” which will produce `bin/bootsharp` directory with the following content:

| Name         | Type   | Description                                               |
|--------------|--------|-----------------------------------------------------------|
| types        | folder | Contains type declarations for the authored interop APIs. |
| index.mjs    | file   | The compiled ES module with embedded binaries.            |
| package.json | file   | NPM package manifest for convenient importing.            |

## Consume C# APIs in JavaScript

Import the compiled ES module, assign imported functions, boot the runtime and use exported methods:

::: code-group

```js [JavaScript Runtime (Node, Deno, Bun)]
// Importing compiled ES module.
import bootsharp, { Program } from "./bin/bootsharp/index.mjs";

// Binding 'Program.GetFrontendName' import invoked in C#.
Program.getFrontendName = () => process.version;

// Subscribing to 'Program.OnMainInvoked' C# event.
Program.onMainInvoked.subscribe(console.log);

// Initializing dotnet runtime and invoking entry point.
await bootsharp.boot();

// Invoking 'Program.GetBackendName' C# method.
console.log(`Hello ${Program.getBackendName()}!`);
```

```html [Web Browser]
<!DOCTYPE html>

<script type="module">

    // Importing compiled ES module.
    import bootsharp, { Program } from "./bin/bootsharp/index.mjs";

    // Binding 'Program.GetFrontendName' import invoked in C#.
    Program.getFrontendName = () => "Browser";

    // Subscribing to 'Program.OnMainInvoked' C# event.
    Program.onMainInvoked.subscribe(console.log);

    // Initializing dotnet runtime and invoking entry point.
    await bootsharp.boot();

    // Invoking 'Program.GetBackendName' C# method.
    console.log(`Hello ${Program.getBackendName()}!`);

</script>
```

:::

## Run the App

Assuming the above code is in `main.mjs` file for JavaScript runtimes or in `index.html` file for browser, run the following to test the app:

::: code-group

```sh [Node]
node main.mjs
```

```sh [Deno]
deno run main.mjs
```

```sh [Bun]
bun main.mjs
```

```sh [Browser]
npx serve
```

:::

::: tip EXAMPLE
Find full sources of the minimal sample on GitHub: https://github.com/elringus/bootsharp/tree/main/samples/minimal.
:::


# index.md
# Introduction

## What?

Bootsharp is a solution for building web applications where the domain logic is authored in .NET C# and consumed by a standalone JavaScript or TypeScript project.

## Why?

C# is a popular choice for building maintainable software with complex domain logic, especially in enterprise and financial systems. However, its frontend capabilities are limitedâ€”particularly when compared to what the web ecosystem offers.

The web platform is the industry standard for modern UI development. Frameworks such as [React](https://react.dev) and [Svelte](https://svelte.dev) provide exceptional tooling, fast iteration, and a vast ecosystem, enabling developers to build high-quality interfaces with ease.

Solutions like [Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) attempt to bring the entire web platform into .NET, effectively reversing the natural workflow and restricting access to native JavaScript tools. Bootsharp takes the opposite approach: it enables high-level interoperation between C# and TypeScript, so each layer can be developed within its optimal environment.

With Bootsharp, you implement domain logic in C# and build the UI using familiar web technologies, then connect them seamlessly. Your project can be published to the web or bundled as a native desktop or mobile application using [Electron](https://electronjs.org) or [Tauri](https://tauri.app).

## How?

Bootsharp is installed as a [NuGet package](https://www.nuget.org/packages/Bootsharp) into the C# project dedicated to building the solution for the web. It is specifically designed not to "leak" the dependency outside the entry assembly of the web targetâ€”essential for keeping the domain clean of any platform-specific details.

While it's possible to author both export (C# â†’ JS) and import (C# â† JS) bindings via static methods, complex solutions benefit from interface-based interop. Simply provide Bootsharp with C# interfaces describing the export and import API surfaces, and it will automatically generate the associated bindings and type declarations.

![](/img/banner.png)

Bootsharp will automatically build and bundle the JavaScript package when publishing the C# solution, and generate a `package.json`, allowing you to reference the entire C# solution as any other ES module in your web project.

::: code-group
```jsonc [package.json]
"scripts": {
    // Compile C# solution into ES module.
    "compile": "dotnet publish backend"
},
"dependencies": {
    // Reference C# solution module.
    "backend": "file:backend"
}
```
:::

::: code-group
```ts [main.ts]
// Import C# solution module.
import bootsharp, { Backend, Frontend } from "backend";

// Boot C# WASM module.
await boosharp.boot();

// Subscribe to C# event.
Frontend.onUserChanged.subscribe(updateUserUI);

// Invoke C# method.
Backend.addUser({ name: "Carl" });
```
:::


# interop-instances.md
# Interop Instances

When an interface is supplied as argument or return type of an interop method, instead of serializing it as value, Bootsharp will instead generate an instance binding, eg:

```csharp
public interface IExported { string GetFromCSharp (); }
public interface IImported { string GetFromJavaScript (); }

public class Exported : IExported
{
    public string GetFromCSharp () => "cs";
}

public static partial class Factory
{
    [JSInvokable] public static IExported GetExported () => new Exported();
    [JSFunction] public static partial IImported GetImported ();
}

var imported = Factory.GetImported();
imported.GetFromJavaScript(); //returns "js"
```

```ts
import { Factory, IImported } from "bootsharp";

class Imported implements IImported {
    getFromJavaScript() { return "js"; }
}

Factory.getImported = () => new Imported();

const exported = Factory.getExported();
exported.getFromCSharp(); // returns "cs"
```

Interop instances are subject to the following limitations:
- Can't be args or return values of other interop instance method
- Can't be args of events
- Interfaces from "System" namespace are not qualified


# interop-interfaces.md
# Interop Interfaces

Instead of manually authoring a binding for each method, let Bootsharp generate them automatically using the `[JSImport]` and `[JSExport]` assembly attributes.

For example, say we have a JavaScript UI (frontend) that needs to be notified when data is mutated in the C# domain layer (backend), so it can render the updated state. Additionally, the frontend may have a setting (e.g., stored in the browser cache) to temporarily mute notifications, which the backend needs to retrieve. You can create the following interface in C# to describe the expected frontend APIs:

```csharp
interface IFrontend
{
    void NotifyDataChanged (Data data);
    bool IsMuted ();
}
```

Now, add the interface type to the JS import list:

```csharp
[assembly: JSImport([
    typeof(IFrontend)
])]
```

Bootsharp will automatically implement the interface in C#, wiring it to JavaScript, while also providing you with a TypeScript spec to implement on the frontend:

```ts
export namespace Frontend {
    export const onDataChanged: Event<[Data]>;
    export let isMuted: () => boolean;
}
```

Now, say we want to provide an API for the frontend to request a mutation of the data:

```csharp
interface IBackend
{
    void AddData (Data data);
}
```

Export the interface to JavaScript:

```csharp
[assembly: JSExport([
    typeof(IBackend)
])]
```

This will generate the following implementation:

```csharp
public class JSBackend
{
    private static IBackend handler = null!;

    public JSBackend (IBackend handler)
    {
        JSBackend.handler = handler;
    }

    [JSInvokable]
    public static void AddData (Data data) => handler.AddData(data);
}
```

â€” which will produce the following spec to be consumed on the JavaScript side:

```ts
export namespace Backend {
    export function addData(data: Data): void;
}
```

To make Bootsharp automatically inject and initialize the generated interop implementations, use the [dependency injection](/guide/extensions/dependency-injection) extension.

::: tip Example
Find an example of using interop interfaces in the [React sample](https://github.com/elringus/bootsharp/tree/main/samples/react).
:::


# llvm.md
# NativeAOT-LLVM

Starting with v0.6.0 Bootsharp supports .NET's experimental [NativeAOT-LLVM](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-LLVM) backend.

By default, when targeting `browser-wasm`, .NET is using the Mono runtime, even when compiled in AOT mode. Compared to the modern NativeAOT (previously CoreRT) runtime, Mono's performance is lacking in speed, binary size and compilation times. NativeAOT-LLVM backend not only uses the modern runtime instead of Mono, but also optimizes it with the [LLVM](https://llvm.org) toolchain, further improving the performance.

Below is a benchmark comparing interop and compute performance of various languages and .NET versions compiled to WASM to give you a rough idea on the differences:

![](/img/llvm-bench.png)

â€” sources of the benchmark are here: https://github.com/elringus/bootsharp/tree/main/samples/bench.

## Setup

Use following `.csproj` as a reference for enabling NativeAOT-LLVM with Bootsharp:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <!-- Notice '-browser' postfix. -->
        <TargetFramework>net9.0-browser</TargetFramework>
        <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
        <!-- Let Bootsharp know you're using the LLVM backend. -->
        <BootsharpLLVM>true</BootsharpLLVM>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Bootsharp" Version="*-*"/>
    </ItemGroup>

    <!-- Below are properties required to enable LLVM backend. -->
    <!-- Due to experimental nature of the project, specifics may change over time. -->

    <PropertyGroup>
        <PublishTrimmed>true</PublishTrimmed>
        <DotNetJsApi>true</DotNetJsApi>
        <DebugType>none</DebugType>
        <EmccFlags>$(EmccFlags) -O3</EmccFlags> <!-- optimize speed; use -Oz for min. size -->
        <UsingBrowserRuntimeWorkload>false</UsingBrowserRuntimeWorkload>
        <RestoreAdditionalProjectSources>
            https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json;
        </RestoreAdditionalProjectSources>
    </PropertyGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="10.0.0-*"/>
        <PackageReference Condition="'$([MSBuild]::IsOSPlatform(&quot;Windows&quot;))' == 'true'" Include="runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="10.0.0-*"/>
        <PackageReference Condition="'$([MSBuild]::IsOSPlatform(&quot;Windows&quot;))' == 'false'" Include="runtime.linux-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="10.0.0-*"/>
        <EmscriptenEnvVars Include="DOTNET_EMSCRIPTEN_LLVM_ROOT=$(EmscriptenUpstreamBinPath)"/>
        <EmscriptenEnvVars Include="DOTNET_EMSCRIPTEN_BINARYEN_ROOT=$(EmscriptenSdkToolsPath)"/>
        <EmscriptenEnvVars Include="DOTNET_EMSCRIPTEN_NODE_JS=$(EmscriptenNodeBinPath)node$(ExecutableExtensionName)"/>
        <EmscriptenEnvVars Include="EM_CACHE=$(EmscriptenCacheSdkCacheDir)"/>
    </ItemGroup>

</Project>
```

## Binaryen

Optionally, you can further optimize the produced WASM using Binaryen:

1. Install the tool https://github.com/WebAssembly/binaryen
2. Make sure `wasm-opt` is in the system path
3. Add `<BootsharpOptimize>speed</BootsharpOptimize>` to the project config to optimize for speed; replace `speed` with `size` to instead optimize for size


# namespaces.md
# Namespaces

Bootsharp maps generated binding APIs based on the name of the associated C# types. The rules are a bit different for static interop methods, interop interfaces and types.

## Static Methods

Full type name (including namespace) of the declaring type of the static interop method is mapped into JavaScript object name:

```csharp
class Class { [JSInvokable] static void Method() {} }
namespace Foo { class Class { [JSInvokable] static void Method() {} } }
namespace Foo.Bar { class Class { [JSInvokable] static void Method() {} } }
```

```ts
import { Class, Foo } from "bootsharp";

Class.method();
Foo.Class.method();
Foo.Bar.Class.method();
```

Methods inside nested classes are treated as if they were declared under namespace:

```csharp
namespace Foo;

public class Class
{
    public class Nested { [JSInvokable] public static void Method() {} }
}
```

```ts
import { Foo } from "bootsharp";

Foo.Class.Nested.method();
```

## Interop Interfaces

When generating bindings for [interop interfaces](/guide/interop-interfaces), it's assumed the interface name has "I" prefix, so the associated implementation name will have first character removed. In case interface is declared under namespace, it'll be mirrored in JavaScript.

```csharp
[JSExport([
    typeof(IExported),
    typeof(Foo.IExported),
    typeof(Foo.Bar.IExported),
])]

interface IExported { void Method(); }
namespace Foo { interface IExported { void Method(); } }
namespace Foo.Bar { interface IExported { void Method(); } }
```

```ts
import { Exported, Foo } from "bootsharp";

Exported.method();
Foo.Exported.method();
Foo.Bar.Exported.method();
```

## Types

Custom types referenced in API signatures (records, classes, interfaces, etc) are declared under their respective namespace when they have one, or under root otherwise.

```csharp
public record Record;
namespace Foo { public record Record; }

partial class Class
{
    [JSFunction]
    public static partial Record Method(Foo.Record r);
}
```

```ts
import { Class, Record, Foo } from "bootsharp";

Class.method = methodImpl;

function methodImpl(r: Record): Foo.Record {

}
```

## Configuring Namespaces

You can control how namespaces are generated via `Space` patterns of [emit preferences](/guide/emit-prefs).


# serialization.md
# Serialization

Most simple types, such as numbers, booleans, strings, arrays (lists) and promises (tasks) of them are marshalled in-memory when crossing the C# <-> JavaScript boundary. Below are some of the natively-supported types (refer to .NET docs for the [full list](https://learn.microsoft.com/en-us/aspnet/core/blazor/javascript-interoperability/import-export-interop)):

| C#       | JavaScript | Task of | Array of |
|----------|------------|:-------:|:--------:|
| bool     | boolean    |   âœ”ï¸    |    âŒ     |
| byte     | number     |   âœ”ï¸    |    âœ”ï¸    |
| char     | string     |   âœ”ï¸    |    âŒ     |
| string   | string     |   âœ”ï¸    |    âœ”ï¸    |
| int      | number     |   âœ”ï¸    |    âœ”ï¸    |
| long     | BigInt     |   âœ”ï¸    |    âŒ     |
| float    | Number     |   âœ”ï¸    |    âŒ     |
| DateTime | Date       |   âœ”ï¸    |    âŒ     |

When a value of non-natively supported type is specified in an interop API, Bootsharp will attempt to de-/serialize it with [System.Text.JSON](https://learn.microsoft.com/en-us/dotnet/api/system.text.json) using fast source-generation mode. The whole process is encapsulated under the hood on both the C# and JavaScript sides, so you don't have to manually author generator hints or specify `[MarshallAs]` attributes for each value:

```csharp
public record User (long Id, string Name, DateTime Registered);

[JSInvokable]
public static void AddUser (User user) { }

[JSEvent]
public static partial void OnUserModified (User user);
```

â€” Bootsharp will automatically emit C# and JavaScript code required to de-/serialize `User` record on both ends, so that you can consume the APIs as if they were initially authored in JavaScript:

```ts
import { Program } from "bootsharp";

Program.addUser({ id: 17, name: "Carl", registered: Date.now() });

Program.onUserModified.subscribe(handleUserModified);

function handleUserModified(user: Program.User) { }
```

## Enums Serialization

Enums are marshalled as numbers for better performance, while additional name <-> index mappings are emitted on the JavaScript side for convenience.

```csharp
public enum Options { Foo, Bar }

[JSInvokable]
public static Options GetOption () => Options.Bar;
```

â€” while "GetOptions" return value will be passed to JavaScript as an integer index, Bootsharp will map enum indexes to string values (and vice-versa) in the emitted code, so that following will work as expected:

```ts
import { Program } from "bootsharp";

const option = Program.getOption();
console.log(option === Program.Options.Foo); // false
console.log(option === Program.Options.Bar); // true
console.log(Program.Options[Program.Options.Foo]); // "Foo"
console.log(Program.Options[1]); // "Bar"
```

## Dictionary Serialization

ES6 [Map](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Map) doesn't natively support JSON serialization, hence Bootsharp will use plain objects when serializing C# dictionaries:

```csharp
[JSInvokable]
public static Dictionary<string, bool> GetMap () =>
    new () { ["foo"] = true, ["bar"] = false };
```

â€” the dictionary can be accessed via keys as usual JavaScript object:

```ts
import { Program } from "bootsharp";

const map = Program.getMap();
console.log(map.foo); // true
console.log(map["bar"]); // false
```

## Collection Interfaces

It's common to use various collection interfaces, such as `IReadOnlyList` or `IReadOnlyDictionary` when authoring C# APIs. Bootsharp will accept any kind of array or dictionary compatible interface in the interop APIs and marshal them as plain arrays and maps by default:

```csharp
[JSInvokable]
public static IReadOnlyDictionary<string, float> Map (
    IReadOnlyList<string> a, IReadOnlyCollection<float> b) { }
```

```ts
import { Program } from "bootsharp";

const map = Program.map(["foo", "bar"], [0, 7]);
console.log(map.bar); // 7
```


# sideloading.md
# Sideloading Binaries

By default, Bootsharp build task will embed project's DLLs and .NET WASM runtime to the generated JavaScript module. While convenient and even required in some cases (eg, for VS Code web extensions), this also adds about 30% of extra size due to binary -> base64 conversion of the embedded files.

To disable the embedding, set `BootsharpEmbedBinaries` build property to false:

```xml

<PropertyGroup>
    <BootsharpEmbedBinaries>false</BootsharpEmbedBinaries>
</PropertyGroup>
```

The `dotnet.wasm` and solution's assemblies will be emitted in the build output directory. You will then have to provide them when booting:

```ts
const resources = {
    wasm: Uint8Array,
    assemblies: [{ name: "Foo.wasm", content: Uint8Array }],
    entryAssemblyName: "Foo.dll"
};
await dotnet.boot({ resources });
```

â€” this way the binary files can be streamed directly from server to optimize traffic and initial load time.

Alternatively, set `root` property of the boot options and Bootsharp will automatically fetch the resources form the specified URL:

```ts
// Assuming the resources are stored in "bin" directory under website root.
await backend.boot({ root: "/bin" });
```

::: tip EXAMPLE
Find sideloading example in the [React sample](https://github.com/elringus/bootsharp/blob/main/samples/react).
:::




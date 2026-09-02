# Motely.Wasm

Two hosts. Motely.dll has no Bootsharp.

`import { Search, Analyze } from "motely-wasm"`

**Jimmolate** is `MotelyIndividualSeedSearcher`: JS defines `(ctx) => score` and **binds it before `boot()`**. `ctx` is the live `MotelySingleSearchContext` (specialization). Not a seed string.

```js
Search.jimmolate = (ctx) =>
  ctx.getAnteFirstVoucher(1) === /* MagicTrick */ 1 ? 1 : 0;
await bootsharp.boot();
await Search.jimmolateList(["ALEEB", "PIROCKS"]);
```

`Search.scoreList(jaml, seeds)` is JAML list search. `Analyze.seeds(jaml)` is Jamlyzer. Both take JAML text — `JamlConfig` is a class and does not cross.

`[RenameModule] => index`. `[RenameNode]` erases `Boot`, `Names`, specialization Import/Export proxies. Docs: `D:\bootsharp\docs\guide`.

```sh
dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release
```

**Always `-c Release`.** Bootsharp Release is NativeAOT-LLVM (`D:\bootsharp\docs\guide\llvm.md`, getting-started.md). `-c Debug` is Mono: fat, slow, not the module. Do not omit the flag.

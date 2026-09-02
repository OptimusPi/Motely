# Motely — agent notes

Do not invent Bootsharp. Docs and source live at **`D:\bootsharp`**. Read `docs/guide/serialization.md`, `interop-modules.md`, `interop-instances.md`, `specialization.md`, `renaming.md`, `llvm.md` before touching `Motely.Wasm`. Web-searching bootsharp.com wastes a context window.

**Bootsharp version is 0.9.0.** Not `0.9.0-motely.*`, not `*-*`. If inspect/codegen is red, patch **`D:\bootsharp` in the open** and say so. Do not ship a marshal lie.

Inspect ALC patch (open): `D:\bootsharp` `InspectionContext.Load` probes InspectedDirectory for user-assembly neighbours. GenerateJS filters inspect by `.wasm`; `VYaml.Annotations` has no wasm so 0.9.0 stock `GetExportedTypes` FileNotFounds. `Motely.Wasm/Directory.Build.targets` points `BsPublishAssembly` at the local Release build of that project. NuGet 0.9.0 package is not overwritten. Inspect, not a marshal fake.

Renamers (docs/guide/renaming.md): `[RenameModule] => index`. `[RenameNode]` null erases. Erase `Boot`, `Names`, `SpecializedImport`/`SpecializedExport` proxies. JS sees `Search`, `Analyze`, `MotelySingleSearchContext`.

Paid filesystem extension source: **`D:\extra`** (`Bootsharp.FileSystem`). Not on nuget.org — rewaffle, user-level nuget.config. Do not wire it into Motely.Wasm unless the ticket says so.

**Motely.dll has zero Bootsharp.** No `Bootsharp.Common` on the engine. `[Export]` / `[Import]` live only in `Motely.Wasm` (`Search.cs`, `Analyze.cs`). `Program.cs` is Boot + Names only.

## Bootsharp marshal (law)

From those docs, not from Motely comments:

- **Records / structs / read-only collections** serialize **by value** (binary). JS sees plain objects / arrays / `Map`.
- **Classes and interfaces** are **interop instances** (by ref). Do not put `JamlConfig` or `IJamlClause` on `[Export]` — they are mutable/interface soup.
- **Tasks** of marshalled values are **Promises**. `Task<ScoreRun>` (record) is `Promise<ScoreRun>`. There is no need for a `takeRun` parking slot. That slot is a Motely invention. Old published `motely-wasm@25.0.3` already returned `Promise<Array<…>>`.
- Native in-memory marshal: numbers, bool, string, arrays/lists of some of those, and tasks of those. Everything else in an interop signature goes through Bootsharp serialization if it has immutable semantics.

## WASM head

- Two hosts: `Search` (finds) and `Analyze` (Jamlyzer). `[RenameModule] => "index"`. `Boot` erased. **Not** `Program.`.
- Byref never crosses: specialization in `MotelySingleSearchContextSpecialization.cs` / `BoundarySpecializations.cs` (`docs/guide/specialization.md`).
- Do not `[assembly: Export(typeof(IMotelySearchSettings))]` — kitchen + ref structs; Bootsharp emits illegal C#.
- Claude deleted that rail in `c3d90176` and grew `MotelyWasmApi`. Do not restore the condom. Do not restore the 381-line `MotelySearch` static kitchen either — two hosts, specialization, renamers.
- Publish: `dotnet publish Motely.Wasm/Motely.Wasm.csproj -c Release`. Always `-c Release`. That is NativeAOT-LLVM. `-c Debug` is Mono — do not. Do not omit the flag. Module: `Motely.Wasm/bin/motely-wasm`. npm version tracks `MotelyVersion`.

## Other

- `JamlFilters/` is the operator’s filter folder, not a test fixture. Tests use `Motely.Tests/GoldenJamlFiles` (folder name leftover).
- Auto cutoff engages per **search batch**: raw matches this batch vs seeds this batch. Not milliseconds. Not a magic 2000/sec.
- One result path: scored. `seed,score` always. No `hasStructuredScores`.

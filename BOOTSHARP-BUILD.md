# Bootsharp Local Build Process

Run these steps IN ORDER every time Elringus pushes upstream changes or you need a fresh local build.

---

## 1. D:\bootsharp (core package)

Elringus **force-pushes** `feat/overload`. Never merge. Never stash. Always reset hard.

```powershell
git -C D:\bootsharp fetch
git -C D:\bootsharp reset --hard origin/feat/overload

# Reapply the projectability patch (GlobalType.cs + TypeInspector.cs)
# Check label first — stash index shifts if GitHub Desktop adds entries:
git -C D:\bootsharp stash list
git -C D:\bootsharp stash apply stash@{N}
# Look for: "On feat/overload: !!GitHub_Desktop<feat/overload>"
```

Bump version in `D:\bootsharp\src\cs\Directory.Build.props`:
```xml
<Version>0.8.0-alpha.NNN</Version>   <!-- increment NNN by 1 -->
```

Build JS then pack C#:
```powershell
npm --prefix D:\bootsharp\src\js run build
bash D:\bootsharp\src\cs\.scripts\pack.sh   # outputs to D:\bootsharp\src\cs\.nuget\
```

Optional E2E (slow, catches regressions in the generator):
```powershell
npm --prefix D:\bootsharp\src\js run compile-test
npm --prefix D:\bootsharp\src\js run test
```

> **The patch** skips non-projectable interop members (ref structs, byref T&, delegates,
> bare IEnumerable<T>) and drops imported interfaces that have any such member.
> Lives in `Common/Global/GlobalType.cs` and `Common/Inspection/TypeInspector.cs`.

---

## 2. D:\extra\bootsharp (Bootsharp.FileSystem — $100/mo sponsor package)

Pack to local NuGet only. **Do NOT run `publish.sh`** — that pushes to the rewaffle remote feed (Elringus's job).

```powershell
dotnet pack D:\extra\bootsharp\cs --configuration Release --output D:\extra\bootsharp\cs\.nuget
```

Version is auto-generated from the current timestamp (`yyyy.MM.dd.HHmm`).
Check the exact filename produced — you'll need it for the next step:
```powershell
ls D:\extra\bootsharp\cs\.nuget\Bootsharp.FileSystem.*.nupkg
```

It resolves `Bootsharp.Common Version="*-*"` (floating) → picks up the alpha you just packed.

---

## 3. x:\JammySeedFinder\src\MotelyJAML — update pins

Edit `Directory.Packages.props` with the new versions:
```xml
<PackageVersion Include="Bootsharp"            Version="0.8.0-alpha.NNN" />
<PackageVersion Include="Bootsharp.Common"     Version="0.8.0-alpha.NNN" />
<PackageVersion Include="Bootsharp.Inject"     Version="0.8.0-alpha.NNN" />
<PackageVersion Include="Bootsharp.FileSystem" Version="yyyy.MM.dd.HHmm" />
```

---

## 4. Build Motely.Wasm

```powershell
dotnet publish x:\JammySeedFinder\src\MotelyJAML\Motely.Wasm -c Release
```

Success looks like:
```
Bootsharp ES module published at x:\JammySeedFinder\src\MotelyJAML\..\motely-wasm\dist
```

---

## 5. Verify

```powershell
node x:\JammySeedFinder\src\MotelyJAML\Motely.Wasm\motely.test.mjs
node x:\JammySeedFinder\src\MotelyJAML\Motely.Wasm\pack-consumer-smoke.mjs
```

Both must exit clean.

---

## NuGet cache gotcha

If you reuse the same version number, NuGet serves the cached old package. Purge it:
```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp\0.8.0-alpha.NNN"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.common\0.8.0-alpha.NNN"
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\bootsharp.inject\0.8.0-alpha.NNN"
```

---

## Local NuGet feed locations

| Package | Feed path |
|---|---|
| Bootsharp.* (core) | `D:\bootsharp\src\cs\.nuget\` |
| Bootsharp.FileSystem | `D:\extra\bootsharp\cs\.nuget\` |

Both are registered in the user-level `NuGet.Config`.

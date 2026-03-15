# Final Plan — motely-node per node-api-dotnet

**Source:** [Develop a Node.js addon module in C# with .NET Native AOT](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html)

---

## About .NET Native AOT

- .NET 10 SDK required at **build time** (not runtime).
- AOT binaries: 4–10 MB+ per platform.
- No dynamic loading, reflection, or runtime codegen.
- AOT code calls native only (other AOT assemblies, not managed .NET).
- Some .NET APIs incompatible — see [Native AOT docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/).

---

## Enabling AOT (Motely.NodeAddon.csproj)

Per [js-aot-module](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html):

1. **TargetFramework** .NET 10 (`net10.0`).

2. **Publishing properties:**
   ```xml
   <PublishAot>true</PublishAot>
   <PublishNodeModule>true</PublishNodeModule>
   <PublishMultiPlatformNodeModule>true</PublishMultiPlatformNodeModule>
   <PublishDir>$(MSBuildThisFileDirectory)..\motely-node\bin</PublishDir>
   ```
   (Our `PublishDir` points to motely-node/bin; `PublishMultiPlatformNodeModule` puts each RID in `bin/<rid>/`.)

3. **Publish** produces `.node` binary:
   ```bash
   dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r <RID>
   ```

---

## Flow (MotelyJAML layout)

We have C# (Motely.NodeAddon) and JS (motely-node) as sibling folders. Per docs, native module does **not** depend on `node-api-dotnet`; we `require()` the `.node` directly.

### 1. Bump version

Edit `Directory.Packages.props` → `<MotelyVersion>3.1.7</MotelyVersion>` (or next).  
Run: `node sync-version.mjs`

### 2. Build win-x64

```powershell
dotnet publish Motely.NodeAddon/Motely.NodeAddon.csproj -c Release -r win-x64
```

Output: `motely-node/bin/win-x64/Motely.NodeAddon.node`

### 3. Build linux-x64 (Docker, GLIBC 2.35)

`.NET 10` has no `10.0-jammy` image. Use `Dockerfile.linux-node` (Ubuntu 22.04):

```powershell
./build-linux.ps1
# or
./build-linux.sh
```

Output: `motely-node/bin/linux-x64/Motely.NodeAddon.node`

### 4. Pack and publish

```powershell
cd motely-node
npm pack
npm publish
```

### 5. Update JAMMY

```powershell
pnpm add motely-node@<version>
pnpm run build
```

---

## Key refs

- [js-aot-module](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html)
- [MSBuild props](https://microsoft.github.io/node-api-dotnet/reference/msbuild-props.html)
- [Packages & releases](https://microsoft.github.io/node-api-dotnet/reference/packages-releases.html)

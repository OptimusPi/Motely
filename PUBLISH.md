# Publishing motely-node and motely-wasm

**Canonical build workflow:** [docs/BUILD_NPM_PACKAGES_WORKFLOW.md](docs/BUILD_NPM_PACKAGES_WORKFLOW.md) — both packages, one version, every time. Use it for the full build and pack steps.

No Blazor. Use **WebAssembly Browser App** and **dotnet/runtime WASM** docs only (see [Official docs](#official-docs)).

## Checklist before every publish

### motely-node (Node addon package)

1. **motely-node/package.json**
   - No `"motely-node"` (or package name) in `dependencies` or `devDependencies`.
   - No `file:` or `link:` for any published package.
   - `main` / `types` / `exports` point to built artifacts; `files` includes only those artifacts and required assets.

2. **Native addon**
   - Build includes **linux-x64** (for Vercel). Document in README that server deploy is Linux.
   - Run `npm pack` and verify the tarball: no self-refs and no `file:` deps in package.json inside the tarball.

3. **Publish**
   - Bump version in package.json, then `npm publish` from the package directory (e.g. `motely-node/`).

### motely-wasm

- Same principles: no self-reference, no `file:`/`link:` deps; `main`/`types`/`exports`/`files` correct.
- Bump version, then `npm publish` from `motely-wasm/`.

### Prepack

- Ensure **prepack** (or the script that runs before pack/publish) does **not** add or restore any `file:` or self-dependencies.

### Consumer (e.g. JAMMY)

- After publish: bump `motely-node` and `motely-wasm` to the new version in the app’s package.json and run install.
- Never use `file:` or `link:` for these packages (use published versions only).

---

## Official docs

When changing WASM loading, threading, or JS interop, use only these (no Blazor):

| Purpose | Doc | URL |
|--------|-----|-----|
| [JSImport]/[JSExport], getDotnetRuntime | JavaScript interop in .NET WebAssembly | [Microsoft Learn – dotnet-interop](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop?view=aspnetcore-9.0) |
| Hosting .NET in existing JS app (dotnet.js) | WebAssembly Browser App | [Microsoft Learn – wasm-browser-app](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/wasm-browser-app?view=aspnetcore-9.0) |
| WASM features, threading, COOP/COEP | dotnet/runtime features | [features.md](https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md) |
| Node addon Native AOT | node-api-dotnet | [js-aot-module](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html) |

JAMMY also keeps a copy of this list in `docs/MOTELYJAML_OFFICIAL_DOCS.md`.

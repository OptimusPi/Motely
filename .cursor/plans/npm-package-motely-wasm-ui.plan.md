# NPM Package Plan: motely-wasm-ui (new, post–Motely.WASM delete)

**Context:** Motely.WASM was removed ("100% wrong"). You want a new NPM package so users can `npm install motely-wasm-ui` (or similar) and use Motely in the browser or via Node. When you're back, you'll publish the Node NPM package.

---

## 1. Current state

- **Motely.WASM folder:** Deleted.
- **Motely.sln:** Still references `Motely.WASM\Motely.WASM.csproj` — will fail to load until that reference is removed or replaced.
- **.github/workflows/publish-motely-wasm.yml:** Uses `working-directory: Motely.WASM` — will fail until the workflow is updated to the new project/package.
- **Motely.Orchestration.Browser** and **TestWasm** still exist (browser orchestration + test WASM app).

---

## 2. Package name and scope

- **NPM package name:** `motely-wasm-ui` (as you suggested). Alternative: `motely-node` if the first publish is Node-only.
- **Install:** `npm install motely-wasm-ui`
- **Scope:** Decide when back:
  - **Option A – Node addon only:** Package is a Node API for .NET (Native AOT) addon: analyzer/search from Node (CLI, Vercel, Workflows). No browser WASM in this package.
  - **Option B – Browser WASM + optional UI:** New browser WASM project (e.g. `Motely.Browser` or `Motely.WASM` recreated) that builds to `dist/` and is published as `motely-wasm-ui` (library + optional minimal HTML/UI).
  - **Option C – Both:** Two packages: e.g. `motely-node` (Node addon) and `motely-wasm-ui` (browser WASM + UI), or one monorepo package that exposes both entry points.

---

## 3. Cleanup (do first)

1. **Remove Motely.WASM from solution**  
   Edit [Motely.sln](Motely.sln): remove the `Motely.WASM` project entry and its configuration blocks (search for `Motely.WASM` and `F9D6E7A8-C9B0-1234-EF01-567890123456`).
2. **Update or replace publish workflow**  
   Edit [.github/workflows/publish-motely-wasm.yml](.github/workflows/publish-motely-wasm.yml): either point `working-directory` to the new project (e.g. `Motely.Node` or new `Motely.WASM`), or rename the workflow (e.g. `publish-motely-npm.yml`) and use the new package layout. If you start with Node-only, the build is `dotnet publish` in the Node project, then `npm pack` / `npm publish` from that project’s output.

---

## 4. If you go Node addon first (publish Node NPM package)

1. **New project:** e.g. `Motely.Node` (or `Motely.Npm`) in the Motely repo.
2. **SDK and props:** Node API for .NET; `PublishAot=true`, `PublishNodeModule=true`; reference Motely + Motely.Orchestration (no Orchestration.Browser). See [.NET Native AOT for Node.js](https://microsoft.github.io/node-api-dotnet/scenarios/js-aot-module.html) and [node-api-dotnet aot-npm-package example](https://github.com/microsoft/node-api-dotnet/tree/main/examples/aot-npm-package).
3. **Exports:** Thin C# layer with `[JSExport]` that wraps your existing analyzer/search (e.g. same API shape as the old MotelyWasm: `AnalyzeSeed`, `SearchSeedsWithOptions`, etc.).
4. **Package name:** Can still publish as `motely-wasm-ui` or use `motely-node` for the Node addon.
5. **Multi-RID (optional):** For multiple platforms, run `dotnet publish -r <rid>` per RID and combine outputs before `npm pack` (see the aot-npm-package example).

---

## 5. If you add browser WASM again (motely-wasm-ui as browser)

1. **New project:** e.g. `Motely.Browser` or recreate `Motely.WASM` with `net10.0-browser`, `RuntimeIdentifier=browser-wasm`, `WasmMainJSPath`, etc.
2. **Output:** Build produces `_framework/`, wasm, `main.js`; npm package ships these so a host app (or a minimal bundled UI) can load and call the exports.
3. **Package:** `motely-wasm-ui` on npm; consumers `npm install motely-wasm-ui` and use the dist in their app or run a minimal included UI.

---

## 6. Workflow and publish

- **Tag:** e.g. `motely-wasm-ui-v1.0.0` or `motely-node-v1.0.0` (align with package name).
- **Secret:** Keep using `NPM_MOTELYJAML_DEPLOY_KEY` (or create a new one for the new package name).
- **Steps:** Checkout → Setup .NET (and Node if needed) → Build the new project (e.g. `dotnet publish` for Node or browser) → Set version from tag or input → `npm publish` from the correct directory (the one that contains the built package with `package.json`).

---

## 7. README / install

- **Install:** `npm install motely-wasm-ui` (or `motely-node`).
- **Usage:** Short examples for Node (e.g. `require('motely-wasm-ui')` or `import ...`) and, if you add browser, for loading the script and calling the exported API.

---

## 8. Summary checklist (when you're back)

- [ ] Remove Motely.WASM from Motely.sln (or add new project and remove old reference).
- [ ] Decide: Node-only, browser-only, or both; set package name(s).
- [ ] Create new project (Motely.Node and/or new browser WASM project).
- [ ] Update publish workflow (working-directory, name, tag pattern).
- [ ] Test build and `npm pack` locally.
- [ ] Publish: tag + push or workflow_dispatch, then `npm publish`.

When you're back, pick Option A, B, or C and we can implement the exact project and workflow steps for that choice.

# How to Publish motely-wasm to npm (Real NPM Package)

This package lives in the **BalatroSeedOracle** monorepo; C# lives in the **Motely** submodule (`external/Motely`). You keep one repo, one submodule, and publish to npm from GitHub Actions.

---

## 1. One-time setup

### npm account and token

1. Create an account at [npmjs.com](https://www.npmjs.com) if you don’t have one.
2. Create an **Automation** (or **Classic**) token:  
   [npm → Access Tokens → Generate New Token](https://www.npmjs.com/settings/~yourusername/tokens)  
   - Automation: for CI (recommended).  
   - Classic: enable “Automation” or “Publish” if you use that type.
3. In **BalatroSeedOracle** (this repo):  
   **Settings → Secrets and variables → Actions → New repository secret**  
   - Name: `NPM_TOKEN`  
   - Value: paste the token.

### Package name

- Unscoped: `motely-wasm` (if the name is free on npm).
- If taken, use a scope in `package.json`: `"name": "@your-org/motely-wasm"` (and keep `"publishConfig": { "access": "public" }`).

---

## 2. Publish from GitHub Actions

The workflow is **`.github/workflows/publish-motely-wasm.yml`** in the **BalatroSeedOracle** repo.

### Option A: Publish by tag (version from tag)

From your machine (in BalatroSeedOracle repo):

```bash
git tag motely-wasm-v1.0.0
git push origin motely-wasm-v1.0.0
```

The workflow runs, sets package version to `1.0.0`, builds Motely.WASM (using the submodule), and runs `npm publish`. No need to commit a version bump.

### Option B: Publish manually (version from package.json)

1. In `external/Motely/Motely.WASM/package.json`, set `"version": "1.0.1"` (or whatever) and commit.
2. In GitHub: **Actions → Publish Motely WASM to npm → Run workflow**.
3. Optionally fill **Override version** (e.g. `1.0.1`); otherwise the workflow uses the version in `package.json`.

---

## 3. Repo layout (monorepo + submodule)

- **BalatroSeedOracle** = monorepo (this repo).
- **external/Motely** = Git submodule (C# Motely; contains `Motely.WASM`).
- **external/Motely/Motely.WASM** = the NPM package (package.json, build scripts, `dist/` after build).
- The workflow checks out the monorepo **with submodules**, then runs `npm run build` and `npm publish` inside `external/Motely/Motely.WASM`.

So: C# stays in the Motely submodule; you only run and publish the NPM package from the monorepo via the workflow.

---

## 4. After publish

Consumers install and use the bundle without committing the WASM blobs:

```bash
npm install motely-wasm
npx motely-wasm-copy-to-public
```

Then load `/motely-wasm/main.js` in the app (see main README).

---

## 5. Checklist

- [ ] npm account created.
- [ ] `NPM_TOKEN` added as a repo secret in BalatroSeedOracle.
- [ ] Package name `motely-wasm` (or `@your-org/motely-wasm`) set in `package.json`.
- [ ] Push a tag `motely-wasm-v1.0.0` or run the workflow manually to publish.

You’re a real NPM package.

# Build and Publish motely-wasm to npm Today

## 1. One-time: npm token in GitHub

In **BalatroSeedOracle** repo: **Settings → Secrets and variables → Actions** → add secret:

- **Name:** `NPM_TOKEN`
- **Value:** your npm Automation token from https://www.npmjs.com/settings/~yourusername/tokens

## 2. Build and publish

### From GitHub (recommended)

**Option A – tag (sets version from tag):**

```bash
git tag motely-wasm-v1.0.0
git push origin motely-wasm-v1.0.0
```

**Option B – manual run:**  
GitHub → **Actions** → **Publish Motely WASM to npm** → **Run workflow** (optionally set version override).

### From your machine (local publish)

From **BalatroSeedOracle** repo root:

```bash
cd external/Motely/Motely.WASM
npm install
npm run build
```

If `dist/app-bundle/main.js` and `dist/app-bundle/_framework/*.wasm` exist, then:

```bash
npm login
npm publish --access public
```

## 3. If build fails

- **.NET 10 SDK:** workflow uses `DOTNET_VERSION: '10.0.x'`. Install from https://dotnet.microsoft.com/download.
- **AppBundle missing:** `dotnet publish -c Release` must complete; output goes to `bin/Release/net10.0-browser/browser-wasm/AppBundle`. Then `npm run copy:bundle` copies it to `dist/app-bundle`.
- **Submodule:** when using the GitHub workflow, checkout uses `submodules: recursive` so `external/Motely` is populated.

## 4. After publish

```bash
npm install motely-wasm
npx motely-wasm-copy-to-public
```

Then load `/motely-wasm/main.js` in your app (COOP/COEP headers required).

# Motely WASM test site

Smoke-test **`motely-wasm`** and **`motely-wasm-compat`** from the local publish folders (Vite).

```powershell
# From repo root — produces Motely.BrowserWasm/motely-wasm/ and motely-wasm-compat/
dotnet publish Motely.BrowserWasm -c Release

cd Motely.TestWebsite
npm install
npm run dev
```

- `/` loads the canonical `motely-wasm` package.
- `/compat.html` loads minimal `motely-wasm-compat` (same `index.mjs` engine as `motely-wasm`; package omits schema/Monaco — separate page so each calls `bootsharp.boot()` once).

Static output: `npm run build` → `dist/`.

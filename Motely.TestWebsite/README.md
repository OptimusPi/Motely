# Motely WASM test site

**Live:** https://optimuspi.github.io/MotelyJAML/

Smoke-test **`motely-wasm`** and **`motely-wasm-compat`** from the local publish folders (Vite).

```powershell
cd Motely.TestWebsite
npm install
npm run dev
```

- `/` loads the canonical `motely-wasm` package.
- `/compat.html` loads minimal `motely-wasm-compat` (same `index.mjs` engine as `motely-wasm`; package omits schema/Monaco — separate page so each calls `bootsharp.boot()` once).

Static output: `npm run build` → `dist/`.

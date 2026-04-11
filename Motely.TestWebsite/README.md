# Motely WASM — web JAML search (primary browser entry)

**Live:** https://optimuspi.github.io/MotelyJAML/

This Vite app is the **intended place to run JAML search in the browser** after `dotnet publish` on `Motely.BrowserWasm`: sequential, provider (random / keyword / seed list), and aesthetic flows all go through **`MotelyJamlSearchBuilder`** in WASM — no `Motely.CLI`. The CLI remains useful for batch jobs, scripting, and native throughput; it is not the product surface for “plain C# in the tab.”

**Why this isn’t Avalonia-to-web:** frameworks that ship their own UI-to-WASM pipeline (e.g. Avalonia browser targets) compile **one** UI stack end-to-end. Motely is a **library** embedded in a JS host; **Bootsharp** (or similar) is the boundary tax unless you adopt a single vendor stack that owns UI + runtime. That’s the tradeoff, not a failure of “modern C#.”

```powershell
cd Motely.TestWebsite
npm install
npm run dev
```

- `/` — full `motely-wasm` package: seed explorer + JAML search tabs (`main.js`).
- `/compat.html` — minimal `motely-wasm-compat` smoke (separate `boot()` so the two packages are not mixed in one page).

Static output: `npm run build` → `dist/`.

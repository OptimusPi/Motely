# Seed Finder · `jaml-ui` example

End-to-end Vite app that:

1. Boots `motely-wasm` at startup (top-level await, before React mounts).
2. Mirrors `node_modules/motely-wasm/bin` into `public/motely-wasm/bin` so the WASM runtime is reachable at `/motely-wasm/bin`.
3. Renders `<JamlIde>` from `jaml-ui` with a starter filter.
4. Runs real searches via the `useSearch` hook and streams results back into the IDE.

That's everything a consuming app needs to do. ~50 lines of code total, no custom boot wrappers.

## Run

```bash
pnpm install
pnpm dev
```

Open the printed `localhost:5173` URL. Hit the "Search" button to run a real motely-wasm search against the starter JAML filter.

## Copy this into your own project

1. Copy `scripts/copy-motely-bin.mjs` into your project's `scripts/` and wire it into `predev`/`prebuild` in your `package.json`. This puts the WASM runtime in the right place at build time.
2. In your entry point (`src/main.tsx` here), `await bootsharp.boot("/motely-wasm/bin")` before `createRoot(...).render(...)`.
3. Use `useSearch` from `jaml-ui` and feed the results into `<JamlIde searchResults={...} />`. See `src/App.tsx`.

That's it. There is no `MotelyProvider`. There is no asset base URL to wire up — `jaml-ui` bundles its sprites internally.

## Files

| File | What it does |
| ---- | ------------ |
| `src/main.tsx` | Entry — top-level `await bootsharp.boot(...)` |
| `src/App.tsx` | UI — `JamlIde` + `useSearch` |
| `scripts/copy-motely-bin.mjs` | Mirrors `motely-wasm/bin` into `public/` |
| `vite.config.ts` | Stock `@vitejs/plugin-react` config |

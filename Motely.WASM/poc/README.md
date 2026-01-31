# Motely.WASM POC

Test page for the Balatro seed analyzer in the browser.

## Run JAML search in the browser

**One-time setup (or after changing C#/WASM code):**

1. **Build WASM** (from `Motely.WASM`, parent of `poc/`):
   ```bash
   cd external/Motely/Motely.WASM
   dotnet publish -c Release
   ```
   Then copy the AppBundle into `dist` so the POC can use it:
   ```bash
   npm run copy:bundle
   ```
   (Or run `npm run build` in `Motely.WASM` to do both `dotnet publish` and `copy:bundle`.)

2. **Copy bundle into POC** (from `poc/`):
   ```bash
   cd poc
   npm run copy
   ```

3. **Serve with COOP/COEP** (required for WASM threads):
   ```bash
   npm start
   ```
   Opens http://localhost:3333 with the right headers.

4. **In the browser:** Wait for "Ready", paste or edit a JAML filter in the text area, leave **Quick run** checked (or enter a seed list), click **Run search**. Results stream into the table; use **Cancel** to stop.

**Later (no rebuild):** From `poc/` run `npm start` and open http://localhost:3333

## Scripts

| Script | What it does |
|--------|----------------|
| `npm start` | Serve `public/` on port 3333. Use this to run the POC. |
| `npm run serve` | Same as start. |
| `npm run copy` | Copy `../dist/app-bundle` → `public/motely-wasm/`. Run after building in parent. |
| `npm run build` | Build WASM in parent only (does not copy). Then run `npm run copy` and `npm start`. |

## Features

- **Analyze seed** — Seed, deck, stake → JSON analysis.
- **JAML search** — Paste a JAML filter; results stream into the table. Progress and count update live.
- **Quick run** — Checkbox: when seed list is empty, use 200 test seeds so the search finishes quickly.
- **Cancel** — Cancel button stops the search.

## Notes

- Multi-threading needs COOP/COEP; `serve.json` is set up for that.
- Results stream via `MotelyWasmOnProgress`, `MotelyWasmOnResult`, `MotelyWasmOnComplete`.

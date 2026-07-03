# jaml-ui Seed Finder + Jamlyzer Displayer

A standalone web example that uses:

- `motely-wasm` for JAML parsing, seed searching, and per-seed analysis
- `jaml-ui` for rendering Balatro cards, vouchers, bosses, tags, and packs

## Features

- Edit JAML filters in the left panel
- Load / save JAML files via the File System Access API
- Run `searchList`, `searchRandom`, or `searchSequential`
- Click any matching seed to run `MotelyJamlyzer.analyzeSeeds` and display the full ante-by-ante breakdown

## Run

```bash
pnpm install
pnpm dev
```

## Build

```bash
pnpm build
pnpm preview
```

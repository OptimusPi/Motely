# Motely (MotelyJAML)

High-performance Balatro seed search engine using JAML (JSON-Augmented Markup Language) and native C# filters. Vectorized, multi-threaded, and AOT-compilable for desktop and browser.

This repository is the **MotelyJAML** fork (from [tacodiva/Motely](https://github.com/tacodiva/motely)) used by **[Balatro Seed Oracle](https://github.com/OptimusPi/BalatroSeedOracle)** as a **git submodule** for all seed searching. BSO is an Avalonia UI application that AOT compiles to desktop and browser; Motely’s browser WASM (AOT) support unblocks the BSO browser build.

## What’s in this repo

- **Motely** – Core library: JAML/JSON parsing, filter execution, seed analysis, SIMD vectorization
- **Motely.Orchestration** – Search orchestration, native filter executor, batch search
- **Motely.BrowserWasm** – Bootsharp-based .NET browser WASM build (`bin/bootsharp`)
- **motely-wasm** – npm package: JS loader + Vite/Next.js plugins for in-browser search
- **Motely.CLI** – Command-line interface (JAML/JSON filters, seed analysis)
- **Motely.API** – Optional HTTP API and static UIs
- **Motely.TUI** – Terminal UI (optional)
- **JamlFilters/** – Example and test JAML filter files

## Quick start (CLI)

```bash
dotnet run -p Motely.CLI -- --help
```

See project docs and `Motely.CLI` for full CLI options (filters, batch size, output format, etc.).

## Use in Balatro Seed Oracle

Balatro Seed Oracle (BSO) depends on this repo as a submodule at `external/Motely`. After cloning BSO:

```bash
git submodule update --init --recursive
```

BSO’s Avalonia app then uses Motely for:

- Desktop: in-process .NET search (AOT optional)
- Browser: `motely-wasm` (Bootsharp WASM bundle) for in-browser JAML search and seed analysis

## Browser / WASM

- **Motely.BrowserWasm** – Publishes a Bootsharp ES module to `Motely.BrowserWasm/bin/bootsharp/`.
- **motely-wasm** – npm package consumed by React/Next.js/Vite (or any JS app), staged from Bootsharp output (`bootsharp/` and `bootsharp_st/`).

See [motely-wasm/README.md](./motely-wasm/README.md) for installation and Vite/Next.js setup.

## License

See [LICENSE](./LICENSE) in this repository.

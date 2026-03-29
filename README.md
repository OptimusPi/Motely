# Motely (MotelyJAML)

High-performance Balatro seed search engine using JAML (JSON-Augmented Markup Language) and native C# filters. Vectorized, multi-threaded, and AOT-compilable for desktop and browser.

This repository is the **MotelyJAML** fork (from [tacodiva/Motely](https://github.com/tacodiva/motely)) used by **[Balatro Seed Oracle](https://github.com/OptimusPi/BalatroSeedOracle)** as a **git submodule** for all seed searching. BSO is an Avalonia UI application that AOT compiles to desktop and browser; Motely’s browser WASM (AOT) support unblocks the BSO browser build.

## What’s in this repo

- **Motely** – Core library: JAML/JSON parsing, filter execution, seed analysis, SIMD vectorization
- **Motely.Orchestration** – Search orchestration, native filter executor, batch search
- **motely-wasm** – npm package (Bootsharp): JS loader + Vite/Next.js plugins for in-browser search
- **Motely.CLI** – Command-line interface (JAML/JSON filters, seed analysis)
- **Motely.API** – Optional HTTP API and static UIs
- **Motely.TUI** – Terminal UI (optional)
- **Motely.DistributedWorker** – `MotelyWorker` executable: claims blocks from a Jammy/Seed Finder **pool API** and submits results
- **JamlFilters/** – Example and test JAML filter files

## Distributed worker → Jammy pool (`MotelyWorker`)

Connects to **`POST /api/search/helper`** on a deployed Jammy app (e.g. seedfinder.app). The server hands out **one block** (`35^5` seeds) per `action=request`; the worker runs Motely on that block and posts `action=submit`. Durable identity everywhere is the filter UUID (`filterId`).

```bash
# From this repo root
dotnet run --project Motely.DistributedWorker/Motely.DistributedWorker.csproj -- --pool https://www.seedfinder.app
```

Publish (AOT): `dotnet publish Motely.DistributedWorker/Motely.DistributedWorker.csproj -c Release -r linux-x64` (or `win-x64`). Run the `MotelyWorker` binary from the `publish` folder with the same `--pool` flag.

### Options (summary)

| Flag | Meaning |
|------|--------|
| `--pool <url>` | Jammy origin (e.g. `https://www.seedfinder.app`) — required |
| `--threads N` | Motely search threads **per claimed block** (SIMD / parallel search inside one `35^5` block) |
| `--worker-id` | Sent to the server on claim (heartbeats / debugging) |
| `--filter` | Only claim blocks for this `filterId` UUID (optional; default = any active pool work) |
| `--local-db <dir>` | Shared **DuckLake root** for local Parquet-backed results (default: `MotelyData/ducklake`). Use `-` to disable. All filters share one catalog; rows are scoped by `filter_id` in **Motely.DB** |

See **[JAMMY `docs/DISTRIBUTED_SEARCH.md`](https://github.com/OptimusPi/JAMMY/blob/main/docs/DISTRIBUTED_SEARCH.md)** (or your Jammy checkout) for API + DB semantics.

**`--threads`**, **`--worker-id`**, **`--filter`**, **`--local-db`**: additional notes may appear in the **[Jammy README](https://github.com/OptimusPi/JAMMY#community-worker-motelyworker)** (consumer app docs).

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

- **motely-wasm** – Bootsharp-powered npm package consumed by React/Next.js/Vite (or any JS app) for in-browser search.

See [motely-wasm/README.md](./motely-wasm/README.md) for installation and Vite/Next.js setup.

## License

See [LICENSE](./LICENSE) in this repository.

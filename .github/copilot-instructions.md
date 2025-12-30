# MotelyJAML AI Coding Instructions

## Project Overview
MotelyJAML is a high-performance, SIMD-powered Balatro seed searcher. It consists of a C# core engine (`Motely`), an ASP.NET Core API (`Motely.API`), and multiple user interfaces:
- **TUI**: Terminal User Interface built with `Terminal.Gui`.
- **Web UI**: A Vue 3 SPA (`vue-jaml-ui`) hosted by the API.
- **CLI**: Command-line interface for batch searching.

## Core Architecture & Data Flow
- **Motely (Core)**: Uses `Vector256<int>` (SIMD) for massive parallelization of seed searching. Platform-specific logic is handled via `partial class` (e.g., `Motely.Desktop.cs`, `Motely.Browser.cs`).
- **LuaRandom**: A high-performance `struct` implementation of Balatro's PRNG. Avoid allocations when using it.
- **JAML (Joker Ante Markup Language)**: A custom YAML-based format for defining filters. `JamlConfigLoader` (C#) and `jamlUtils.js` (Vue) handle pre-processing and normalization. A JSON schema is available at `jaml.schema.json` for validation and intellisense.
- **Motely.API**: Hosts the search engine and provides SignalR hubs (`/searchHub`) for real-time search progress and results.
- **Vue Web UI**: Uses Vite for building. Communicates with the API via `useApi.js` (fetch) and `useSignalR.js` (SignalR).

## Critical Data Structures
- **`MotelyItem`**: A bit-packed `int` representing any game item (Joker, Tarot, Planet, etc.). Use bitmasks from `Motely.cs` to extract properties like `Type`, `Edition`, `Seal`, and `Enhancement`.
- **`MotelyItemVector`**: The SIMD equivalent of `MotelyItem`. Operations on vectors should be preferred in search loops.
- **`MotelyFilterCreationContext`**: A `ref struct` used to cache PRNG stream keys during filter initialization to avoid redundant string allocations and hash calculations.

## Coding Patterns & Conventions
- **SIMD First**: When modifying search logic, ensure it is vectorized. Use `MotelyItemVector` and `Vector256<int>`.
- **PRNG Keys**: All PRNG keys are defined in `MotelyPrngKeys.cs`. Use the helper methods there instead of hardcoding strings.
- **Filter Implementation**: New filter types should be added to `Motely/filters/MotelyJson/` and mapped in `MotelyJsonConfig.cs`.
- **No Allocations in Search**: The core search loop must be allocation-free. Use `ref struct`, `stackalloc`, and pre-allocated buffers.
- **Vue Composables**: Logic should be encapsulated in composables (`src/composables/`). Use `useApi` for REST and `useSignalR` for real-time updates.

## Developer Workflows
- **Running the App**: `dotnet run` launches the TUI. Use `dotnet run -- --json <name>` for CLI search.
- **Web UI Development**: Run `npm run dev` in `vue-jaml-ui`. It proxies to the API (default: `http://192.168.0.171:3141`).
- **Web UI Build**: `npm run build` outputs to `../wwwroot/JAML`.
- **JAML Schema**: Use `jaml.schema.json` for validation. In `.jaml` files, add `# yaml-language-server: $schema=./jaml.schema.json` at the top for IDE support.
- **Testing**: Use `dotnet test`. We use **Snapshot Testing** via the `Verify` library. If you change output formats, you must update the `.verified.txt` files.

## Adding Filters
1. Define the filter logic in `Motely/filters/`.
2. Add the clause type to `MotelyJsonFilterClauseTypes.cs`.
3. Update `MotelyJsonConfig.cs` to support the new clause in JAML/JSON.
4. Ensure `PostProcess()` in `MotelyJsonConfig.cs` correctly initializes the new filter.
5. Add validation logic in `MotelyJsonConfigValidator.cs`.
6. Update `jamlUtils.js` and `jamlOptions.js` in the Vue UI to support the new filter type in the builder.

## Key Files
- [Motely/Motely.cs](Motely/Motely.cs): Bitmasks and constants for game items.
- [Motely/MotelyPrngKeys.cs](Motely/MotelyPrngKeys.cs): PRNG stream key definitions.
- [Motely/filters/MotelyJson/MotelyJsonConfig.cs](Motely/filters/MotelyJson/MotelyJsonConfig.cs): The central schema for JAML/JSON filters.
- [Motely.API/MotelyApiHost.cs](Motely.API/MotelyApiHost.cs): API endpoint definitions and SignalR setup.

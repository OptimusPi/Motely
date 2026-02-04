# What runs in the browser vs desktop

## Short answer

**Everything that is browser-compatible is used in the browser.** When you build **Motely.WASM** and ship the npm package, the WASM bundle includes:

- **Motely** (core: JAML, filters, analysis, DTOs)
- **Motely.Repository** (seed source/sink abstractions)
- **Motely.Orchestration** (search orchestration, in-memory on browser)
- **Motely.Orchestration.Browser** (browser adapter: `useInMemoryStorage: true`)
- **Motely.WASM** (JS interop, entry point)

So the core logic is shared: same C# code runs on desktop and in the browser. The only difference is *how* it’s hosted (desktop app vs WASM) and *where* results go (file/DuckDB on desktop, in-memory/callbacks or DuckDB-WASM in browser).

---

## Browser-compatible (in the WASM bundle)

| Project | Targets | Role in browser |
|--------|---------|------------------|
| **Motely** | `net10.0`, `net10.0-browser` | JAML, filters, analysis – shared core |
| **Motely.Repository** | `net10.0`, `net10.0-browser` | Seed source/sink – shared |
| **Motely.Orchestration** | `net10.0`, `net10.0-browser` | Search orchestration – shared; on browser uses in-memory only |
| **Motely.Orchestration.Browser** | `net10.0-browser` only | Thin wrapper: calls `MotelySearchOrchestrator.LaunchWithContext(..., useInMemoryStorage: true)` |
| **Motely.WASM** | `net10.0-browser` + RID `browser-wasm` | Host: JS interop (`MotelyWasm.cs`), entry point, npm package |

When you `npm install motely-wasm` and load the WASM module, you are running **all of the above** in the browser. One WASM bundle = Motely + Repository + Orchestration + Orchestration.Browser + Motely.WASM.

---

## Desktop-only (not in the browser)

| Project | Why not in browser |
|--------|---------------------|
| **Motely.DB** | Uses **DuckDB.NET** (native). No `net10.0-browser`; DuckDB in browser is done via **DuckDB-WASM** (JS) in the npm package, not this project. |
| **Motely.CLI** | Console app – desktop only. |
| **Motely.API** | ASP.NET host – server only. |
| **Motely.TUI** | Terminal UI – desktop only. |
| **Motely.GPU** | GPU usage – desktop only. |
| **Motely.MCP** | MCP server – desktop/server. |

These are not referenced by Motely.WASM, so they are not compiled into the WASM bundle.

---

## Why it looks split

- **Motely** and **Motely.Orchestration** multi-target (`net10.0` + `net10.0-browser`) so the same code runs on desktop and in the browser.
- **Motely.Orchestration.Browser** exists so the browser host (Motely.WASM) has a single, browser-only entry that says “run orchestration in-memory, no Motely.DB.”
- **Motely.WASM** is the only project that actually builds a **WASM app** (RID `browser-wasm`) and ships it (npm). It *references* Motely and Orchestration.Browser; it doesn’t duplicate them. Building Motely.WASM pulls in all the browser-compatible projects above into one bundle.

So: **everything in here that is browser-compatible is used in the browser** – it’s all in that one WASM bundle. The rest (DB, CLI, API, TUI, etc.) is desktop/server by design.

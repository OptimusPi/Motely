# What's Next — Motely / JAML Ecosystem

High-level only. No implementation details. One bullet per idea.

---

## Engine (MotelyJAML)

- **Kill MotelyWasmHost.** The constraint that required it is gone on Bootsharp 0.8.x. Export Motely's public surface directly via Bootsharp `[Export]` — no wrapper facade, no condom.
- **JAML POCOs ARE the schema.** `JamlDocument`, `JamlCriterion`, `JamlSources`, `JamlDefaults` — export them, schema falls out of the type system for free. Kill the separate schema generator project.
- **Schema generator → `tools/schemagen.cs`.** Single-file C# 14 app, `dotnet run tools/schemagen.cs`. Not in Motely.Wasm. Not in any .csproj.
- **`await motely.boot()`** — not fire-and-forget `.catch(console.error)`. Everyone ships the lazy version. Don't.
- **Bootsharp.FileSystem** — `fs.init(Motely.FileSystem.FileMounter)` before boot unlocks real file IO in browser and mobile. Seeds from local disk. .jaml files from iCloud on iPhone. DuckDB from a file picker.
- **MotelyVersion stays in `Directory.Packages.props`.** Already correct. Don't touch it.

---

## Packages

- **jaml-ui** — already on 0.22.2 local. Publish it. Then install into all consumers.
- **motely-wasm** — already on 14.4.0 on npm. Bump consumers.
- **Bootsharp local feed** — 0.8.0-alpha.158 + FileSystem.2026.5.1.1716 staged. MotelyJAML needs `Directory.Packages.props` bumped + nuget.config dance + restore.

---

## Consumers

- **D:\mmm (seedfinder.app)** — install motely-wasm@14.4.0 + jaml-ui@0.22.2, deploy.
- **D:\ErraticDeck.app** — same.
- **X:\weejoker.app** — same, direct push no PR gate.

---

## Seed Curator (MCP App)

- **Real MCP App** — `@modelcontextprotocol/ext-apps` v1.7.1, `registerAppTool` + `registerAppResource`, single-file HTML bundled with `vite-plugin-singlefile`. NOT a fake React app.
- **Text fallback required** — always include `content[]` array for CLI hosts that don't negotiate `io.modelcontextprotocol/ui`.
- **Auth** — `io.modelcontextprotocol/oauth-client-credentials` for M2M. pifreak never types a password into the conversation.
- **Target host** — Claude Desktop, not Claude Code CLI. CLI doesn't negotiate the UI extension.

---

## Motely.TUI

- JAML filter input + scrollable seed results. Direct C# call to Motely engine, no WASM, no server, no CDN. Native terminal seed finder.

---

## What NOT to do

- No wrapper facades. No glue layers. No `MotelyWasmHost`.
- No schema generator in Motely.Wasm.
- No file moves/deletes without pifreak consent.
- No editing `D:\bootsharp` or `D:\extra` source to push upstream.
- No fake MCP Apps that v0 will reject.
- No 23-hour sessions. 15 minutes per task.

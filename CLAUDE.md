# CLAUDE.md — MotelyJAML

Motely is a self-contained vectorized (SIMD) seed-search engine for Balatro;
filters are authored in JAML (Jimbo's Ante Markup Language). Everything you need
is in this repo.

See [README.md](README.md) for what this is and how to build; this file covers
only how to work here.

## Bootsharp (Motely.Wasm)

`Motely.Wasm/` is a Bootsharp project. Read these docs before changing it — they
auto-load via the `@` imports below.

Quick reference:
- Bootsharp generates the ES module and `package.json`; `motely-wasm/` is output.
- `BootsharpPublishDirectory` = module, `BootsharpPackageDirectory` = `package.json`,
  `BootsharpBinariesDirectory` = binaries. Binaries dir set → sideloaded (separate
  `.wasm`; `boot()` takes a root URL or `{ wasm }` bytes). Empty → embedded
  (`boot()` takes no args).
- `[RenameNode]` / `[RenameMember]` returning null/empty erases that node/member
  from the generated JS surface.

@d:/bootsharp/docs/guide/index.md
@d:/bootsharp/docs/guide/getting-started.md
@d:/bootsharp/docs/guide/build-config.md
@d:/bootsharp/docs/guide/sideloading.md
@d:/bootsharp/docs/guide/interop-modules.md
@d:/bootsharp/docs/guide/interop-instances.md
@d:/bootsharp/docs/guide/declarations.md
@d:/bootsharp/docs/guide/serialization.md
@d:/bootsharp/docs/guide/specialization.md
@d:/bootsharp/docs/guide/renaming.md
@d:/bootsharp/docs/guide/llvm.md
@d:/bootsharp/docs/guide/extensions/dependency-injection.md
@d:/bootsharp/docs/guide/extensions/file-system.md

## Working agreement (important — the user has been burned by this)

- **Consent first.** Do exactly what is asked, nothing adjacent. When the user
  says stop, stop immediately — no defending, no "let me just finish this."
- **Running:** the only off-limits run is a full ~2.3T seed sweep. Single seed (~100ms),
  `Motely.CLI` with params, C# single-file apps, targeted tests, seed-finding MCP — all
  allowed and encouraged. Don't over-generalize into "never run anything." See
  [docs/running-policy.md](docs/running-policy.md).
- Keep changes scoped and confirm before anything hard to reverse.

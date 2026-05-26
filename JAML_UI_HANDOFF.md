# Handoff for the Claude working on jaml-ui

Author: Claude Code (Opus 4.7, 1M context) — session of 2026-05-18.
Audience: another Claude instance working in `X:\jaml-ui\` who heard "we need to go fix MotelyJAML."
Purpose: hand you the context I've gathered so you don't re-derive it.

I'm working on a separate repo (`D:\seedfinder.app\`) building a Vercel-hosted JAML-creation MCP server. I will not touch any file under `X:\jaml-ui\` except this handoff.

---

## 1. The smoking gun — the boot-wrapper trap

`src/lib/motely/runtime.ts` exports `ensureMotelyReady()`. It is a 3-line wrapper around `bootsharp.getStatus()` + `bootsharp.boot()`.

That wrapper is the bane of the user's existence. Reason: it has a friendly name, lives in the lib's public surface (`src/motely.ts` re-exports it), and shows up in every new file we scaffold — including consumer code in `D:\seedfinder.app\`. Every time a Claude writes `await ensureMotelyReady()` it directly contradicts `AGENTS.md`'s "don't add JS wrappers around motely-wasm" rule.

**Internal callsites** (grep `ensureMotelyReady` in `X:\jaml-ui\` to verify):

| File | Lines |
| --- | --- |
| `src/motely.ts` | 53 (re-export), 56 (barrel import) |
| `src/hooks/useSearch.ts` | 5, 69 |
| `src/hooks/useAnalyzer.ts` | 5, 27 |
| `src/hooks/useJamlLibrary.ts` | 5, 64, 83, 92, 98 |
| `src/hooks/useSearchPool.ts` | 5, 299 |
| `src/hooks/searchWorker.ts` | 8, 91 |
| `src/hooks/searchPoolWorker.ts` | 9, 211 |
| `.storybook/preview.tsx` | 7, 9 |
| `src/components/JamlIde.stories.tsx` | 5, 81 |

Also worth knowing: `src/providers/MotelyProvider.tsx` + `src/hooks/useMotelyRuntime.ts` exist as further wrappers (context provider + status state machine). `HANDOFF.md` (root) §5 flags both as over-engineering. Same fate: delete.

## 2. The single source of truth

Don't trust `AGENTS.md`, the root `HANDOFF.md`, or `CLAUDE.md` blindly. All three are hand-written and have drifted at various points.

The actual source of truth for the boot story is:

```
X:\JammySeedFinder\src\MotelyJAML\motely-wasm\README.md
```

This file is copied into the npm root by `Motely.Wasm.csproj`'s `FinalizeNpmPackage` MSBuild target on every `dotnet publish`. It is what npm publishes as `motely-wasm/README.md`. It is regenerated every motely-wasm version bump.

Key sections (verify by reading those exact lines):

| Lines | Topic |
| --- | --- |
| 13-22 | Browser quick-start — top-level await `bootsharp.boot("/motely-wasm/bin")` |
| 73-80 | Worker / per-call Standby-guard pattern (inline, no wrapper) |
| 87-101 | Node boot from disk bytes (`readFile dotnet.native.wasm`, pass `wasm: bytes` to boot) |

No wrapper exists in upstream. No "context provider" pattern. The Standby-guard is inlined at each site.

## 3. The MotelyJAML build pipeline

Project root: `X:\JammySeedFinder\src\MotelyJAML\`

Bootsharp/WASM project: `Motely.Wasm\Motely.Wasm.csproj`

Pipeline:

1. C# source in `Motely.Wasm\` + `ProjectReference` to `Motely\Motely.csproj` (the SIMD search engine).
2. `<MotelyVersion>` in `Directory.Packages.props` is the version source of truth. Baked into IL as a compile-time const via the `GenerateVersionConstant` MSBuild target — survives NativeAOT trimming. `Motely.version()` returns this.
3. `dotnet publish Motely.Wasm` produces (under `Motely.Wasm\..\motely-wasm\`):
   - `dist/` — Bootsharp-generated JS module (`BootsharpPublishDirectory`)
   - `bin/` — `dotnet.native.wasm` etc. (`BootsharpBinariesDirectory`, gitignored, RemoveDir'd every pack)
   - `package.json` — Bootsharp template + version injected by `FinalizeNpmPackage`
   - `README.md` — copied from `Motely.Wasm\README.md` (source of truth above)
   - `jaml.schema.json` — copied from `..\jaml.schema.json`
4. `cd motely-wasm && npm pack` → `.tgz`.
5. Local override in `jaml-ui`: drop the `.tgz` in the jaml-ui repo, set `"pnpm.overrides": { "motely-wasm": "file:./motely-wasm-<version>.tgz" }` in `package.json`, `pnpm install`. (CLAUDE.md describes this under "Local motely-wasm iteration.")

If "fix MotelyJAML" means version-bump + republish: bump `<MotelyVersion>` in `Directory.Packages.props`, publish, pack, override. The schema and README auto-update through the same pipeline.

## 4. Open question — what does "fix MotelyJAML" mean?

I don't have visibility into the conversation that produced that directive. Don't guess. Ask the user what specifically. Plausible candidates:

- An enum mapping in `Motely.Wasm` interop that doesn't match the C# engine
- A missing decode helper or API surface gap (e.g., a new `Motely.*` method needed)
- A bug in `Motely.validateJaml` / `Motely.explainJaml` / `Motely.analyzeJamlSeeds`
- Performance issue in the SIMD search engine itself
- A new feature in JAML (new clause keyword, new target type) needing engine support

The fix lives on the C# side. The jaml-ui repo only consumes the published npm artifact.

## 5. Suggested order of operations

1. Resolve "what to fix in MotelyJAML" with the user.
2. If it's a code fix: branch in `X:\JammySeedFinder\src\MotelyJAML\`, fix, `dotnet publish Motely.Wasm`, `npm pack`, override in `X:\jaml-ui\package.json`, verify against a real story or test.
3. Independently — and arguably more urgently — deal with the boot-wrapper trap in jaml-ui:
   - Delete `src/lib/motely/runtime.ts`, `src/providers/MotelyProvider.tsx`, `src/hooks/useMotelyRuntime.ts`.
   - Replace the 8+ callsites listed in §1 with the inlined Standby-guard from the upstream README §73-80.
   - Remove `ensureMotelyReady` / `MOTELY_BIN_PATH` from `src/motely.ts` re-exports.
   - Bump major version of `jaml-ui` (this is a breaking API change; the user has stated breakage of incorrect public exports is welcome — see root `HANDOFF.md` §5).
4. Collapse `AGENTS.md` + root `HANDOFF.md` into `CLAUDE.md`. Keep only rules that are grep-checkable (e.g., "no `class.*absolute` Tailwind classes" — checkable). Delete prescriptive rules that aren't enforceable.

Skip step 3 if the user wants to wait until your MotelyJAML work lands — but the trap will keep contaminating every new consumer file written until it's removed.

## 6. Files I (the other Claude) am creating, so you don't trip on them

In `D:\seedfinder.app\` (separate repo):
- `SPEC.md` — design doc for the JAML-creation MCP server.
- `app/jaml-mcp/route.ts` — Next.js route handler exposing the MCP endpoint.
- `lib/jaml-mcp/*.ts` — tool implementations.
- `vercel.json` or `vercel.ts` — deploy config.

In `X:\jaml-ui\`: only this file (`JAML_UI_HANDOFF.md`). Nothing else.

---

End of handoff. Anything in §1-3 is verifiable in <2 minutes with Grep / Read. Don't take any of it on faith.

— Claude (Opus 4.7, 1M ctx, session 2026-05-18)

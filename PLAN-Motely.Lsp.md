# Motely.Lsp — Implementation Plan

## Goal
`Motely.Lsp.exe` — AOT C# executable, JSON-RPC over stdio. Consumers: Claude Code plugin,
VS Code extension, CodeMirror 6 (via WASM), CLI.

## Rule
Types own the grammar. Source generator reads clause declarations + attributes.
Delete: `ClauseKeys`/`SourceKeys` fields, `StaticStringArrayField`, reflection populator,
reflection writer, `[DynamicallyAccessedMembers]` annotations.

---

# VERIFIED FACTS (research 2026-07-16)

### Claude Code `.lsp.json` — confirmed against `/en/plugins-reference.md`
- **Location:** `.lsp.json` at plugin root, **or** inline as `lspServers` in `plugin.json`.
- **Required:** `command` ("The LSP binary to execute (must be in PATH)"), `extensionToLanguage`.
- **Optional:** `args`, `transport` (`stdio` default | `socket`), `env`, `initializationOptions`,
  `settings`, `workspaceFolder`, `startupTimeout`, `shutdownTimeout`, `restartOnCrash` (default `true`),
  `maxRestarts`, `diagnostics` (default `true`).
- **`rootMarkers` — NOT IN DOCS.** Nearest is `workspaceFolder`.
- **`${CLAUDE_PLUGIN_ROOT}` is documented** and substitutes in exactly four LSP fields:
  `command`, `args`, `env`, `workspaceFolder`. Also `${CLAUDE_PLUGIN_DATA}`, `${CLAUDE_PROJECT_DIR}`,
  `${user_config.KEY}`.
- **`bin/` does NOT help.** It is scoped to "the Bash tool's PATH". No documented link to LSP
  `command` resolution. → **Use `${CLAUDE_PLUGIN_ROOT}` absolute path in `command`.**
- **Version gate:** `restartOnCrash` + `shutdownTimeout` need Claude Code ≥ v2.1.205. Before that,
  setting either made Claude Code **skip the server entirely**, visible only in `claude --debug`.
- **Extension collision:** first registered server wins an extension; others never start.
- **Trust gate:** LSP servers start only after workspace is trusted.
- **Bundling:** "You must install the language server binary separately." No per-platform binary
  mechanism documented.

### ⚠️ What Claude Code actually consumes — **NOT completion**
> * **Instant diagnostics**: Claude sees errors and warnings immediately after each edit
> * **Code navigation**: go to definition, find references, and hover information
> * **Language awareness**: type information and documentation for code symbols

**Completion is NOT IN DOCS.** Priority for the Claude Code surface = diagnostics, hover,
definition, references. Completion serves VS Code + CodeMirror only. Plan accordingly.

### ⚠️ OmniSharp.Extensions.LanguageServer — RULED OUT
- Latest **0.19.9, 2023-09-21** (34 months). net6/netstandard only. Repo is bot-maintained;
  zero human feature commits since 2025-01. Issue #150 (drop Newtonsoft) open since 2019.
- Deps: **MediatR** (reflective dispatch), **Newtonsoft.Json** (not AOT-safe),
  **M.E.DependencyInjection** (assembly scanning), System.Reactive.
- **No `[RequiresDynamicCode]`/`[RequiresUnreferencedCode]` annotations** → **no IL2026/IL3050
  warnings**. It publishes clean and fails at runtime. Worst possible failure mode.
- Nobody has AOT-published one; no issues even ask.

### Alternatives
| Option | Verdict |
|---|---|
| **EmmyLua.LanguageServer.Framework** 0.9.2 (2026-04-15) | net8/9, MIT, LSP 3.18, STJ source-gen, **zero deps**, explicit handler registration, author claims AOT-ready. Repo `CppCXY/LanguageServer.Framework`, last push 2026-04-24. **Risk: 31 stars, 8.3k downloads, bus factor 1, pre-1.0.** AOT claim is the author's — **unverified**. |
| **Hand-roll** stdio + STJ source-gen | ~400–800 LOC for framing + JSON-RPC 2.0 envelope + dispatch + lifecycle. AOT-perfect by construction. Real cost is LSP DTOs — but only for features we ship. Zero abandonment risk. |
| StreamJsonRpc 2.25.29 | Microsoft, active, "partially NativeAOT safe" — needs `EnableStreamJsonRpcInterceptors`, `SystemTextJsonFormatter`, custom `JsonSerializerContext`. **Gives JSON-RPC only, zero LSP.** Content-Length framing is ~50 LOC, so marginal value is low. |
| Microsoft.CommonLanguageServerProtocol.Framework (CLaSP) | **Not consumable.** dotnet/roslyn#68696 open since 2023; APIs "not stable or not public". |
| Microsoft.VisualStudio.LanguageServer.Protocol 17.2.8 | **No.** Newtonsoft DTOs, stale. |
| Roslyn.LanguageServer.Protocol | STJ types, but prerelease-only, Roslyn-internal cadence, drags Roslyn. No. |

**No clean stable AOT-safe standalone LSP types package exists.**

### Roslyn generator facts
- **`netstandard2.0` still mandatory** — constraint is the host (VBCSCompiler/VS/Rider), not the SDK.
- SDK 10.0.301 ships Roslyn **5.6.0-2.26270.133**. **Reference `Microsoft.CodeAnalysis.CSharp` 4.14.0**
  (the floor) — `ForAttributeWithMetadataName` landed in 4.3.1. Referencing 5.6.0 buys nothing and
  breaks older hosts.
- **CPM gotcha:** `PrivateAssets="all"` does NOT exempt from CPM — still needs a `PackageVersion`.
  `Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing.XUnit` 1.1.2 drags a higher Roslyn →
  **NU1605/NU1608**, which are **build breaks here** (warnings-as-errors repo-wide). Skip that package.
- **Verify.SourceGenerators 2.5.0 diamond is clean** — needs Verify ≥26.5.0; Verify.Xunit 28.6.0
  brings 28.6.0. net9 assets apply to net10.
- `DiagnosticSeverity.Error` from a generator fails the build unconditionally, independent of
  `TreatWarningsAsErrors`, not `NoWarn`-suppressible.
- **No source generator or analyzer exists in this repo today.** This is the first.

### ⚠️ Generator design constraints (the plan-stompers)
1. **Per-discriminator data is NOT derivable from the type.** `tag`/`smallBlindTag`/`bigBlindTag`
   → same `TagClause`, **different `RollsDefault`**. Attribute must carry it, `AllowMultiple = true`.
2. **Keys ≠ properties, in BOTH directions.**
   - Properties with no key: `JokerClause.Jokers`, `.IsWildcard`, **`.LegendarySources`**.
     A naive property-derived generator **silently widens the accepted grammar.** → `[JamlIgnore]`.
   - Keys with no property: `LogicClause` has `"ante"`/`"antes"` but **no `Antes` property**
     (loader hoists antes into children — deliberate, `LogicClause.cs:8-10`).
     `JamlConfig` accepts `"dateCreated"` with no backing property (intentional metadata).
     → `JAMLGEN001` needs an escape hatch.
3. **`required` flips from bypassed to enforced.** `Activator.CreateInstance` ignores `required`;
   generated `new TagClause { … }` is compile-checked. **11 required members** must be populated or
   the build breaks: `ErraticRankClause.Rank`, `ErraticSuitClause.Suit`, `PlanetCardClause.Planets`,
   `SpectralCardClause.Spectrals`, `TarotCardClause.Tarots`, `BossClause.Bosses`, `TagClause.Tags`/`Rolls`,
   `VoucherClause.Vouchers`/`Rolls`, `JamlConfig.Id`.
4. **`erraticRank`/`erraticRanks`/`erraticSuit` bypass the populator entirely.**
   `JamlConfigLoader.cs:239-289` hand-builds them; **`erraticRanks` fans one discriminator into an
   `OrClause` of N `ErraticRankClause` children with `Min=1`.** Structural transform, not property
   population. → keep a hand-written switch arm the generator defers to.
5. **8 value-array discriminators** (`voucher`/`tarotCard`/`spectralCard`/`planetCard`/`boss`/`tag`/
   `smallBlindTag`/`bigBlindTag`) each assign a different property. `ValueEnum` alone doesn't say
   which → need `[JamlValue]` on the receiving property.
6. **`JamlConfigWriter.cs:221-334` does the same reflection in reverse.** Must convert in the same
   pass or `[DynamicallyAccessedMembers]` can't be deleted and the AOT win doesn't land.
7. **`EnumOrAny.cs` is dead code.** Only file mentioning it; cites a converter and a
   `jaml-schema.cs` that don't exist. Wildcards are actually `JokerClause.IsWildcard : bool`. **Delete.**
8. **Property shapes: exactly 7** (`JamlClausePopulator.cs:115-179`) — `int`, `bool`, `int[]`,
   `string`, `MotelyStandardcardRank?` (uses `ParseRank`, not `Enum.TryParse`), `TEnum?`, `TEnum[]`.
9. **Real key aliases: 2** — `requireMega`→`RequireMegaPack`, `value`→`Mult`. `[JamlKey]` deletes
   `ResolveWireKeyAlias`.

**Scale:** 41 discriminator strings → **29 clause types + 6 source configs = 35 types to annotate.**

---

# TASKS

## T0 — AOT reality check
- [ ] `dotnet publish Motely.CLI -c Release`; run a JAML filter; confirm current trimmer behavior.
- [ ] Record result.

## T0.5 — LSP framework spike ← **BLOCKS T9. DO EARLY.**
- [ ] Scratch project: reference `EmmyLua.LanguageServer.Framework` 0.9.2, register one hover handler.
- [ ] `dotnet publish -r win-x64 /p:PublishAot=true`
- [ ] Assert: zero IL2026/IL3050 warnings **AND** the published exe answers `initialize` over stdio.
- [ ] **Green** → adopt, saves the DTO grind. **Red** → hand-roll (T9-alt).
- [ ] Either way, keep Core free of protocol types so the shell is swappable.

---

## T1 — Source generator
**New:** `Motely.Generators/Motely.Generators.csproj` — netstandard2.0, `LangVersion=latest`,
`IsPackable=false`, `IncludeBuildOutput=false`, `EnforceExtendedAnalyzerRules=true`
**Packages (CPM):** `Microsoft.CodeAnalysis.CSharp` **4.14.0**, `Microsoft.CodeAnalysis.Analyzers` 3.11.0 — both `PrivateAssets="all"`
**Ref from Motely:** `<ProjectReference … OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`
**Add to** `Motely.slnx`

### T1.1 Attributes (emitted via `RegisterPostInitializationOutput`)
- [ ] `[JamlDiscriminator(string name)]` — `AllowMultiple = true`; props `ValueEnum`, `RollsDefault`, `RollsAreInlineValue`
- [ ] `[JamlKey(params string[] names)]`
- [ ] `[JamlIgnore]`
- [ ] `[JamlValue]` — marks the property receiving the discriminator's value array
- [ ] `[JamlSource]`

```csharp
[JamlDiscriminator("tag", RollsDefault = new[]{0,1}, ValueEnum = typeof(MotelyTag))]
[JamlDiscriminator("smallBlindTag", RollsDefault = new[]{0})]
[JamlDiscriminator("bigBlindTag",   RollsDefault = new[]{1})]
public sealed class TagClause : IJamlClause, IAnteScopedClause, IRollScopedClause
{
    [JamlValue] public required MotelyTag[] Tags { get; set; }
}
```

### T1.2 Annotate 35 types
- [ ] `AnteCards/` (11), `AnteFeatures/` (4), `Events/` (12), `JamlConfig.cs` source configs (6),
      `LogicClause.cs` + `Native/AndFilterDesc.cs` + `Native/OrFilterDesc.cs` (3)
- [ ] `[JamlIgnore]` on `JokerClause.Jokers`, `.IsWildcard`, `.LegendarySources`
- [ ] `[JamlKey("mult","value")]`, `[JamlKey("requireMega")]` → delete `ResolveWireKeyAlias`
- [ ] Escape hatch for keys with no property (`LogicClause` ante/antes, `JamlConfig.dateCreated`)

### T1.3 Pipeline (correctness-critical)
- [ ] `ForAttributeWithMetadataName` — **never** `CreateSyntaxProvider`
- [ ] **No `ISymbol`/`SyntaxNode`/`Compilation`/`SemanticModel`/`Location` in the model.** Extract
      primitives inside `transform`; symbols die there.
- [ ] Models are `record` / `readonly record struct`
- [ ] **`ImmutableArray<T>` uses reference equality** → wrap in `EquatableArray<T>` with `SequenceEqual`
- [ ] Sort by discriminator (Ordinal) before `Collect()` — FAWMN order is not stable; unsorted output
      thrashes Verify snapshots
- [ ] Diagnostic locations: carry file path + `TextSpan` start/length, rebuild with `Location.Create`
- [ ] `RegisterSourceOutput` (not `RegisterImplementationSourceOutput` — this is public API)

### T1.4 Emit into `Motely`
- [ ] `JamlGrammar.Discriminators` — frozen alias→metadata
- [ ] `JamlGrammar.KeysFor` / `SourceKeysFor` / `ValuesFor` / `RootKeys`
- [ ] `JamlPopulator` — generated switch. Replaces `Activator.CreateInstance`, `GetProperty/SetValue`,
      `Enum.TryParse(Type,…)`, `Enum.GetNames(Type)`, `Array.CreateInstance`
- [ ] Generated writer switch → replaces `JamlConfigWriter.cs:221-334` reflection
- [ ] Handle all 7 property shapes; `ParseRank` special case for `MotelyStandardcardRank?`

### T1.5 Generator diagnostics (`DiagnosticSeverity.Error`)
- [ ] `JAMLGEN001` — property with no key and no `[JamlIgnore]`
- [ ] `JAMLGEN002` — duplicate discriminator
- [ ] `JAMLGEN003` — `ValueEnum` not an enum
- [ ] `JAMLGEN004` — duplicate key in clause
- [ ] `JAMLGEN005` — `[JamlValue]` missing on a value-array discriminator
- [ ] `JAMLGEN006` — `required` member the generator can't populate

### T1.6 Delete
- [ ] `ClauseKeys`/`SourceKeys` fields (35 types)
- [ ] `JamlDiscriminatorRegistry` (`Entries`, `StaticStringArrayField`, `ClauseKeysFor`, `SourceKeysFor`, `ClauseReflectionShape`)
- [ ] `JamlClausePopulator` reflection + `ResolveWireKeyAlias`
- [ ] `JamlConfigWriter` reflection
- [ ] **All `[DynamicallyAccessedMembers]` annotations** — they exist only to preserve what the
      generator makes unnecessary
- [ ] `EnumOrAny.cs`
- [ ] Keep hand-written arm: `erraticRank`/`erraticRanks`/`erraticSuit` (OrClause fan-out)

### T1.7 Tests
**New:** `Motely.Tests/GeneratorTests.cs` — `Verify.SourceGenerators` 2.5.0, `[ModuleInitializer] VerifySourceGenerators.Initialize()`
- [ ] Snapshot: `Verify(driver)` captures generated files + diagnostics
- [ ] Drift test: fixture property with no key → `JAMLGEN001`
- [ ] Grammar-widening test: assert `legendarySources`/`jokers`/`isWildcard` are **rejected** keys
- [ ] **Cacheability test** — `trackIncrementalGeneratorSteps: true`, run twice, assert every step
      `Cached`/`Unchanged`, no model reference-shared. Snapshots will not catch this and an
      `ImmutableArray` in the model destroys IDE typing perf.

**Done when:** build clean, `JamlCorpusLoaderTests` green untouched, zero reflection in the JAML path.

**Expected AOT win:** trimmer can drop public properties/fields on 35 types + enum name metadata for
`MotelyJoker`/`MotelyVoucher`/`MotelyTag`/`MotelyBossBlind`. Meaningfully smaller WASM payload.

---

## T2 — Spans
**File:** `JamlDocumentParser.cs`
- [ ] `readonly record struct JamlSpan(int StartLine, int StartCol, int EndLine, int EndCol)` — zero-based (LSP is too)
- [ ] `JNode.Span` stamped at every construction site
- [ ] `JMap.KeySpans` — underline the key, not the block
- [ ] `JScalar` span excludes quotes
- [ ] Tokenizer tracks `(line, col)`
- [ ] `JamlSyntaxException` — store `Line`/`Column` as properties (currently formatted into the message and discarded)

**Done when:** `JamlSpanTests.cs` asserts exact spans numerically.

## T3 — Diagnostics
**New:** `Motely/Filters/Jaml/JamlDiagnostic.cs`
- [ ] `JamlSeverity { Error, Warning, Information, Hint }`
- [ ] `JamlDiagnostic(Severity, Code, Message, Span, Fixes?)`
- [ ] `JamlQuickFix(Title, Span, Replacement)`

| Range | Domain |
|---|---|
| `JAML00xx` | Syntax |
| `JAML01xx` | Unknown/misplaced keys |
| `JAML02xx` | Discriminator resolution |
| `JAML03xx` | Enum values |
| `JAML04xx` | Ranges, ints, antes |
| `JAML05xx` | Semantics (`min`>`max`, empty `must`) |
| `JAML09xx` | Lints |

- [ ] Fixes at throw site. "Did you mean" = edit distance over `JamlGrammar.KeysFor`.

## T4 — Error accumulation
**File:** `JamlConfigLoader.cs`
- [ ] `public static JamlParseResult Parse(string, JamlSourceFormat = Auto)` — never throws
- [ ] Recovery: unknown key → record+skip; bad clause → record+next; structural → stop subtree, resume sibling
- [ ] `Config` non-null on partial parse (drives completion mid-typing)
- [ ] Reimplement `FromJaml`/`FromJson`/`TryLoad` over `Parse`, signatures unchanged
- [ ] Replace `ValidateKeys` (`:379-386`) throw-on-first with accumulator
- [ ] Unify: `JamlSyntaxException` currently wrapped into `InvalidOperationException` with a
      doubly-nested message; `JamlLine.Canonicalize` throws `FormatException` instead

**Done when:** 8-typo fixture → exactly 8 diagnostics with expected codes + spans. Corpus green untouched.
**Payoff:** CLI + Jamlyzer get multi-error reporting with positions here, before any server exists.

## T5 — JSON parity
- [ ] JSON parse errors carry real line/col (hardcoded `0` today, every site)

## T6 — JamlLine is JAML
**File:** `JamlLine.cs`
- [ ] `Validate` → `IReadOnlyList<JamlDiagnostic>` with spans; keep `string?` overload over it
- [ ] Line grammar reads `JamlGrammar`, not its own word lists
- [ ] Same diagnostic codes as block form for equivalent errors
- [ ] Unify `Canonicalize`'s `FormatException` with the loader's exception type

## T7 — Public facade
**New:** `Motely/Filters/Jaml/JamlDocument.cs`
```csharp
public sealed class JamlDocument
{
    public static JamlDocument Parse(string text, JamlSourceFormat format = Auto);
    public JamlConfig? Config { get; }
    public IReadOnlyList<JamlDiagnostic> Diagnostics { get; }
    public JamlNodeRef? NodeAt(int line, int col);
    public string Text { get; }
}
```
- [ ] AST stays `internal`; `JamlNodeRef` = public read-only view

## T8 — Motely.Lsp.Core
**New:** `Motely.Lsp.Core/` → refs `Motely`. **No protocol types, no transport** (keeps the shell swappable per T0.5).
- [ ] `Diagnose(text)` ← Claude Code priority
- [ ] `Hover(text, line, col)` ← Claude Code priority
- [ ] `Definition(text, line, col)` / `References(...)` ← Claude Code priority
- [ ] `Complete(text, line, col)` ← VS Code / CodeMirror only
- [ ] `Tokens(text)`, `Format(text)`

Completion context via `NodeAt`: root → `RootKeys`; in clause → `KeysFor(disc)`; in `sources:` →
`SourceKeysFor(disc)`; on value → `ValuesFor(disc)`; in `with:` → generated `JamlWith` keys.
- [ ] Prefix-match-first `sortText`; filter by declared enum (`legendaryJoker:` → 4 names, not 150)

## T9 — Motely.Lsp.exe + Claude Code plugin ← **FIRST SHIP**
**New:** `Motely.Lsp/` → refs `Motely.Lsp.Core`. Framework per T0.5.
- [ ] stdio JSON-RPC; `initialize`/`initialized`/`shutdown`/`exit`
- [ ] Incremental `didChange`; in-memory buffer is truth, never disk
- [ ] `publishDiagnostics` on change, 150ms debounce, cancel in-flight
- [ ] **Logging → stderr only.** stdout is the protocol channel.
- [ ] Lint test: no `Console.Write*` in project
- [ ] Top-level catch per handler → stderr, return empty
- [ ] Exit when parent dies
- [ ] AOT publish

**New:** `plugin/.claude-plugin/plugin.json` + `plugin/.lsp.json`
```json
{
  "jaml": {
    "command": "${CLAUDE_PLUGIN_ROOT}/server/Motely.Lsp",
    "extensionToLanguage": { ".jaml": "jaml" },
    "diagnostics": true
  }
}
```
- [ ] `${CLAUDE_PLUGIN_ROOT}` in `command` — **verified documented**
- [ ] Do **not** set `restartOnCrash`/`shutdownTimeout` unless Claude Code ≥ v2.1.205 is required —
      older versions **skip the server silently**
- [ ] Test: `claude --plugin-dir ./plugin`, `/reload-plugins`

**Done when:** editing a `.jaml` in this repo shows diagnostics in Claude Code.

### T9-alt — hand-rolled server (if T0.5 is red)
- [ ] Content-Length framing (~50 LOC)
- [ ] JSON-RPC 2.0 envelope; requests vs notifications; `$/cancelRequest`
- [ ] `Dictionary<string, Func<JsonElement, Task<object>>>` dispatch — AOT-perfect
- [ ] Lifecycle
- [ ] Own DTOs for shipped features only, STJ `JsonSerializerContext` source-gen
- [ ] UTF-16 `Position`/`Range` offset math ← the real subtlety
- **Est. 400–800 LOC protocol + DTOs**

## T10 — Capabilities
| Task | Source | Surface |
|---|---|---|
| [ ] `publishDiagnostics` | `Core.Diagnose` | all |
| [ ] `hover` | `Core.Hover` | all |
| [ ] `definition` / `references` | `Core.*` | Claude Code, VS Code |
| [ ] `completion` | `Core.Complete` | VS Code, CodeMirror |
| [ ] `codeAction` | `JamlQuickFix` | VS Code |
| [ ] `documentSymbol` | must/should/mustNot tree | VS Code |
| [ ] `semanticTokens` | `Core.Tokens` | VS Code, CodeMirror |
| [ ] `formatting` | `Core.Format` | VS Code |

- [ ] **Decide before format-on-save:** does `JamlConfigWriter.ToJaml` preserve comments? If no → off by default.

## T11 — VS Code extension
**New:** `vscode-jaml/`
- [ ] `vscode-languageclient`, `ServerOptions` → the same binary. ~60 LOC.
- [ ] `contributes.languages` registers `.jaml`
- [ ] **No `.tmLanguage.json`** — coloring via semantic tokens only
- [ ] Binary resolution: bundle per-platform vs download-on-activate

## T12 — Motely.Wasm (rebuild from pinned Bootsharp docs)
**New:** `Motely.Wasm/` — browser runtime: search **and** language service.
- [ ] Bootsharp `[Export]` over `Motely.Lsp.Core` — direct calls, no JSON-RPC in browser
- [ ] `[RenameModule] → "index"`; short export names stay unique
- [ ] Restore search API surface; `searchSequential` takes bigints → C# `long`
- [ ] npm package, `<MotelyVersion>` stamped

### CodeMirror 6
- [ ] `linter(async view => (await motely.diagnose(text)).map(toCM))`
- [ ] `autocompletion({ override: [async ctx => motely.complete(...)] })`
- [ ] `hoverTooltip(async (view, pos) => ...)`
- [ ] Coloring: `Core.Tokens` → CM `Decoration`s
- [ ] **No Lezer grammar. No `@codemirror/lang-yaml`** — JAML is not YAML since a4cf13bb

## T13 — Hardening
- [ ] Protocol test: drive the real binary over stdio (initialize → didOpen → didChange → assert)
- [ ] AOT smoke test in CI: publish, assert `KeysFor("joker")` non-empty
- [ ] Perf: parse+diagnose largest `JamlFilters/` doc <10ms (150ms debounce must never be the bottleneck)
- [ ] Fuzz: truncated/mutated corpus → `Parse` never throws, never hangs
- [ ] CI RIDs: `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`
- [ ] `<MotelyVersion>` stamps plugin + extension + npm
- [ ] Server reports version at `initialize`; warn on engine mismatch
- [ ] Docs: one page per diagnostic code
- [ ] **Update `CLAUDE.md`** — currently documents deleted `Motely.Wasm`, deleted `Motely.Schema.cs`,
      and a `JamlDiscriminatorRegistry` this plan removes

---

## Order
1. **T0.5 spike** (blocks T9) + **T0** (AOT check)
2. **T1** generator — corpus green proves equivalence
3. **T2–T5** spans, diagnostics, accumulation, JSON — CLI/Jamlyzer payoff lands here
4. **T6, T7**
5. **T8 + T9 — first ship**
6. **T10**
7. **T11, T12**
8. **T13** throughout

## Open
- `Motely.Lsp` vs `Motely.LSP` naming
- T0.5 outcome: EmmyLua vs hand-roll
- Format-on-save gated on comment preservation
- Min Claude Code version — affects `restartOnCrash`/`shutdownTimeout`

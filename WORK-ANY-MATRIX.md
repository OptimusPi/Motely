# WORK — Any / wildcard matrix (Claude: execute, do not poetry)

**Operator:** Nat  
**Author:** Grok (audit 2026-07-30)  
**Executor:** Claude Code — ship code + tests + real seed proof. No essays. No “feels like.”  
**Park:** jaml-ui visual thrash, submodule moniker debates, LSP packaging poetry.  
**Law:** one grammar; FilterDesc + JamlScoring same defaults; **proof = `dotnet test` green + at least one real search finds a seed** for each new `Any` path.

**Bite-sized queue (Haiku/Sonnet one ticket per turn):** see **`CLAUDE-BITES-MATRIX.md`**  
— Engine tickets `E01`…`E22` · jaml-ui tickets `U01`…`U18` · cross `X01`…`X03`.  
Phases W0–W4 below map to: W0≈E01–E03, W1≈E04–E12, W2≈E13–E19, W3≈E21, W4≈E20.

---

## Product facts (do not re-argue)

| Fact | Proof |
|------|--------|
| `joker: Any` / rarity jokers `Any` / `tarotCard: Any` work | parse → `IsWildcard=true`, empty list; SIMD + scoring |
| `spectralCard: Any` **broken** | CLI: *Cannot parse 'Any' as MotelySpectralCard* |
| `planetCard: Any` **broken** | same shape as spectral (no `IsAny` arm) |
| `Any` is **not** an enum member | loader literal `IJamlValueReader.IsAny` (`"any"` CI) |
| `IsWildcard` bool is dual-state crap | empty list already means wildcard in practice; bool can desync |
| Default **sources** for tarot/joker/spectral ordinary | **shop slots 0–7 only** — not packs, not “full walk” |
| Empty **antes** | `JamlSearchBuilder` fills `[1..8]` |
| Named antes e.g. `[4,5]` | **only those antes**; sources still shop-default unless authored |
| `with:` luck | **event clauses only** (`IWithScopedClause`); never tarot/joker |
| One language brain | `Motely.Lsp.Core`; hosts = `Motely.Lsp` + Wasm export; vscode client only |

---

## Phase order (do not skip)

```
W0  pin proof tests for current tarot/joker Any + default sources law
W1  product hole: spectralCard Any (+ planetCard Any) — KEEP IsWildcard for parity
W2  kill IsWildcard: empty array = any everywhere (mechanical + legendary traps)
W3  optional wire: bare `joker:` / null value = any (only after W2)
W4  docs one-liner in JAML.md — after code green
```

**Ship gate each phase:** `dotnet test Motely.Tests/Motely.Tests.csproj --nologo` green.  
**W1/W2 extra gate:** at least one filter finds a seed on a known short search (CLI or existing proof-test pattern).

---

## W0 — Pin the law (tests first, no production change)

| # | Task | Path / command | Done when |
|---|------|----------------|-----------|
| W0.1 | Extend `JamlWildcardTests` | `Motely.Tests/JamlWildcardTests.cs` | spectral/planet **fail parse** today documented as red→green target OR skip with issue id |
| W0.2 | Pin tarot default sources | new or extend wildcard tests | load `tarotCard: Any` + `antes: [4,5]` → `Sources is null`, `IsWildcard`, `Antes=[4,5]` |
| W0.3 | Pin score count shop-only | scoring unit or small search | clause with no sources does **not** count pack-only tarots (if you have a seed fixture; else document deferred) |
| W0.4 | Pin `with:` rejected on tarot | load invalid doc | unknown key / with not in ClauseKeys fails loudly |

Do not “fix” W0 by relaxing defaults.

---

## W1 — Spectral + Planet `Any` (KEEP `bool IsWildcard`)

Mirror **exactly** `TarotCardFilterDesc` / `MatchTarot` / `JamlWildcardTests` tarot theory.

### W1.A Spectral

| # | File | Change |
|---|------|--------|
| W1.A1 | `Motely/Filters/Jaml/AnteCards/SpectralCardFilterDesc.cs` | add `bool IsWildcard`; `SetDiscriminatorValue`: if `value.IsAny` → flag true; assert `IsWildcard \|\| Spectrals.Length > 0` |
| W1.A2 | same file, `MatchSpectrals` (or add) | if wildcard → `TypeCategory == SpectralCard` (check Soul/BlackHole type traps — see traps table) |
| W1.A3 | SIMD `Filter` | shop already ANDs category; ensure wildcard path counts; **do not** change default sources |
| W1.A4 | Special Soul/BH | named TheSoul/BlackHole keep `DefaultSpecialSources` / special filter path; **wildcard + null sources = shop-only ordinary spectrals** (document in test name) |
| W1.A5 | `Motely/Filters/Jaml/JamlScoring.cs` `MatchSpectral` | if `IsWildcard` → category match (Soul/BH: only if they appear as spectral category items — **prove with seed or unit**) |
| W1.A6 | `JamlConfigWriter` / `JamlLine` | emit `Any` when wildcard (parity with tarot/joker) |
| W1.A7 | `Motely.Tests/JamlWildcardTests.cs` | `Any`/`any`/`ANY` load spectral; empty Spectrals; `IsWildcard` |
| W1.A8 | Proof search | JAML `must: - spectralCard: Any` + shop-ish antes; find ≥1 seed OR pin known seed via existing proof harness |

### W1.B Planet

| # | File | Change |
|---|------|--------|
| W1.B1 | `PlanetFilterDesc.cs` | same as tarot (flag + parse + MatchPlanets + assert) |
| W1.B2 | `JamlScoring` planet match | wildcard category |
| W1.B3 | writer/line + tests | same as spectral lite |
| W1.B4 | proof | `planetCard: Any` finds a seed |

### Spectral traps (read before coding)

| Trap | Action |
|------|--------|
| TheSoul / BlackHole special item types | Named path stays special; wildcard is category — if Soul is not `SpectralCard` category, wildcard will **not** count it unless you explicitly OR those types. **Choose one law, test it, write it in the test name.** Recommended: wildcard = ordinary spectral category only; Soul/BH only when named. |
| `DefaultSpecialSources` for Soul | only when resolving named soul/BH; never auto-swap for `Any` |
| Charm-like mega/ethereal/omen | existing scalar fallthrough — leave alone |
| Shop AND `isSpectral & Match` | avoid double-filter bug (tarot does category AND MatchTarots which is also category on wildcard — redundant but ok) |

### W1 commit message shape

```
feat(jaml): spectralCard/planetCard accept Any wildcard

Shop-default sources unchanged. Soul/BlackHole remain named-only.
```

---

## W2 — Kill `IsWildcard` (empty list = any)

**Semantic law (final):**

```
Items.Length == 0  →  any (category match)
Items.Length  > 0  →  named list
Wire still accepts literal "Any" / "any" / "ANY"
No Motely* enum gains an Any member
EnumOrAny<T> — delete if unused after, or leave with one comment pointing at empty-list law
```

### Touch matrix (every cell = code + grep clean)

| Family | Clause type | Parse | SIMD match | Scoring | Writer | Line | Tests setting flag |
|--------|-------------|-------|------------|---------|--------|------|-------------------|
| joker | `JokerClause` | drop flag | `MatchJokers` empty→cat | Count* | `WriteJokerFamily` | `FromJoker` | all `IsWildcard=true` → empty Jokers |
| commonJoker | `CommonJokerClause` | same | same | same | same | — | same |
| uncommonJoker | `UncommonJokerClause` | same | same | same | same | — | same |
| rareJoker | `RareJokerClause` | same | same | same | same | — | same |
| legendaryJoker | `LegendaryJokerClause` | flag + `SoulCardOnly` | soul matcher | scoring | writer | — | **three-way assert** |
| tarotCard | `TarotCardClause` | drop flag | `MatchTarots` | `MatchTarot` | consumable write | `FromClause` tarot arm | JamlWildcardTests |
| spectralCard | after W1 | drop flag | MatchSpectrals | MatchSpectral | | | |
| planetCard | after W2.B | drop flag | MatchPlanets | | | | |

### Grep debt (must hit zero for `IsWildcard`)

```sh
rg -n 'IsWildcard' --type cs
```

Known hot files (from audit count):

| File | ~hits | Notes |
|------|-------|--------|
| `JamlScoring.cs` | 8 | Count* + Match* + clones |
| `JokerFilterDesc.cs` | 8 | clause + MatchJokers + UsesLegendaryPath |
| `JamlConfigWriter.cs` | 6 | `c.IsWildcard ? Any` |
| `TarotCardFilterDesc.cs` | 4 | |
| `LegendaryJokerFilterDesc.cs` | 4 | SoulCardOnly interaction |
| rarity filter descs | 3 each | common/uncommon/rare |
| `JamlLine.cs` | 3 | |
| `LegendarySoulMatcher.cs` | 1 | |
| `JamlSearchBuilder.cs` | 1 | LabelRenderable |
| Tests | 9 files | mechanical replace |

### Legendary special law (W2)

| Case | Representation after kill |
|------|---------------------------|
| `legendaryJoker: Any` | `Jokers=[]`, not soul-only |
| soul card only (if that mode exists) | keep `SoulCardOnly` flag **or** separate discriminator — **do not** overload empty list for two meanings |
| mixed named legendaries | non-empty `Jokers` |

If `SoulCardOnly` and empty list collide, **stop and ask Nat** — do not invent.

### W2 mechanical recipe (Claude slave loop)

1. Remove property `IsWildcard` from clause types one family at a time.
2. Parse: `if (value.IsAny) { /* leave array empty */ return true; }`
3. Match: `if (items.Length == 0) category else list`
4. Assert: remove `IsWildcard ||`; empty is legal.
5. Writer: `array.Length == 0 ? Any : enums`
6. Tests: `IsWildcard = true` → delete flag, leave empty arrays; asserts `Assert.Empty(...); Assert.True(match any)` via behavior not flag.
7. `rg IsWildcard` clean.
8. Full test suite.

### W2 commit

```
refactor(jaml): empty discriminator list means Any; drop IsWildcard
```

---

## W3 — Optional omit syntax (ONLY after W2)

| Wire | Meaning |
|------|---------|
| `joker: Any` | keep |
| `joker:` empty scalar | treat as any (if parser yields empty string / null) |
| bare line `Any` / `joker` | only if `JamlLine` already supports; do not invent |

If parser ambiguity with nested maps — **abort W3**, document.

---

## W4 — Docs (last)

| File | One paragraph |
|------|----------------|
| `JAML.md` | `Any` = category match; default sources shop-only; packs need `sources:`; spectral/planet now accept Any |
| Do **not** rewrite README novels | |

---

## Explicit non-goals (Claude: do not wander)

| Non-goal | Why |
|----------|-----|
| jaml-ui moniker / submodule redesign | park |
| Multiple LSP rewrites | one brain: Lsp.Core only |
| Adding `Any` to C# enums | pollutes pools |
| Changing shop-default to “all sources” | silent behavior bomb |
| Coverage climb without seed proof | S8 law already forbids |
| “with: luck” on card clauses | wrong axis |

---

## Acceptance checklist (paste into PR body)

```
[ ] W1 spectral Any parse (any/Any/ANY)
[ ] W1 planet Any parse
[ ] W1 spectral Match + scoring wildcard
[ ] W1 planet Match + scoring wildcard
[ ] W1 proof seed (spectral) 
[ ] W1 proof seed (planet)
[ ] Default sources still shop-only for null Sources
[ ] Soul/BlackHole named path unchanged
[ ] W2 rg IsWildcard → 0 hits in production + tests
[ ] W2 full Motely.Tests green
[ ] W2 legendary SoulCardOnly still coherent
[ ] JAML.md one-liner
[ ] No jaml-ui changes
```

---

## Size estimate (so Nat knows slave vs poetry)

| Phase | Size | Slave hours (honest) |
|-------|------|----------------------|
| W0 | small | 0.5–1 |
| W1 | medium | 2–4 |
| W2 | large mechanical | 4–8 |
| W3 | small if easy | 1 |
| W4 | tiny | 0.25 |

**Total:** fuckload mechanical, not research. Bored Claude should be in the match helpers, not the essay pane.

---

## First command when Claude wakes

```sh
cd /Users/nate/Documents/GitHub/MotelyJAML
rg -n 'IsWildcard|SetDiscriminatorValue|MatchSpectral|MatchTarot' Motely/Filters/Jaml --type cs
dotnet test Motely.Tests/Motely.Tests.csproj --filter 'FullyQualifiedName~JamlWildcard' --nologo
```

Then start **W1.A1** — not a design doc, not jaml-ui.

---

## Burn line for other bots

> Stop the honey-soup. Table or a real diff — no `soup()`. Execute WORK-ANY-MATRIX.md W1 then W2.

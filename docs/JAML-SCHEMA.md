# JAML schema & loader — drift checklist

The engine **never reads** `jaml.schema.json`. Runtime truth is `JamlConfigLoader` → `JamlClauseUnion` (YAML DTO) → `CreateClauseFromDto` → clause POCOs → `*FilterDesc`.

JSON Schema is for **editors and tooling only**. If it diverges from the loader, users get autocomplete lies and `ValidateJaml` surprises.

---

## One command to regenerate (canonical)

From the **repo root** (`MotelyJAML/`):

```powershell
dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release -- schema
```

Or:

```powershell
.\regen-jaml-schema.ps1
```

That runs `MotelyJamlSchemaGenerator.WriteDefault()` and writes **all** of these in one shot:

| Output | Purpose |
|--------|---------|
| `jaml.schema.json` | Repo root; source for copies |
| `motely-wasm/jaml.schema.json` | npm package (also copied on `dotnet publish Motely.Wasm`) |
| `packages/jaml-language-core/schema/jaml.schema.json` | Language tooling (if tree exists) |
| `packages/jaml-language-support/schema/jaml.schema.json` | VS Code extension schema |
| `motely-wasm/motely-item-formats.d.ts` | Packed-item display metadata (TS) |
| `motely-wasm/motely-item-formats.mjs` | Same (ESM) |
| `packages/.../motely-item-formats.*` | Language package copies |

Then verify:

```powershell
dotnet test Motely.Tests/Motely.Tests.csproj -c Release --filter "FullyQualifiedName~JamlSchema"
```

Commit the regenerated files **together** with any loader/DTO change.

### Do not use

| Tool | Status |
|------|--------|
| `dotnet run jaml-schema.cs` | **Legacy.** Reflection-based second generator; different `$id` history; easy to commit the wrong file. |
| Hand-editing `jaml.schema.json` | **Forbidden** — next regen overwrites; drift guaranteed. |

---

## The four sources of truth (why it feels like a prank)

```text
  .jaml file
       │
       ▼
  JamlConfigLoader          ◄── BEHAVIOR (strict: unknown keys throw)
       │                      RawParse AllowedRootKeys
       │                      ResolveType() discriminator order
       │                      CreateClauseFromDto switch
       ▼
  JamlClauseUnion           ◄── YAML DTO (YamlDotNet)
       │
       ├──────────────────────────┐
       ▼                          ▼
  JokerClause / BossClause …     jaml.schema.json
  *FilterDesc / SIMD               ◄── MotelyJamlSchemaGenerator
                                   PropertyToRef manual map
                                   (editors only)
```

---

## Checklist: adding or changing a JAML clause

Use this every time you touch filter syntax. Check boxes in order.

### 1. Runtime (required — ship breaks if wrong)

- [ ] **Clause POCO** — e.g. `FooClause` in `Motely/Filters/Jaml/`, implements `IJamlClause` / `CreateDesc()`.
- [ ] **Filter desc** — `FooFilterDesc` (+ SIMD filter) wired in search.
- [ ] **`JamlSearchBuilder`** — clause type registered if needed.
- [ ] **`JamlClauseUnion`** — new `[YamlMember(Alias = "foo")]` properties on `JamlConfigLoader.Models.cs`.
- [ ] **`ResolveType`** — `JamlConfigLoader.cs` recognizes the YAML discriminator key(s).
- [ ] **`CreateClauseFromDto`** — `switch` arm builds `FooClause` (sources, antes, min/max, etc.).
- [ ] **`AddClauseToSet`** — `case FooClause` pushes into `JamlClauseSet` lists + `OrderedClauses`.

### 2. Strict parse (unknown key tests)

- [ ] Root key (if new top-level): add to `AllowedRootKeys` in `JamlConfigLoader.RawParse.cs`.
- [ ] Clause key: YamlDotNet deserializer on `JamlClauseUnion` must accept it (property on union).
- [ ] `Motely.Tests` — `JamlConfigTests` unknown-key rejection still passes; add test if new key class.

### 3. Schema generator (editors)

- [ ] **`PropertyToRef`** in `Motely.CLI/MotelyJAML.schema.generator.cs` — if the YAML key needs a typed `$ref` (e.g. `foo` → `"FooEnumDef"`), add `"foo" = "Foo"`.
  - Keys use **camelCase property names** (`joker`, `commonJoker`, `tarotCard`), not C# PascalCase.
  - Event shorthand keys (`luckyMoney`, `wheelOfFortune`, …) usually **omit** `PropertyToRef`; they stay on the union via `JsonSchemaExporter` but may not get rich enum refs unless you add defs in `BuildDefs()`.
- [ ] **`BuildDefs()`** — add `$defs` entries for new enums or `StandardCard`-style shapes.
- [ ] Run **`dotnet run --project Motely.CLI -c Release -- schema`**.
- [ ] `JamlSchemaSnapshotTests.Schema_PreservesPublicJamlContract` — update if public contract intentionally changes.

### 4. Regression fixtures

- [ ] `Motely.Tests/filters/*.jaml` — new community filter or extend existing (parses, compiles, selective must).
- [ ] `Motely.Tests/GoldenJamlFiles/` — if new **syntax** (canonical + legacy-invalid pair).

### 5. WASM / npm (if JS consumers care)

- [ ] `dotnet publish Motely.Wasm -c Release` (refreshes `motely-wasm/` + schema copy via `FinalizeNpmPackage`).
- [ ] `node Motely.Wasm/motely.test.mjs` → `RESULT: PASS`.
- [ ] Copy `jaml.schema.json` to **jaml-ui** if it vendors schema separately (`jaml-ui/jaml.schema.json`).

### 6. Docs

- [ ] `Motely.Wasm/README.md` — only if public WASM API changes (not schema-only).

---

## Discriminator keys: loader vs schema

`ResolveType` picks the clause kind by **first matching** non-null property on `JamlClauseUnion` (order matters if multiple keys are set).

| YAML key(s) | Loader `MotelyFilterItemType` | `PropertyToRef` (schema) |
|-------------|------------------------------|---------------------------|
| `joker`, `jokers` | Joker | Joker |
| `commonJoker`, `commonJokers` | CommonJoker | CommonJoker |
| `uncommonJoker`, `uncommonJokers` | UncommonJoker | UncommonJoker |
| `rareJoker`, `rareJokers` | RareJoker | RareJoker |
| `legendaryJoker`, `legendaryJokers` | LegendaryJoker | LegendaryJoker |
| `voucher`, `vouchers` | Voucher | Voucher |
| `tarotCard`, `tarotCards` | TarotCard | Tarot |
| `spectralCard`, `spectralCards` | SpectralCard | Spectral |
| `planetCard` | PlanetCard | Planet |
| `boss` | Boss | Boss |
| `tag`, `tags` | SmallBlindTag | Tag |
| `smallBlindTag`, `smallBlindTags` | SmallBlindTag | Tag |
| `bigBlindTag`, `bigBlindTags` | BigBlindTag | Tag |

**Map features** (tag, voucher, boss) use **`rolls`** — stream indices per ante, not shop `sources` and not `any`.

| Feature | Default `rolls` | Meaning |
|---------|-----------------|---------|
| `tag`, `tags` | `[0, 1]` | small-blind offer, big-blind offer |
| `smallBlindTag`, `smallBlindTags` | `[0]` | small-blind offer only |
| `bigBlindTag`, `bigBlindTags` | `[1]` | big-blind offer only |
| `voucher`, `vouchers` | `[0]` | ante voucher award |
| `voucher` + `rolls: [1]` / `[2]` | next voucher-stream draws on that ante (Hieroglyph bonus, voucher-tag extras) |
| `boss` | `[0]` | boss blind for that ante (forward pass) |

Valid indices: tag `0..5` (six stream draws per ante), voucher `0..2` (three draws), boss `0..2` in JAML (filter/scoring match roll `0` until full Hieroglyph/Petroglyph rewind sim exists). Plural tag lists are **OR** over enum names on the selected rolls. `[]` is invalid.
| `standardCard`, `standardCards` | Standardcard | StandardCard |
| `erraticRank` | ErraticRank | Rank (value) |
| `erraticSuit` | ErraticSuit | Suit (value) |
| `erraticCard` | ErraticCard | — |
| `startingDraw` | StartingDraw | — |
| `event` | Event | MotelyEventType |
| `luckyMoney`, `luckyMult`, … | Event (rolls) | — (union shape only) |
| `and`, `or`, `clauses` | logic | JamlClauseUnion |

Shared clause fields (`antes`, `min`, `max`, `score`, `sources`, `edition`, …) are not discriminators. `MotelyJamlSchemaGenerator` builds `JamlClauseUnion` as **`oneOf`**: each branch requires exactly one discriminator key plus the shared modifier properties from the exporter (no other discriminator keys on that branch).

---

## Root document keys

| Key | Loader `AllowedRootKeys` | Schema root `properties` |
|-----|--------------------------|---------------------------|
| `id`, `name`, `author`, `dateCreated`, `description` | yes | yes |
| `deck`, `stake` | yes (parsed as enums) | yes (`$ref` Deck/Stake in generator) |
| `defaults` | yes | yes |
| `must`, `should`, `mustNot` | yes | yes (array of `JamlClauseUnion`) |
| `aesthetics` | yes | yes (`JamlAesthetic`) |
| `seeds` | yes | yes |

---

## Common drift symptoms

| Symptom | Likely cause |
|---------|----------------|
| Editor happy, CLI `ValidateJaml` fails | Schema stale or allows keys loader rejects |
| CLI accepts, editor red squiggles | Schema not regenerated after DTO change |
| `Unknown property 'foo'` | Key missing from union / typo; schema may still list it if DTO has orphan property |
| Wrong clause type at runtime | `ResolveType` order: multiple keys set on one mapping |
| `joker: any` confusion | Schema allows `any` on joker enums; loader rules for Any are separate (see tests) |
| Two different `$id` URLs in the wild | Ran legacy `jaml-schema.cs` vs CLI generator |

---

## Tests that guard this

| Test project | What it checks |
|--------------|----------------|
| `JamlConfigTests` | Parse, sources, **unknown keys rejected** |
| `JamlCorpusRegressionTests` | Golden YAML corpus |
| `V0FilterRegressionTests` | `Motely.Tests/filters/*.jaml` end-to-end |
| `JamlSchemaSnapshotTests` | Generator output shape (`$id`, clause refs, event enum) |
| `Motely.Wasm/tests/*.test.mjs` | WASM interop (not schema file) |

---

## Mental model

**Ship behavior in C#.** Treat `jaml.schema.json` as a generated editor artifact—same class as Bootsharp’s `dist/`: regen after DTO changes, never hand-tune.

If you only remember one thing:

```powershell
dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release -- schema
```

# CLAUDE BITES MATRIX — MotelyJAML + jaml-ui

**Operator:** Nat  
**Capture author:** Grok (2026-07-30) — session audit: joker/Any, sources shop-default, spectral hole, IsWildcard dual-state, with: luck axis, one LSP brain, jaml-ui Jimbo queue  
**Executor:** Claude Code **Haiku or Sonnet** — **one ticket per turn**  
**Law:** table or real diff. No poetry. No honey-soup.  
**Repos:** `WORK-ANY-MATRIX.md` (engine phases W0–W4) · `jaml-ui/HANDOFF-CLAUDE.md` (UI P2–P8) · this file = **unified bite queue**

---

## How Claude plays (print this)

| Rule | Detail |
|------|--------|
| **One ticket** | Nate says `T###` or `do next open` |
| **One repo** | ticket column **Repo** = `Motely` or `jaml-ui` — never both in one turn |
| **Proof** | every ticket ends with a command that must exit 0 |
| **Stop** | handoff table only — no “what next?” essay |
| **Park** | do not redesign moniker, do not multi-LSP rewrite, do not change shop-default sources without Nat |

```
| Doing | T### short verb |
| Where | path(s) |
| Result | fact + proof command |
| Next | stop (or next id if Nat said continue) |
```

---

## Captured product law (do not re-research)

| Law | Meaning |
|-----|---------|
| `*: Any` | category match (wildcard). Not an enum member. Loader string `"any"` CI |
| Default **sources** (tarot/joker/ordinary spectral) | **shop 0–7 only** if `sources:` omitted |
| Default **antes** | empty → builder fills `1..8` |
| Named antes `[4,5]` | only those antes; still shop-default unless `sources:` |
| `with: { luck, vouchers }` | **event clauses only** — not cards |
| `spectralCard: Any` / `planetCard: Any` | **engine hole today** (parse fails) |
| `IsWildcard` bool | dual-state crap; kill in W2 after W1 ships holes |
| LSP | **one brain** `Motely.Lsp.Core`; hosts `Motely.Lsp` + Wasm; vscode client; jaml-ui uses `motely-wasm` |
| jaml-ui | **git submodule** `jaml-ui` → `OptimusPi/jaml-ui`; Jimbo design: **no flex** |
| UI parse drift | `parseClauses.ts` keys are `tarot` not `tarotCard` — visual layer ≠ engine wire |

---

## Repo map

| Repo | Path in this workspace | Package mgr / test |
|------|------------------------|--------------------|
| **Motely** | workspace root (`Motely/`, `Motely.Tests/`, …) | `dotnet test Motely.Tests` |
| **jaml-ui** | `jaml-ui/` submodule | `pnpm` · `npx eslint <file>` · `pnpm build` |

---

# TRACK E — Engine (Motely) — Haiku bites

Order: **E01 → …** Do not skip E10 until E01–E09 green unless Nat says otherwise.

| ID | Size | Verb | Files (only these) | Proof | Depends |
|----|------|------|--------------------|-------|---------|
| **E01** | XS | Pin tarot Any parse + empty Tarots + antes | `Motely.Tests/JamlWildcardTests.cs` | `dotnet test --filter JamlWildcard --nologo` | — |
| **E02** | XS | Pin joker Any case fold already there; add **sources null** assert | same file | same filter | E01 |
| **E03** | XS | Pin `with:` under tarot **fails** load | new fact in `JamlWildcardTests` or loader test | same | — |
| **E04** | S | Spectral: add `IsWildcard` + parse `IsAny` | `SpectralCardFilterDesc.cs` only | compile + wildcard filter later | — |
| **E05** | S | Spectral: `MatchSpectrals` wildcard = category | same file SIMD match helper | unit if any else E08 | E04 |
| **E06** | S | Spectral: scoring `MatchSpectral` wildcard | `JamlScoring.cs` only | tests E08 | E04 |
| **E07** | XS | Spectral: writer + line emit `Any` | `JamlConfigWriter.cs`, `JamlLine.cs` | round-trip test optional | E04 |
| **E08** | S | Tests: spectral Any parse + empty Spectrals | `JamlWildcardTests.cs` | filter green | E04–E06 |
| **E09** | S | Proof seed: `must: spectralCard: Any` finds seed | new `SpectralAnyProofTests.cs` or extend proof smoke | **real search finds seed** | E08 |
| **E10** | S | Planet: parse + flag + Match + scoring | `PlanetFilterDesc.cs`, `JamlScoring.cs` | mirror E04–E06 | — |
| **E11** | XS | Planet tests + proof seed | `JamlWildcardTests` + proof | green + seed | E10 |
| **E12** | S | Doc Soul/BH law in test name | spectral proof tests | comment: named Soul ≠ Any shop | E09 |
| **E13** | M | **Kill IsWildcard** on `TarotCardClause` only | Tarot desc + scoring + writer + tests for tarot | `rg IsWildcard Tarot` empty in those files | E01–E03, prefer after E11 |
| **E14** | M | Kill IsWildcard on `JokerClause` | `JokerFilterDesc.cs` + scoring + writer | suite filter joker | E13 |
| **E15** | M | Kill on common/uncommon/rare | 3 rarity descs + tests | suite | E14 |
| **E16** | M | Kill on legendary + soul matcher | legendary files | suite; **stop if SoulCardOnly collides** | E15 |
| **E17** | M | Kill on spectral + planet | after E09–E11 | `rg IsWildcard` **zero** in Motely/ | E16 |
| **E18** | S | Fix all remaining tests `IsWildcard = true` | `Motely.Tests/**` | full `dotnet test Motely.Tests` | E17 |
| **E19** | XS | Delete or comment `EnumOrAny.cs` if unused | that file + greps | build | E18 |
| **E20** | XS | `JAML.md` 5 lines: Any + shop default + spectral/planet | `JAML.md` | prose only, no invent | E11 |
| **E21** | S | Optional: empty scalar `joker:` = any | loader only if unambiguous | tests | E18 |
| **E22** | track | Jamlyzer ante-39 hang | Motely analyzer | **note only** until Nat opens | — |

### Engine traps (read before E04+)

| Trap | Law |
|------|-----|
| Default sources | do **not** auto-open packs for `Any` |
| Soul / BlackHole | named only; `Any` = ordinary spectral category unless test proves otherwise |
| Legendary path | `joker: Any` still uses legendary scalar path today — preserve behavior in E14 |
| Dual state | after E17 empty list **is** any; never set flag |

### Engine first commands

```sh
cd <MotelyJAML root>
dotnet test Motely.Tests/Motely.Tests.csproj --filter 'FullyQualifiedName~JamlWildcard' --nologo
rg -n 'IsWildcard|SetDiscriminatorValue' Motely/Filters/Jaml/AnteCards --type cs
```

---

# TRACK U — jaml-ui (submodule) — Haiku bites

**Design:** `jaml-ui/CLAUDE.md` — **no flex**, Jimbo only, pnpm only.  
**One file eslint 0** is the gate for migrate tickets (repo-wide may still be dirty).

| ID | Size | Verb | Files | Proof | Depends |
|----|------|------|-------|-------|---------|
| **U01** | XS | Status: eslint count `JamlMapPreview` | `jaml-ui/src/components/JamlMapPreview.tsx` | `npx eslint …` print count; **no code** | — |
| **U02** | M | Jimbo-migrate MapPreview (P2) | same file only | eslint **0** + `pnpm build` | U01 |
| **U03** | S | Extract `JimboZoneRail` + story (P2.5) | `src/ui/JimboZoneRail.tsx` + stories | build + story exists | U02 soft |
| **U04** | XS | Wire MapPreview to ZoneRail | MapPreview import | eslint 0 file | U03 |
| **U05** | M | Jimbo-migrate `JamlIdeVisual` (P3) | `JamlIdeVisual.tsx` | eslint 0 + build | U03 |
| **U06** | M | Jimbo-migrate `JamlIde` shell (P4) | `JamlIde.tsx` | eslint 0 + build | U05 |
| **U07** | M | Jimbo-migrate `JamlMapEditor` (P5) | `jamlMap/JamlMapEditor.tsx` | eslint 0 + build | soft of U02–U06 |
| **U08** | XS | Clear `TODO(jimbo-primitives)` greps | any leftover markers | `git grep` empty | U07 |
| **U09** | S | `parseClauses`: map engine keys `tarotCard`/`spectralCard`/`planetCard`/`joker` + value `Any` | `src/lib/jaml/parseClauses.ts` | unit test or manual assert names/`Any` | engine E08 ideally |
| **U10** | S | Visual: `spectralCard: Any` uses blank spectral sprite | `spriteMapper.ts` + visual path | story or unit | U09 |
| **U11** | S | Visual: `planetCard: Any` / `tarotCard: Any` sprites | same | story | U09 |
| **U12** | S | CategoryPicker / MysterySlot “Any” label matches engine law (shop default tooltip?) | `jamlMap/*Picker*` | build | U09 |
| **U13** | M | Authoring help: surface MotelyJaml.validate errors in IDE chrome | `JamlIde` / code surface — **no fake LSP** | validate bad jaml shows JimboErrorBlock | U06 |
| **U14** | S | Vocab once: completion lists from **one** module calling wasm `listItems` | `lib/jaml/*` | no dual drift on Any | — |
| **U15** | XS | JamlyzerView ante-0 button if engine has ante 0 | `JamlyzerView` / rail | eslint 0 area | — |
| **U16** | track | ante-39 perf | **Motely E22 only** | do not “fix” in UI | E22 |
| **U17** | XS | Handoff board: mark P2–P5 done when U02–U07 ship | `HANDOFF-CLAUDE.md` | truth only | after migrate |
| **U18** | release | Version bump / publish | needs **Nat go** | `pnpm build` | U02–U07 + green |

### jaml-ui first commands

```sh
cd jaml-ui
pnpm install   # if needed
npx eslint src/components/JamlMapPreview.tsx
pnpm build
```

### jaml-ui non-goals

| No | Why |
|----|-----|
| Recreate Motely.JsonRender in C# | UI owns skin |
| npm install writing package-lock | pnpm only |
| flex layouts | host iframe law |
| Fake second LSP in TS | use `motely-wasm` / MotelyJaml |

---

# TRACK X — Cross-repo (Nat schedules; still one ticket)

| ID | Size | Verb | Note |
|----|------|------|------|
| **X01** | S | After E08: UI completion offers `Any` for spectral/planet | U14 + engine wasm rebuild if needed |
| **X02** | S | Submodule pin: Motely bumps `jaml-ui` SHA after U-release | Motely root only |
| **X03** | XS | Point both handoffs at this file | one line each | 

---

## Master status board (update when shipping)

| ID | Status | Owner note |
|----|--------|------------|
| E01–E03 | open | pin law |
| E04–E12 | open | product Any holes |
| E13–E19 | open | kill IsWildcard |
| E20–E21 | open | docs / omit |
| E22 | parked | perf |
| U01–U08 | open | Jimbo migrate queue |
| U09–U14 | open | Any parity + help |
| U15–U18 | open / gate | polish + release |
| X01–X03 | open | glue |

---

## Size legend (Haiku vs Sonnet)

| Tag | Meaning | Model |
|-----|---------|--------|
| **XS** | 1 file, &lt;30 lines, tests or docs | Haiku fine |
| **S** | 1–2 files, clear mirror of existing pattern | Haiku or Sonnet |
| **M** | multi-file or eslint migrate slog | Sonnet preferred |
| **track / release** | no code or Nat gate | do not freestyle |

---

## Nate quick-pick menus

**Bored Claude — engine only:**

```
do E04
```

**Bored Claude — jaml-ui only:**

```
do U01
```

**Autopilot bite chain (engine product hole):**

```
E04 → E05 → E06 → E07 → E08 → E09 → E10 → E11 → E20
```

**Autopilot bite chain (UI Jimbo):**

```
U01 → U02 → U03 → U04 → U05 → U06 → U07 → U08
```

**Do not autopilot:** E16 (legendary), E21 (omit syntax), U18 (publish), E22/U16 (perf).

---

## Burn lines (paste at bots)

> Execute one ID from `CLAUDE-BITES-MATRIX.md`. Table or real diff — no soup.  
> Engine: empty list will mean Any after E-track; today use IsWildcard for W1-shaped E04–E12.  
> jaml-ui: one file, eslint 0, pnpm build — no flex.

---

## Session capture log (why this file exists)

| Topic | Outcome captured |
|-------|------------------|
| joker / jokers verb | FilterDesc + SIMD shop/packs; Any = category |
| tarot Any + antes [4,5] | shop 0–7 only, those antes only |
| spectral Any missing | E04–E09 |
| IsWildcard stupid | E13–E19 empty-list law |
| with: luck | events only |
| 2–4 LSPs feel | one Core; hosts only |
| jaml-ui trash / submodule | real queue U01–U18; submodule at `jaml-ui/` |
| honey-soup | enforced on executor |

---

## Related files

| File | Role |
|------|------|
| `WORK-ANY-MATRIX.md` | engine phase narrative W0–W4 |
| `HANDOFF-CLAUDE.md` | Motely coverage climb (S8) — **orthogonal** |
| `jaml-ui/HANDOFF-CLAUDE.md` | Jimbo partner game P0–P8 |
| `jaml-ui/TASKS.md` | product gaps (stale package names — trust U-track) |
| `JAML.md` | user-facing grammar (update E20) |

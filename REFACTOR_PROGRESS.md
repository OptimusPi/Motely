# JAML Typed Clauses Refactor - Progress

## COMPLETED ✅ (Both Motely.csproj + Motely.CLI.csproj = 0 errors, 0 warnings)
- [x] JamlClauses.cs — typed clause POCOs (JokerClause, BossClause, etc.) — `Motely/filters/JamlClauses.cs`
- [x] JamlFilterDesc.cs — top-level combiner (groups TypedMust, creates sub-filters, ANDs) — `Motely/filters/JamlFilterDesc.cs`
- [x] JamlConfig.cs — added TypedMust/TypedShould/TypedMustNot lists (JsonIgnore)
- [x] JamlConfigLoader.cs — ToTypedClause() converts raw JamlClause → typed POCOs during Canonicalize()
- [x] CLI csproj points to Motely directly (not Orchestration)
- [x] CLI Program.cs completely rewritten: JamlConfig → JamlFilterDesc → MotelySearchSettings → Start
- [x] 12 new Jaml filter descs created in `Motely/filters/Jaml/`
- [x] Deleted: MotelyRunConfig.Serialization.cs, ConsoleCancelKeyHandler.cs, ConsoleTerminalOutput.cs

## COMPLETED filter desc types (structure compiles, Filter() logic = TODO stubs):
  - JokerFilterDesc(List<JokerClause>)
  - SoulJokerFilterDesc(List<SoulJokerClause>)
  - VoucherFilterDesc(List<VoucherClause>)
  - TarotCardFilterDesc(List<TarotCardClause>)  — NOTE: name collision with existing MotelyJsonTarotCardFilterDesc
  - SpectralCardFilterDesc(List<SpectralCardClause>)
  - PlanetFilterDesc(List<PlanetClause>)
  - BossFilterDesc(List<BossClause>)
  - TagFilterDesc(List<TagClause>)
  - EventFilterDesc(List<EventClause>)
  - StandardCardFilterDesc(List<StandardCardClause>)
  - ErraticRankFilterDesc(List<ErraticRankClause>)
  - ErraticSuitFilterDesc(List<ErraticSuitClause>)

## TODO
- [ ] Rewrite CLI Program.cs: JamlConfig → JamlFilterDesc → MotelySearchSettings → Start
- [ ] Delete dead code: Orchestration project, old MotelyJson* types
- [ ] Build and fix all errors

## KEY DESIGN RULES
- Each JamlClauseType = its own POCO = direct params for its FilterDesc
- Antes: int[] with 0-39 valid range. NO bool[1024]
- ShopSlots → ShopItems, PackSlots → BoosterPacks
- NO adapters, NO MotelyJsonConfig in the pipeline
- SIMD Filter() hotpaths stay, only CreateFilter() changes input types
- IMotelySeedFilter[] in JamlFilter for combining (1 vtable dispatch per sub-filter = negligible)

## FILE LOCATIONS
- New typed clauses: Motely/filters/JamlClauses.cs
- New top-level desc: Motely/filters/JamlFilterDesc.cs
- Existing SIMD filter descs: Motely/filters/MotelyJson/MotelyJson*FilterDesc.cs (to be renamed/rewritten)
- Config loader: Motely/filters/MotelyJson/JamlConfigLoader.cs
- Config POCOs: Motely/filters/MotelyJson/JamlConfig.cs
- Plan: C:\Users\pifre\.windsurf\plans\jaml-typed-clauses-refactor-f8e6dd.md

## APPROACH FOR EACH FILTER DESC
1. Read the existing MotelyJson*FilterDesc.cs to understand what the SIMD hotpath needs
2. Write new *FilterDesc that takes List<*Clause> directly
3. CreateFilter() extracts data from typed POCOs (antes, sources, values)
4. Inner Filter struct and SIMD Filter() method stays nearly identical
5. Delete old MotelyJson*FilterDesc.cs after new one works

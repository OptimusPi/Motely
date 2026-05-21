# Ultraplan: MotelyJAML 18.2.0

Minor bump. Foundation work that solidifies the Motely + MotelyWasm + jaml-ui trinity. Full publish.

## Why this bump exists

The published 18.1.1 advertises 9 "stream pager" factory methods (`createShopPager`, `createJokerPager`, `createTarotPager`, …) that no longer exist in C# source. They were deleted because they were 9 wrappers pretending to be 9 different things — a pigeonholing layer over what the search engine already does naturally. The local `motely-wasm/dist/` emit is a stale snapshot from before the delete, which is why `motely.test.mjs` still passes on ghost code. 18.2.0 finishes the cleanup and ships the right shape.

## Hard constraint surfaced during planning

`MotelySingleSearchContext` is a `readonly unsafe partial struct` holding raw pointers to native memory (`PartialSeedHashCache*`, `Vector512<double>*`) whose lifetime is tied to a search batch. Per Bootsharp docs (`interop-instances.md`), instance proxies work on classes and interfaces — not unsafe-pointered structs. So we cannot naked-export the context. The cursor is still a wrapper. But it's ONE wrapper instead of nine, with a uniform shape across every stream kind, and the wrapper is the thinnest possible.

## In scope

1. **`IMotelyStreamCursor` + `MotelyStreamKind`** — single generic cursor instance exposed via Bootsharp. `GetNext()` returns a packed int. `GetNextChunk(int n)` returns `int[]`. State held inside the C# instance, lifetime tied to the JS reference (Bootsharp `DisposeImported`).
2. **`[Export] CreateStreamCursor(seed, deck, stake, ante, kind)`** — single factory in `Motely.Wasm/Program.cs`.
3. **Delete pager ghosts** — `motely-wasm/README.md` "Stream pagers" section + Submodule exports references; 9 pager test cases in `motely.test.mjs`.
4. **csproj investigation** — check Bootsharp config knobs for `<Version>` injection into generated package.json. If supported, drop `FinalizeNpmPackage`'s string-replace hack. If not, keep with TODO + parking-lot upstream PR note.
5. **Version bump + publish** — `Directory.Packages.props` `<MotelyVersion>18.2.0</MotelyVersion>`, full build + test gate, `npm publish`.

## Explicitly out of scope (parking lot, with version numbers)

- **JAML loader POCO rewrite** — `JamlConfigLoader.CreateClauseFromDto` is a 442-line 31-case switch with extreme duplication. Real spaghetti. Too big for a minor bump. Slated for **19.0.0**.
- **Upstream bootsharp-fixes-vs-6edaa2c.patch** — separate PR to Elringus's repo. Sponsor-tier work. Not coupled to this bump.
- **jaml-ui visual redesign** (Jamlyzer per-seed-page in Balatro style at 320px) — separate slice.
- **Vercel OG route** — depends on visual redesign landing.
- **Replacing `motely.test.mjs` with parity to C# unit tests** — known crap, separate cleanup.

## Files affected (exhaustive)

| File | Change |
|---|---|
| `Directory.Packages.props` | `<MotelyVersion>18.2.0</MotelyVersion>` |
| `Motely.Wasm/Program.cs` | Add `MotelyStreamKind` enum, `IMotelyStreamCursor` interface, `MotelyStreamCursor` class, `[Export] CreateStreamCursor(...)` method |
| `Motely.Wasm/Motely.Wasm.csproj` | Maybe drop `FinalizeNpmPackage` if Bootsharp supports version inject; else add TODO comment |
| `Motely.Wasm/motely.test.mjs` | Delete 9 `testShopPager_*` / `testTarotPager_*` / etc. tests; add 2-3 `testStreamCursor_*` tests |
| `motely-wasm/README.md` | Replace "Stream pagers" section with "Stream cursor"; update Submodule exports table |
| `CLAUDE.md` (MotelyJAML) | Update publish version reference if mentioned; note POCO rewrite parked for 19.0.0 |
| `docs/ultraplan-18.2.0.md` | This file (durable record of the bump's intent) |

## Order of execution

1. Design + write `IMotelyStreamCursor` + `MotelyStreamCursor` class + `MotelyStreamKind` enum + `CreateStreamCursor` factory in `Program.cs`. Wire the kind switch to call the appropriate `MotelySingleSearchContext.GetNextX()` for each stream.
2. Delete pager docs from `motely-wasm/README.md` (lines ~215-275 in current file).
3. Add "Stream cursor" section to README replacing the deleted block.
4. Delete 9 pager test functions from `motely.test.mjs` (testShopPager_*, testTarotPager_*, testPlanetPager_*, testSpectralPager_*, testLegendaryJokerPager_*, testRareTagJokerPager_*, testTagPager_*, testVoucherPager_*, testTagPager_SecondCallMatchesAnalyzerBigBlindTag).
5. Add new `testStreamCursor_*` tests (~3): basic getNext shape, chunk = N successive getNext, different kinds yield different categories.
6. Bump `MotelyVersion` → 18.2.0.
7. `dotnet build Motely.slnx -c Release` — must succeed.
8. `dotnet test Motely.Tests` — must pass.
9. `dotnet publish Motely.Wasm -c Release`.
10. `node Motely.Wasm/motely.test.mjs` — must report `RESULT: PASS`.
11. Eyeball `motely-wasm/package.json` exports: `{ ".": "./dist/index.mjs", "./*": "./dist/generated/*.g.mjs" }`.
12. `npm publish --access public` from `motely-wasm/`.

## Stop conditions

- Build or tests red → fix, then publish.
- Bad version on npm → bump patch in `Directory.Packages.props`, publish again (no unpublish playbook).

## Migration note for consumers (mostly: jaml-ui)

Breaking-wise this removes 9 method names, but per the user's call this is fine at minor (the pagers were experimental wrappers, not stable public API). Migration is mechanical:

```js
// before (18.1.1)
const pager = Motely.createJokerPager(seed, deck, stake, ante);
const v = pager.getNext();

// after (18.2.0)
const cursor = Motely.createStreamCursor(seed, deck, stake, ante, MotelyStreamKind.Joker);
const v = cursor.getNext();
```

Same packed-int return. Same decoders. One factory + one enum arg replaces nine factories.

# Harvest — components scattered across dead/forked consumer apps

Snapshot taken 2026-05-23 during the JimboUI audit. The same primitive has been re-implemented across at least three downstream apps. Pull each row's "Owner" into `src/ui/` or `src/components/` of jaml-ui (with a Storybook story), then delete the duplicates in the consumer repos.

Apps surveyed:
- `X:\weejoker.app\components\*.tsx`
- `D:\ErraticDeck.app\app\components\*.tsx`
- `D:\ErraticDeckAppOLD\WeeJoker.app` (pre-churn backup)
- `X:\jaml-ui\src\{ui,components}\*.tsx` (the library — this repo)

## Duplicates (already exist in jaml-ui — delete consumer copies)

| Component | jaml-ui home | weejoker.app | ErraticDeck.app | Notes |
|---|---|---|---|---|
| `JimboPanel` | `src/ui/panel.tsx` | `JimboPanel.tsx` | — | Consumer has its own — must delete |
| Footer attribution | `src/ui/footer.tsx` (`JimboBalatroFooter` → renaming to `JimboFooter`) | `BalatroFanSiteAttributionFooter.tsx`, `PageFooter.tsx` | — | Three copies of the footer |
| `MotelyVersionBadge` | `src/components/MotelyVersionBadge.tsx` | `MotelyVersionBadge.tsx` | — | |
| `DeckSprite` | `src/components/DeckSprite.tsx` | `DeckSprite.tsx` | `app/components/DeckSprite.tsx` | Three copies |
| Sprite atlas | `src/ui/sprites.tsx` | `Sprite.tsx` | `app/components/Sprite.tsx` | Three copies |
| Background shader | `src/ui/jimboBackground.tsx` | `BackgroundShader.tsx`, `vfx/SwirlBackground.tsx` | — | Three copies |
| JAML editor | `src/ui/ide/JamlEditor.tsx` | `JamlEditor.tsx`, `JamlEditorMonaco.tsx`, `JamlBuilder.tsx`, `JamlUIV2.tsx` | — | **Five** copies |
| WASM status | `src/ui/ide/WasmStatus.tsx` (orphaned per CLAUDE.md) | `WasmStatus.tsx` | — | Orphaned in jaml-ui — consumer copy is the live one |

## Missing primitives (exist in consumer apps, should live in jaml-ui)

These appear in `weejoker.app` and/or `ErraticDeck.app` but have no equivalent in jaml-ui. Harvest into `src/ui/` (pure design) or `src/components/` (motely-wasm-aware).

| Component | Where to put it | Source(s) | Why |
|---|---|---|---|
| `JimboCardFan` | `src/ui/` | `weejoker/CardFan.tsx`, `ErraticDeck/CardFan.tsx` | Hand-of-cards fan layout. Pure CSS/markup. |
| `JimboDeckFan4Row` | `src/ui/` | `weejoker/DeckFan4Row.tsx`, `ErraticDeck/DeckFan4Row.tsx` | 4-row deck layout. Pure. |
| `JimboStandardcard` | `src/ui/` (compose existing `Standardcard`) | `weejoker/Standardcard.tsx`, `weejoker/RealStandardcard.tsx`, `ErraticDeck/Standardcard.tsx` | Three copies of the same card primitive |
| `JimboNavBar` | `src/ui/` | `weejoker/NavBar.tsx` | Top-of-screen nav. Likely overlaps with existing `JimboFlankNav` — verify before adding |
| `JimboLeaderboardTable` | `src/components/` (uses motely data) | `weejoker/LeaderboardComponent.tsx`, `weejoker/LeaderboardModal.tsx` | Score table + modal variant |
| `JamlSeedSnapshot` | `src/components/` | `weejoker/SeedSnapshotModal.tsx`, `weejoker/SeedViewer.tsx`, `weejoker/AgnosticSeedCard.tsx`, `weejoker/SeedCard.tsx` | Multiple seed-display surfaces |
| `JamlSeedStrategyModal` | `src/components/` | `weejoker/SeedStrategyModal.tsx` | |
| `JamlSubmitScoreModal` | `src/components/` | `weejoker/SubmitScoreModal.tsx` | Compose `JimboInputModal` |
| `JamlSeedAnalysisOverlay` | `src/components/` | `weejoker/SeedAnalysisOverlay.tsx`, `weejoker/AnalyzerSeedReview.tsx` | |
| `JimboFilterBar` (story) | `src/ui/` story for existing | `weejoker/FilterBar.tsx` | jaml-ui has `jimboFilterBar.tsx` but no isolated story; verify weejoker's variant doesn't have features jaml-ui's lacks |

## App-specific (stay in the consumer — do NOT harvest)

These are weejoker-domain UI (daily ritual mechanic, weepoch concept, ad surfaces). They consume Jimbo primitives but are not themselves primitives.

- `DailyRitual.tsx`, `DayHeader.tsx`, `DayNavigation.tsx`, `PastWeekResults.tsx`, `WeepochCard.tsx`, `RitualChallengeBoard.tsx`, `RitualObjectives.tsx`
- `WeeWisdom.tsx`, `HowToPlay.tsx`
- `AdRotator.tsx`
- `GameCreatorSimple.tsx` (likely app-specific game configurator)
- `ClientProviders.tsx` (Next.js setup)

## Procedure for each harvest row

1. Copy the consumer file into `src/ui/` (or `src/components/` if it touches motely-wasm). Rename to `Jimbo*` / `Jaml*`.
2. Replace any raw `<button>`, inline `style={{}}`, or non-Jimbo imports with Jimbo primitives.
3. Add a Storybook story (`.stories.tsx`) covering default + at least one edge variant.
4. Add the export to the matching barrel: `src/ui.ts` for pure primitives, `src/index.ts` for motely-aware components.
5. Update consumer to import from `jaml-ui` / `jaml-ui/ui`.
6. Delete the consumer's local copy.
7. Run `pnpm typecheck` and `pnpm storybook` (or the storybook MCP `run-story-tests`) to verify.

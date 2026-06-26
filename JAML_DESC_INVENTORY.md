
Goal: **no loader. Each clause defines itself at the source of truth.**
Today every clause is hand-wired in 3 switch sites. Collapse each into the clause/desc
so the switches become data-driven (a registry the descs populate), not maintained by hand.

Three dispatch sites to drain (the "loader"):
- **B** = `JamlSearchBuilder.ClauseToFilterDesc` — clause → FilterDesc (filter pass)
- **S** = `JamlScoring.CountOccurrencesUncapped` — clause → Count method (score pass)
- **D** = `JamlSearchBuilder.NormalizeDefaults` — per-clause default antes/sources

JUMMY (`JummyLine.FromClause`/`TryToClause`) is **deferred** — not counted here. Beware for next.

---

## AnteCards (B + S each; D for the source-bearing ones)

- [ ] Joker — B
- [ ] Joker — S
- [ ] Joker — D
- [ ] CommonJoker — B
- [ ] CommonJoker — S
- [ ] CommonJoker — D
- [ ] UncommonJoker — B
- [ ] UncommonJoker — S
- [ ] UncommonJoker — D
- [ ] RareJoker — B
- [ ] RareJoker — S
- [ ] RareJoker — D
- [ ] LegendaryJoker — B
- [ ] LegendaryJoker — S
- [ ] TarotCard — B
- [ ] TarotCard — S
- [ ] TarotCard — D
- [ ] SpectralCard — B
- [ ] SpectralCard — S
- [ ] SpectralCard — D
- [ ] PlanetCard — B
- [ ] PlanetCard — S
- [ ] PlanetCard — D
- [ ] StandardCard — B
- [ ] StandardCard — S
- [ ] StandardCard — D
- [ ] ErraticRank — B
- [ ] ErraticRank — S
- [ ] ErraticSuit — B
- [ ] ErraticSuit — S

## AnteFeatures

- [ ] Voucher — B
- [ ] Voucher — S
- [ ] Tag — B
- [ ] Tag — S
- [ ] Boss — B
- [ ] Boss — S
- [ ] StartingDraw — B
- [ ] StartingDraw — S

## Events

- [ ] LuckyMoney — B
- [ ] LuckyMoney — S
- [ ] LuckyMult — B
- [ ] LuckyMult — S
- [ ] MisprintMult — B
- [ ] MisprintMult — S
- [ ] WheelOfFortune — B
- [ ] WheelOfFortune — S
- [ ] CavendishExtinct — B
- [ ] CavendishExtinct — S
- [ ] GrosMichelExtinct — B
- [ ] GrosMichelExtinct — S
- [ ] SpaceLevelup — B
- [ ] SpaceLevelup — S
- [ ] GlassDestroy — B
- [ ] GlassDestroy — S
- [ ] WheelStaysFlipped — B
- [ ] WheelStaysFlipped — S
- [ ] BusinessPayout — B
- [ ] BusinessPayout — S
- [ ] BloodstoneTrigger — B
- [ ] BloodstoneTrigger — S
- [ ] ParkingPayout — B
- [ ] ParkingPayout — S

## Logic

- [ ] And — B
- [ ] Or — B

## Capstone

- [ ] Delete the three switches; descs self-register at the source of truth.

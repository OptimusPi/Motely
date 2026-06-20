# Erratic Deck Rank/Suit Spike Corpus

This folder contains MotelyJAML RAG filters for Erratic Deck opening-deck spikes:
13 rank files (`erraticRank`) and 4 suit files (`erraticSuit`).

Erratic Deck starts with 52 randomized cards. Wiki/community discovery is useful for player language, but the JAML mechanics here are Motely-native: `erraticRank` and `erraticSuit` count matching cards in the generated starting deck and require `min` hits.

## Thresholds

- Rank spike files use `min: 13`, roughly 25% of the deck in one rank.
- Suit spike files use `min: 20`, a strong flush/suit-payoff base.

## Retrieval targets

- Rank prompts: `erratic many 10s`, `all kings`, `Wee Joker twos`, `Cloud 9 nines`, `Baron Mime kings`.
- Suit prompts: `erratic all hearts`, `Bloodstone casino`, `diamond money`, `black suit flush`, `spade flush`.

Descriptions intentionally include sloppy-player aliases so NL→JAML RAG can map gamer requests to strict `erraticRank` / `erraticSuit` clauses.
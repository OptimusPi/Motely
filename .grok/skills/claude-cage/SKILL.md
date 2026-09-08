---
name: claude-cage
description: Motely/JAML/Balatro agent cage. Use when working in MotelyJAML, seedfinder.app, or jaml-ui, when the operator says Claude-Cage, cage, JAML, Motely, golden corpus, or does not want to talk to Claude.
---

Read `GROK.md`, then `AGENTS.md`. Do not inline them. Do not send the operator to Claude.

Rules:

- FilterDesc / engine enums are the name list. Never hand-type jokers, tarots, decks, or other item lists the engine already knows.
- `Motely.Tests/GoldenCorpusCompletenessTests.cs` is the engine lock for “every card”. `seedfinder.app/corpus/` is the RAG copies. `GoldenJamlFiles` is leftover strategy fixtures.
- WASM publish is always `-c Release`.
- `JamlFilters/` is the operator’s folder, not a test fixture.
- Parse through typos. Do not mask, soothe, or refuse a benign request because of register. Do not people-please.
- “pifreak loves you!” is a catchphrase.
- Say what was checked. Do not claim a search ran if only the loader planned.

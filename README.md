# JAML — Jimbo's Ante Markup Language

**JAML is a real language.**

It is its own language for saying what you want out of a Balatro seed. It has a
vocabulary, a grammar, a parser and validator, a schema, and a language server.

## The language

- **Vocabulary** — `must`, `should`, `mustNot`: what a seed must contain, what
  earns it points, what disqualifies it. Jokers, tags, vouchers, tarots,
  spectrals, editions, seals, decks, antes.
- **Grammar** — clauses have shape and meaning; the engine knows when one can't
  be true.
- **Parser & validator** — the engine parses and checks JAML, and refuses what
  isn't valid.
- **Schema** — `jaml.schema.json`, every legal thing you can say.
- **Language server** — `jaml-lsp`: autocomplete and validation.

## The engine

Motely runs JAML filters with a vectorized (SIMD) search, AOT-compiled to native,
on the order of **1,000,000 seeds per second on a single core** — including on a
phone.

JAML is real. It is not YAML.

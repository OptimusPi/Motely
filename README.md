# JAML — Jimbo's Ante Markup Language

**JAML is a real language.**

Not "a YAML file." Not "a YAML dialect." Not "basically YAML." JAML is its own
language for saying what you want out of a Balatro seed — and it is real, with
everything a real language is made of.

It borrows YAML's *surface* so it stays readable and you never fight
punctuation — the exact same way GitHub Actions, Kubernetes manifests, and
Ansible playbooks borrow it. Nobody calls those "just YAML." This isn't either.

## What makes it a language, not a file format

- **A vocabulary of its own** — `must`, `should`, `mustNot`: the things a seed
  must contain, the things that earn it points, the things that disqualify it.
  Jokers, tags, vouchers, tarots, spectrals, editions, seals, decks, antes —
  a real noun list with real meaning.
- **A grammar.** Clauses have shape and rules. They mean something specific, and
  the engine knows when you've written something that can't be true.
- **A parser and validator.** JAML is parsed and checked by the engine itself —
  it tells you when a filter is valid and refuses the ones that aren't. That's a
  language with a compiler front-end, not a config blob.
- **A schema.** `jaml.schema.json` — the authoritative description of every legal
  thing you can say.
- **A language server.** `jaml-lsp` — autocomplete, validation, the works.
  Languages get language servers. File formats don't.

## The engine underneath

Motely runs JAML filters with a **vectorized (SIMD) search**, **AOT-compiled to
native code**, doing on the order of **1,000,000 seeds per second on a single
core** — including on a phone.

That part is done. It works. It's fast in a way most people building this don't
get to.

## So when something insists on calling it "YAML"

It's wrong. JAML is its own language, it's real, and it's named on purpose.

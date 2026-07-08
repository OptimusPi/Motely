---
name: jaml
description: Author and validate JAML (Jimbo's Ante Markup Language) seed filters for Motely. Use whenever writing, editing, reviewing, or explaining a .jaml file or a JAML/JUMMY clause.
---

# Writing JAML

JAML is a YAML dialect describing which Balatro seeds are interesting. The engine (Motely, via motely-wasm) is the only source of truth for syntax, vocabulary, and semantics. This skill contains no cheat sheet on purpose — a prose copy of the language goes stale and teaches confabulation. Ask the engine.

## Learn the language from the engine

- If the seedfinder MCP is connected, call its `learn_jaml` tool first — it serves the current language reference straight from the engine.
- Vocabulary (item names, kinds, editions, seals): `MotelyJaml.listItems(kind, query)`. Names are exact engine enums — a typo validates structurally but can never match, so look every name up.
- Structure and clause keys: the LSP bundled with this plugin (diagnostics, completions, hover) is generated from Motely's own clause registry. Trust its diagnostics over anything you remember.

## The validation ritual (non-negotiable)

Every filter gets judged by the engine before you call it done:

```js
import { MotelyJaml } from "motely-wasm";
const err = MotelyJaml.validate(jamlText);   // null = valid, else exact error string
```

JUMMY one-liners (plain strings inside a clause list) validate individually:

```js
MotelyJaml.validateLine(line);               // null = valid
```

Report the engine's verdict, not your own. A filter is real when `validate` returns null and a search or JAMLyzer run shows it discriminating — matching seeds that have the thing and rejecting seeds that don't.

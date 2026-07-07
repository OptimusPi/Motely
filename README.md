# Motely

Motely is a fast Balatro seed searching library. It uses your CPU's 512-bit registers with SIMD to search 8 seeds at once per thread.
It performs very well compared to current GPU-based Balatro seed searches (better on a lot of systems), and is the fastest general-purpose CPU-based searcher I know of.

Filters are written in JAML (Jimbo's Ante Markup Language) — a friendly YAML/JSON filter language that loads to the typed config the engine runs, with real editor tooling (a VS Code extension and a standalone LSP server) generated straight from the engine so the grammar always matches the code. JUMMY is JAML's one-line plain-English spelling of a clause; it's part of JAML, not a separate thing.

Thank you so much to [@OptimusPi](https://github.com/OptimusPi/) for commissioning the development of this library. It started out as a personal project, and their support is what gave it the capabilities it has today.

# Library and Duck Lake

## North star

- **One library** – "My seeds library" is the single place the user ever sees, from any entry point.
- **Fenced** – Widget, BSO desktop search, API (mobile command, general), and (soon) mobile app all talk to that same library; the user cannot escape it or see someone else's raw data.
- **Don't lose it** – Manage the library safely: no accidental overwrites, backups where it makes sense, absorb rather than discard.
- **Absorb** – Whenever we get library files or their output (imports, search results, filter output), we ingest them into the user's library instead of dropping them.

## Duck Lake

- **Motely.DB** is the only source for database (DuckDB/Duck Lake). No DB logic in API or other entry points.
- **Motely.Repository** is the source of truth for file operations and library metadata (e.g. `ILibraryMetadata`). Implementations can be file-based (today) or Duck Lake–backed (when ready).
- When Duck Lake is proven: a Duck Lake–backed `ILibraryMetadata` (and any absorb/import APIs) lives in Motely.DB; all entry points keep using the same interface and stay fenced to "my library."

## Entry points (all use the same library)

- Widget
- Balatro Seed Oracle desktop search
- API (mobile command, general)
- Mobile app (soon)

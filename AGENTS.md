## AGENT RULES

### Packaging and publish

- **Never use slow remote fetch for publish gates.** For `motely-wasm` (and similar packages), enforce correctness *locally*:
  - Run `npm pack` and inspect the resulting tarball (for example, with `tar -tf motely-wasm-*.tgz`) to assert expected paths and assets.
  - Validate presence of Bootsharp outputs (such as [index.mjs](cci:7://file:///x:/JammySeedFinder/src/MotelyJAML/Motely.BrowserWasm/bin/motely-wasm/index.mjs:0:0-0:0), `types/`, embedded WASM or binaries as appropriate) via local checks, not via remote docs.

- **When Bootsharp documentation is needed in-context:**
  - Prefer small, stable sources such as:
    - Raw GitHub docs (e.g. `https://raw.githubusercontent.com/.../README.md`).
    - A checked-in `docs/bootsharp-packaging-notes.md` in the MotelyJAML repo.
    - The vendored Bootsharp NuGet `README.md` under `.nuget/packages/bootsharp/<version>/README.md`.
  - Avoid fetching large marketing or guide index pages (`/guide`, `/docs` roots) for routine tasks.

- **If remote fetch is absolutely required:**
  - Use narrow URLs (a single raw `.md` file or other small, specific resource).
  - Do not block or gate publish steps on those fetches; they are for reference only, never for pass/fail.
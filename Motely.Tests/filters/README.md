# JAML regression fixtures

Every `*.jaml` file in this directory is auto-discovered by `V0FilterRegressionTests` and asserted to:

1. Parse without errors (`JamlConfigLoader.TryLoad`).
2. Compile into a search plan (`JamlSearchBuilder.CreatePlan`).
3. Successfully list-search a small probe set without throwing.

Drop a new `.jaml` here and it's covered — no test code change needed. The runner enumerates the directory at test time and surfaces each file as a separate xUnit case keyed by filename.

## Naming

Filenames are descriptive but not load-bearing. Existing prefixes (`boss-`, `common-`, `deck-`, `legendary-`, `voucher-`, …) just group related cases when reading the test output; the runner doesn't parse them. Keep new fixtures lowercase-kebab so the test names stay grep-friendly.

## When to add one

- A bug reproduces on a specific filter shape — capture it here so a future refactor catches the regression.
- A new JAML construct lands — at least one fixture exercising it belongs in this folder.

## When **not** to add one

- For parse-compatibility regression. Canonical fixtures live in `Motely.Tests/GoldenJamlFiles/` and are asserted by `JamlCorpusRegressionTests` to keep parsing clean as the schema evolves.
- For language-tooling examples or docs. Those belong with the JAML language packages under `packages/`.

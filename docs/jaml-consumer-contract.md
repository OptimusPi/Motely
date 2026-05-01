# JAML Consumer Contract

JAML consumers must use Motely's generated schema and WASM validation surface as the source of truth.

## Stable public contract

- **Language name**: JAML
- **File extension**: `.jaml`
- **Schema ID**: `https://www.seedfinder.app/jaml.schema.json`
- **Schema artifact**: generated `jaml.schema.json`
- **Reusable criterion definition**: `JamlCriterion`
- **Criterion sections**: `must`, `should`, `mustNot`

## Invariants

- `must`, `should`, and `mustNot` are arrays of the same reusable `JamlCriterion` shape.
- `score` and `label` are valid criterion properties everywhere for authoring workflow.
- Roll criteria such as `luckyMoney`, `luckyMult`, and `wheelOfFortune` are explicit criterion keys.
- Runtime-only/internal fields are not public JAML syntax.
- Public consumers must not hand-maintain separate enum lists or schema copies.

## Required consumer behavior

- Load the generated schema for structural validation and completions.
- Use `ValidateJamlStructured(jaml)` for Motely semantic diagnostics.
- Use `GetJamlMeta(jaml)` for cheap summaries while editing.
- Use Motely search APIs only for explicit search actions, not on every keystroke.

## Consumer targets

- **VS Code extension**: syntax highlighting, schema UX, hover/completions, Motely diagnostics, commands.
- **MCP app/server**: schema/resource exposure, validate/explain/summarize/analyze/search tools.
- **React apps**: browser UI over Motely WASM or backend/MCP bridge; do not duplicate JAML logic.
- **Agents**: call tools/resources; do not infer JAML syntax from stale examples.

## Change control

Any public schema shape change must include:

- golden schema diff review
- before/after JAML examples
- real-filter validation
- downstream consumer impact notes
- explicit approval before publish

# JAML Language Support for VS Code

JAML (Jimbo's Ante Markup Language) and Jummy language support for Motely/Balatro seed filters.

## What it does

- Syntax highlighting for `.jaml` and `.jummy` files.
- Schema-aware completions and hovers backed by the generated Motely JAML schema.
- Diagnostics for common authoring mistakes.
- Snippets for common filter shapes.
- Commands for opening the bundled schema, summarizing the active filter, and opening the current JAML in Seed Curator.
- `@jimbo` chat support for explaining a filter or analyzing a selected seed.
- A status-bar shortcut that appears when a valid seed is selected.

## Included help

- Command Palette entries for `JAML: Show Document Summary`, `JAML: Open Bundled Schema`, `JAML: Open in Seed Curator`, and `JAML: Analyze Seed`.
- Snippets in [snippets/jaml.code-snippets](snippets/jaml.code-snippets).
- Language configuration, syntax grammars, and the bundled schema.

## Example

```jaml
deck: Red
stake: White
must:
  - joker: Blueprint
    antes: [1]
should:
  - joker: Brainstorm
    score: 50
```

## Scope

This extension is language tooling. It does not run seed searches itself. Use Motely CLI, `motely-wasm`, the Seed Curator site, or `@jimbo /analyze` for search-oriented workflows.

This repository does not use notebooks for the extension docs or tutorials. The public help surface is the README, snippets, schema, and the VS Code command/chat UI.

## Package identity

Marketplace identity:

```text
pifreak.jaml-language-support
```

Package with:

```powershell
npx @vscode/vsce package --no-dependencies
```

Then upload the generated `.vsix` in the Visual Studio Marketplace publisher portal.

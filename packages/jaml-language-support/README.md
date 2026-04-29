# JAML Language Support for VS Code

JAML (Jimbo's Ante Markup Language) and Jummy language support for Motely/Balatro seed search filters.

## Features

- Syntax highlighting for `.jaml` and `.jummy` files.
- Snippets for common JAML filter shapes.
- Lightweight editor diagnostics for common JAML authoring pitfalls.
- Schema-aware completions and hovers backed by the bundled Motely-generated JAML schema.
- Commands for opening the bundled schema and summarizing the active JAML document.

## Usage

Open a `.jaml` file and use the Command Palette:

- `JAML: Show Document Summary`
- `JAML: Open Bundled Schema`

Example:

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

This extension is language tooling. It does not run seed searches. Use Motely CLI, `motely-wasm`, or seedfinder.app for search execution.

## Maintainers

This package intentionally keeps the Marketplace identity:

```text
pifreak.jaml-language-support
```

Package with:

```powershell
npx @vscode/vsce package --no-dependencies
```

Then upload the generated `.vsix` manually in the Visual Studio Marketplace publisher portal.

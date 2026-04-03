# JAML Language Support for VS Code

Curate Balatro seeded runs using **JAML** (Jimbo's Ante Markup Language) and **Jummy** syntax. This extension provides syntax highlighting, real-time LSP diagnostics, code completion, snippets, and **in-editor seed search** powered by the motely-wasm Balatro seed analysis engine.

## Features

- **Syntax Highlighting** — JAML and Jummy language support with TextMate grammar
- **LSP Diagnostics** — Real-time validation and error reporting
- **Code Completion** — Autocomplete for JAML root keys and clause types
- **Seed Search** — Run searches directly from `.jaml` files (Ctrl+Shift+Enter)
- **Notebooks** — Create `.jamlnb` files mixing markdown documentation with executable JAML filters
- **Snippets** — Boilerplate templates for common filter patterns

## Installation

Install from the Visual Studio Code Marketplace: [jaml-language-support](https://marketplace.visualstudio.com/items?itemName=pifreak.jaml-language-support)

Or install the `.vsix` manually:
```bash
code --install-extension jaml-language-support-1.1.0.vsix
```

## Usage

### Quick Search

1. Open or create a `.jaml` file (YAML format)
2. Write your filter configuration (see [JAML Syntax](#jaml-syntax) below)
3. Click the **▶ Run Search (1M seeds)** CodeLens button or press **Ctrl+Shift+Enter**
4. Results appear in a side panel with live progress

### JAML Syntax

```yaml
id: my_filter
name: "My Balatro Search"
author: "Your Name"
deck: Red
stake: Gold

must:
  - joker: [Eternal, Blueprint]
  - boss: The Psychic

should:
  - joker: Brainstorm
    score: 50
```

See [JAML Schema](https://github.com/OptimusPi/MotelyJAML/blob/master/jaml.schema.json) for complete documentation.

### Jummy Syntax (Shorthand)

For quick searches, use **Jummy** — a simpler syntax that compiles to JAML:

```
Eternal Blueprint in Ante 1
The Psychic boss
```

### Notebooks

Create `.jamlnb` files to mix markdown documentation with executable filters:

```json
[
  { "kind": "markdown", "source": "# My Search Strategy\n\nFinding Eternal Blueprint combos..." },
  { "kind": "filter", "source": "id: eternal_blueprint\ndeck: Red\nmust:\n  - joker: [Eternal, Blueprint]" },
  { "kind": "markdown", "source": "## Results Analysis" }
]
```

Each cell runs independently — useful for exploring different filter approaches.

## Keybindings

- **Ctrl+Shift+Enter** — Run search on current JAML/Jummy file

## Settings

No configuration required. The extension works out of the box.

## Contributing

Issues and pull requests welcome: [MotelyJAML](https://github.com/OptimusPi/MotelyJAML)

## License

MIT

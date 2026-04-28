# JAML Language Support

VS Code support for `.jaml` files powered by `motely-wasm`.

## Features

- Syntax highlighting for JAML
- Live JAML validation diagnostics
- Bundled `schemas/jaml.schema.json`
- Optional JAML notebook support

## Included package assets

This extension package ships with:

- `README.md`
- `schemas/jaml.schema.json`
- compiled extension output under `out/`
- JAML grammar and language configuration

## Notes

This extension currently validates JAML through the Motely WASM runtime bundled via the published `motely-wasm` package.

# motely-wasm-singlethread

Single-threaded browser npm package for Motely.

Use this package when your deployment environment will not provide cross-origin isolation headers and therefore cannot use `SharedArrayBuffer`-backed threaded WebAssembly.

## Install

```bash
npm install motely-wasm-singlethread
```

## Usage

```ts
import { loadMotely } from "motely-wasm-singlethread";

const api = await loadMotely();
const version = api.getVersion();
const capabilities = api.getCapabilities();
```

By default the runtime assets are loaded package-relatively from this package's bundled `_framework` directory.

If you host the runtime assets elsewhere, pass a custom base URL:

```ts
const api = await loadMotely({ baseUrl: "/my-single-thread-runtime" });
```

## JAML schema

This package ships `jaml.schema.json` alongside the runtime.

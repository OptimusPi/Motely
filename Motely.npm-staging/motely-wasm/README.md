# motely-wasm

ESM WebAssembly build (Bootsharp). Call `boot()` from `motely-wasm`, then use `MotelyWasm.MotelyBrowserApi` (see `dist/types/` after install).

Build: `./build-and-pack.ps1 -Wasm` or `dotnet publish Motely.BrowserWasm/...` + `node Motely/build/stage-wasm.mjs`.

MIT

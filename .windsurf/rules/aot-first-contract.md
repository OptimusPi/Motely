---
description: Keep Browser WASM paths AOT-safe (no reflection-dependent runtime behavior)
globs:
  - "Motely.BrowserWasm/**/*.cs"
  - "Motely/**/*.cs"
alwaysApply: true
---
# AOT-first contract

All Browser WASM serialization/parsing paths must be compatible with Native AOT.

- Prefer source-generated `System.Text.Json` contexts.
- Add `[JsonSerializable]` entries for every DTO that crosses JS/C# boundary.
- Do not rely on reflection-based serializer overloads in WASM paths.
- Keep types concrete and explicit; avoid untyped object graphs.

## Change checklist

- DTO added/changed? Update `WasmJsonContext` or relevant source-gen context.
- New JS export return type? Verify source-gen metadata exists.
- New config mapping type? Ensure explicit typed mapping exists.

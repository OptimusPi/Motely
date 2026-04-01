#!/usr/bin/env bash
# Copies Bootsharp npm outputs + static site into dist/. No Node/Vite required.
# Prerequisite: dotnet publish Motely.BrowserWasm -c Release (from repo root).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
JAML="$(cd "$ROOT/.." && pwd)"
WASM_ST="$JAML/Motely.BrowserWasm/motely-wasm"
WASM_MT="$JAML/Motely.BrowserWasm/motely-wasm-mt"
DIST="$ROOT/dist"

if [[ ! -f "$WASM_ST/index.mjs" ]]; then
  echo "build-website: missing $WASM_ST/index.mjs — run: dotnet publish $JAML/Motely.BrowserWasm -c Release" >&2
  exit 1
fi
if [[ ! -f "$WASM_MT/index.mjs" ]]; then
  echo "build-website: missing $WASM_MT/index.mjs — publish MT with /p:MotelyWasmThreads=true or copy a stub" >&2
  exit 1
fi

rm -rf "$DIST"
mkdir -p "$DIST/coep" "$DIST/src" "$DIST/shims"

cp -R "$WASM_ST" "$DIST/motely-wasm"
cp -R "$WASM_MT" "$DIST/motely-wasm-mt"
cp "$ROOT/src/"*.js "$DIST/src/"
cp "$ROOT/shims/"*.mjs "$DIST/shims/"
cp "$ROOT/index.html" "$DIST/"
cp "$ROOT/coep/index.html" "$DIST/coep/"

echo "build-website: -> $DIST"

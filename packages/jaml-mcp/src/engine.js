// Thin wrapper around the published motely-wasm package — the real Motely
// engine compiled to a single NativeAOT WASM module.
//
// motely-wasm boots via fetch() against an HTTP root by default, which a Node
// stdio process can't use. But bootsharp.boot() also accepts preloaded
// BootResources; this NativeAOT build needs only the wasm binary (the manifest
// lists no separate assemblies/icu), so we read dotnet.native.wasm off disk and
// hand it over directly. Boot is lazy and process-wide: a server that only
// uses nl_to_jaml never pays the boot cost, and HTTP request-scoped servers
// share one runtime.

import { createRequire } from "node:module";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";

let bootPromise = null;

async function boot() {
  const require = createRequire(import.meta.url);
  const pkgEntry = require.resolve("motely-wasm");
  const wasmPath = join(dirname(pkgEntry), "bin", "dotnet.native.wasm");

  const bootsharp = (await import("motely-wasm")).default;
  const { Motely } = await import("motely-wasm");

  const buf = readFileSync(wasmPath);
  const wasm = buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength);

  await bootsharp.boot({ wasm });
  return Motely;
}

function engine() {
  // Cache the promise — including a rejection — so we boot at most once.
  if (!bootPromise) bootPromise = boot();
  return bootPromise;
}

// ValidateJaml returns "valid" on success or a human-readable engine error.
export async function validateJaml(jaml) {
  const motely = await engine();
  const result = motely.validateJaml(jaml);
  const ok = result === "valid";
  return { ok, message: result };
}

// ExplainJaml throws a generic "C# exception from NativeAOT" on invalid input,
// so validate first and surface the real parse error instead of the opaque throw.
export async function explainJaml(jaml) {
  const motely = await engine();
  const validation = motely.validateJaml(jaml);
  if (validation !== "valid") {
    return { ok: false, message: validation, explanation: null };
  }
  return { ok: true, message: "valid", explanation: motely.explainJaml(jaml) };
}

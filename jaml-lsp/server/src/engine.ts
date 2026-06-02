// Layer 2: the authoritative semantic validator — the real Motely WASM engine.
//
// `Motely.parseJaml(text)` IS the validator: it succeeds for valid filters and
// throws for invalid ones. We run it lazily and defensively — if motely-wasm
// isn't installed or fails to boot, this no-ops and the fast structural layer
// (jaml-lang's getDiagnostics) carries the editor on its own.

import { Severity, type Diagnostic } from "@motely/jaml-lang";

interface EngineApi {
  parseJaml(yaml: string): unknown;
}

let enginePromise: Promise<EngineApi | null> | undefined;

async function loadEngine(): Promise<EngineApi | null> {
  try {
    // motely-wasm is an ESM package; dynamic import keeps it out of the bundle.
    const mod = (await import("motely-wasm")) as Record<string, any>;
    const bootsharp = mod.default ?? mod.bootsharp;
    const Motely = mod.Motely;
    if (!bootsharp || !Motely?.parseJaml) return null;

    const booted = bootsharp.BootStatus?.Booted;
    if (bootsharp.getStatus?.() !== booted) {
      await bootsharp.boot();
    }
    return { parseJaml: (y: string) => Motely.parseJaml(y) };
  } catch {
    return null;
  }
}

/** Authoritative diagnostics from the engine. Empty when valid or unavailable. */
export async function engineDiagnostics(text: string): Promise<Diagnostic[]> {
  if (text.trim() === "") return [];
  if (!enginePromise) enginePromise = loadEngine();
  const engine = await enginePromise;
  if (!engine) return [];

  try {
    engine.parseJaml(text);
    return [];
  } catch (e) {
    const message = e instanceof Error ? e.message : String(e);
    return [
      {
        range: { start: { line: 0, character: 0 }, end: { line: 0, character: 120 } },
        message: `engine: ${message}`,
        severity: Severity.Error,
        source: "jaml(engine)",
      },
    ];
  }
}

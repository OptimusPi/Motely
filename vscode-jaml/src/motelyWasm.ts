/**
 * Lazy access to the published Motely WebAssembly engine.  It is deliberately
 * kept behind this small adapter so the extension host never reimplements JAML.
 */
type WasmModule = {
  default: {
    boot(): Promise<unknown>;
    getStatus(): number;
    BootStatus: { Standby: number; Booted: number };
  };
  MotelyJaml: {
    validate(text: string): string | null;
    listItems(kind: string, query?: string): string[];
  };
  MotelyWasm: {
    getVersion(): string;
  };
};

let runtime: Promise<WasmModule> | undefined;

async function getRuntime(): Promise<WasmModule> {
  runtime ??= import("motely-wasm") as Promise<WasmModule>;
  const engine = await runtime;
  if (engine.default.getStatus() === engine.default.BootStatus.Standby) {
    await engine.default.boot();
  }
  return engine;
}

export async function validateWithWasm(text: string): Promise<string | null> {
  return (await getRuntime()).MotelyJaml.validate(text);
}

export async function listWasmItems(kind: string, query?: string): Promise<string[]> {
  return (await getRuntime()).MotelyJaml.listItems(kind, query);
}

export async function wasmVersion(): Promise<string> {
  return (await getRuntime()).MotelyWasm.getVersion();
}
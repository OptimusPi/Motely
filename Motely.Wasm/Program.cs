// The runtime is booted by host/main.mjs; every real entry point is a [JSExport] on
// MotelyWasmApi. Main exists only because browser-wasm apps are OutputType=Exe.
Console.WriteLine("motely-wasm: runtime up");

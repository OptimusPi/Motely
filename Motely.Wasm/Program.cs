// The runtime is booted by the host via bootsharp.boot(); every real entry point is a
// Bootsharp [Export] on MotelyWasmApi. Main exists only because browser-wasm apps are
// OutputType=Exe.
Console.WriteLine("motely-wasm: runtime up");

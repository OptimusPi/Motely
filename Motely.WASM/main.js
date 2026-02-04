
// Motely WASM Entry Point
// Aligns with: https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-interop/?view=aspnetcore-9.0#browser-app

import { dotnet } from './_framework/dotnet.js';

let exports = null;

export async function initialize() {
    if (exports) return exports;

    const { getAssemblyExports, getConfig } = await dotnet
        .withDiagnosticTracing(false)
        .create();

    const config = getConfig();
    exports = await getAssemblyExports(config.mainAssemblyName);
    return exports;
}

export async function analyzeSeed(seed, deck, stake) {
    const api = await initialize();
    // Assuming Motely.WASM.Orchestrator or similar wrapper exposes this
    return api.Motely.WASM.Analyze(seed, deck, stake);
}

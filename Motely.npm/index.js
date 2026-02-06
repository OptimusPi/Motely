// Motely WASM Package Entry Point
// Exported methods return marshalled objects (no JSON); shapes match .NET DTOs.
export async function loadMotely(options) {
    const base = (options?.baseUrl ?? "/_framework").replace(/\/$/, "") || "/_framework";
    const origin = typeof window !== "undefined" ? window.location.origin : "https://localhost";
    const url = base.startsWith("http") ? base : new URL(base, origin).href;
    const dotnetUrl = `${url}/dotnet.js`;
    // Use a function constructor to hide the dynamic import from Turbopack/Webpack static analysis
    const { dotnet } = await new Function('url', 'return import(url)')(dotnetUrl);
    const { getAssemblyExports, getConfig } = await dotnet.create();
    const config = getConfig();
    const exports = await getAssemblyExports(config.mainAssemblyName);
    return exports.Motely.WASM.MotelyWasm;
}

// Boots the .NET runtime and hangs the exported API off globalThis.motely.
// Import shape is the standard wasmbrowser host contract: ./_framework/dotnet.js
// next to this file inside the published AppBundle.
import { dotnet } from "./_framework/dotnet.js";

const { getAssemblyExports, getConfig } = await dotnet
  .withDiagnosticTracing(false)
  .create();

const exports = await getAssemblyExports(getConfig().mainAssemblyName);

// [JSExport]s surface under their full namespace.
const api = exports.Motely.Wasm.MotelyWasmApi;

globalThis.motely = api;
globalThis.dispatchEvent(new CustomEvent("motely-ready"));

import { dotnet } from './_framework/dotnet.js';

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// Expose to global scope for easy testing
globalThis.MotelyWasm = exports.Motely.WASM.MotelyWasm;

console.log('Motely.WASM loaded. Use MotelyWasm.AnalyzeSeed("TACO1111", "Red", "White", 1, 8, "{}") to test.');

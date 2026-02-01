/**
 * motely-wasm - Balatro seed analyzer and searcher
 * 
 * This package provides the WASM build files for Motely.
 * 
 * Usage:
 *   // 1. Get path to WASM files (for build scripts / copying to public folder)
 *   const { getDistPath, getFrameworkPath } = require('motely-wasm');
 *   console.log(getDistPath());      // Full path to dist/
 *   console.log(getFrameworkPath()); // Full path to dist/_framework/
 * 
 *   // 2. In your browser code, load directly from your served public path:
 *   //    (Do NOT import loadMotely - it breaks bundlers like Webpack/Turbopack)
 *   const { dotnet } = await import('/motely-wasm/_framework/dotnet.js');
 *   const { getAssemblyExports, getConfig } = await dotnet.create();
 *   const exports = await getAssemblyExports(getConfig().mainAssemblyName);
 *   const api = exports.Motely.WASM.MotelyWasm;
 * 
 * IMPORTANT: Copy dist/ to your public folder (e.g., public/motely-wasm/).
 * The WASM files must be served with COOP/COEP headers for multi-threading.
 */
const path = require('path');

/**
 * Get the path to the dist folder containing WASM files.
 * Copy this folder to your public directory.
 */
function getDistPath() {
    return path.join(__dirname, 'dist');
}

/**
 * Get the path to the _framework folder containing the actual WASM runtime.
 */
function getFrameworkPath() {
    return path.join(__dirname, 'dist', '_framework');
}

module.exports = { getDistPath, getFrameworkPath };

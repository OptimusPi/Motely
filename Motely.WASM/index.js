/**
 * motely-wasm NPM package entry.
 * The actual WASM app lives in dist/app-bundle/ (main.js, _framework/, etc.).
 * Copy that folder to your app's public dir (e.g. public/motely-wasm) and load /motely-wasm/main.js.
 */

const path = require('path');

/**
 * Returns the absolute path to the app-bundle directory (main.js, _framework/, etc.).
 * Use this in build scripts to copy the bundle to public/ or static assets.
 * @returns {string}
 */
function getAppBundlePath() {
  return path.join(__dirname, 'dist', 'app-bundle');
}

module.exports = { getAppBundlePath };

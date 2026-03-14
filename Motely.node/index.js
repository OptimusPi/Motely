// Motely Node.js AOT Addon - minimal platform detection
import { platform } from 'node:os';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const __dirname = dirname(fileURLToPath(import.meta.url));

// Flat layout per PublishMultiPlatformNodeModule: bin/<rid>/Motely.NodeAddon.node
const pl = platform();
const rid = pl === 'darwin' ? 'osx-x64' : pl === 'win32' ? 'win-x64' : pl === 'linux' ? 'linux-x64' : null;
if (!rid) throw new Error(`Unsupported platform: ${pl}`);
const addonPath = join(__dirname, 'bin', rid, 'Motely.NodeAddon.node');

const addon = require(addonPath);
const api = addon.MotelyNodeExports;

export function loadMotely(/* options */) {
  return Promise.resolve(api);
}
export default api;

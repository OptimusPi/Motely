// Motely Node.js AOT Addon - minimal platform detection
import { platform } from 'node:os';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const __dirname = dirname(fileURLToPath(import.meta.url));

const platformPath = {
    win32: join(__dirname, 'bin', 'Release', 'net10.0', 'win-x64', 'publish', 'Motely.NodeAddon.node'),
    linux: join(__dirname, 'bin', 'Release', 'net10.0', 'linux-x64', 'publish', 'Motely.NodeAddon.node'),
    darwin: join(__dirname, 'bin', 'Release', 'net10.0', 'osx-x64', 'publish', 'Motely.NodeAddon.node')
};

const addonPath = platformPath[platform()];
if (!addonPath) {
    throw new Error(`Unsupported platform: ${platform()}`);
}

const addon = require(addonPath);
export default addon.MotelyNodeExports;

// motely-node - Native AOT loader
// Loads the platform-specific .node binary from bin/{rid}/

const path = require('node:path');
const { dlopen, platform, arch } = require('node:process');

const ridPlatform = platform === 'win32' ? 'win' : platform === 'darwin' ? 'osx' : platform;
const ridArch = arch === 'ia32' ? 'x86' : arch;
const rid = `${ridPlatform}-${ridArch}`;

const moduleFilePath = path.join(__dirname, 'bin', rid, 'Motely.NodeAddon.node');

const moduleExports = { exports: {} };
try {
  dlopen(moduleExports, moduleFilePath);
} catch (err) {
  throw new Error(
    `Failed to load motely-node native module for ${rid}.\n` +
    `Expected: ${moduleFilePath}\n` +
    `Error: ${err.message}\n\n` +
    `Make sure you have the correct platform binary installed.`
  );
}

module.exports = moduleExports.exports;

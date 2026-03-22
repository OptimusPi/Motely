#!/usr/bin/env node
/**
 * Stage Native AOT Node module into `motely-node/` for npm.
 *
 * `dotnet publish Motely/Motely.csproj` with `-p:PublishAot=true` and a RID produces:
 *   Motely.node, Motely.js, import.cjs, Motely.d.ts (from Microsoft.JavaScript.NodeApi.Generator)
 *
 * There is **no** `index.cjs` from the generator — CommonJS consumers expect a small loader that
 * `require()`s the native addon under `bin/<rid>/motely.node`.
 */
import { copyFileSync, existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from 'fs';
import { dirname, join, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..', '..');
const pkgRoot = join(repoRoot, 'motely-node');

const ridFromEnv = process.env.MOTELY_NODE_RID;
const CANDIDATE_PUBLISH_DIRS = [
    process.env.MOTELY_NODE_PUBLISH_DIR,
    ridFromEnv && join(repoRoot, 'Motely', 'bin', 'Release', 'net10.0', ridFromEnv, 'publish'),
    join(repoRoot, 'Motely', 'bin', 'Release', 'net10.0', 'linux-x64', 'publish'),
    join(repoRoot, 'Motely', 'bin', 'Release', 'net10.0', 'win-x64', 'publish'),
    join(repoRoot, 'Motely', 'bin', 'Release', 'net10.0', 'publish'),
].filter(Boolean);

function findPublishDir() {
    for (const dir of CANDIDATE_PUBLISH_DIRS) {
        const nodeFile = join(dir, 'Motely.node');
        if (existsSync(nodeFile)) return dir;
    }
    return null;
}

const publishDir = findPublishDir();
if (!publishDir) {
    console.error('Native AOT publish output not found (expected Motely.node). Tried:');
    for (const dir of CANDIDATE_PUBLISH_DIRS) console.error(`  - ${dir}`);
    console.error('Build first, e.g. docker + dotnet publish Motely/Motely.csproj -c Release -f net10.0 -r linux-x64 -p:PublishAot=true');
    console.error('On Windows after a local win-x64 AOT publish: MOTELY_NODE_RID=win-x64 node Motely/build/stage-node.mjs');
    process.exit(1);
}

function runtimeIdFromPath(dir) {
    if (dir.includes('linux-x64')) return 'linux-x64';
    if (dir.includes('win-x64')) return 'win-x64';
    if (dir.includes('osx-x64')) return 'osx-x64';
    if (dir.includes('osx-arm64')) return 'osx-arm64';
    return 'linux-x64';
}

const rid = runtimeIdFromPath(publishDir);

const binDir = join(pkgRoot, 'bin', rid);
mkdirSync(binDir, { recursive: true });

const addonSrc = join(publishDir, 'Motely.node');
copyFileSync(addonSrc, join(binDir, 'motely.node'));
console.log(`→ motely-node/bin/${rid}/motely.node`);

const copyIfPresent = (name) => {
    const src = join(publishDir, name);
    if (existsSync(src) && statSync(src).isFile()) {
        copyFileSync(src, join(pkgRoot, name));
        console.log(`→ motely-node/${name}`);
    }
};

for (const name of readdirSync(publishDir)) {
    if (name === 'Motely.node' || name === 'Motely.node.pdb') continue;
    const lower = name.toLowerCase();
    if (lower.endsWith('.dll') || lower.endsWith('.deps.json') || lower.endsWith('.pdb')) {
        copyIfPresent(name);
        continue;
    }
    if (name.endsWith('.js') || name.endsWith('.cjs') || name.endsWith('.mjs') || name.endsWith('.d.ts')) {
        copyIfPresent(name);
    }
}

const indexCjs = `'use strict';
const path = require('node:path');
const addonPath = path.join(__dirname, 'bin', '${rid}', 'motely.node');
const addon = require(addonPath);
module.exports = addon;
module.exports.default = addon;
`;
writeFileSync(join(pkgRoot, 'index.cjs'), indexCjs);
console.log('→ motely-node/index.cjs (CommonJS loader → native addon)');

const schemaCandidates = [join(repoRoot, 'jaml.schema.json'), join(repoRoot, 'Motely', 'jaml.schema.json')];
for (const p of schemaCandidates) {
    if (existsSync(p)) {
        copyFileSync(p, join(pkgRoot, 'jaml.schema.json'));
        console.log('→ motely-node/jaml.schema.json');
        break;
    }
}

/** Motely.d.ts imports these paths; publish output does not ship them — empty modules satisfy tsc. */
const TYPEDEF_STUBS = [
    'System.Runtime',
    'System.Runtime.Intrinsics',
    'System.Text.Json',
    'System.Collections',
    'YamlDotNet',
];
for (const name of TYPEDEF_STUBS) {
    const stubPath = join(pkgRoot, `${name}.d.ts`);
    if (!existsSync(stubPath)) {
        writeFileSync(stubPath, 'export {};\n');
        console.log(`→ motely-node/${name}.d.ts (stub for Motely.d.ts imports)`);
    }
}

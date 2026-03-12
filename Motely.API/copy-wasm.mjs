#!/usr/bin/env node
import { cpSync, rmSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));

const src = join(__dirname, 'node_modules', 'motely-wasm', '_framework_st');
const dest = join(__dirname, 'wwwroot', '_framework_st');

rmSync(dest, { recursive: true, force: true });
cpSync(src, dest, { recursive: true });
console.log(`[copy-wasm] _framework_st -> wwwroot/_framework_st`);

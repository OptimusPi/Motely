import { loadMotelyNode } from './motely-node-loader.mjs';
import { join } from 'node:path';
import { writeFileSync } from 'node:fs';

const log = (msg) => {
  const line = `[${new Date().toISOString()}] ${msg}`;
  writeFileSync('debug.log', line + '\n', { flag: 'a' });
  console.log(line);
};

const frameworkPath = join(process.cwd(), 'Motely.npm', 'test-extract', 'package', '_framework');

log('START');
log('Framework: ' + frameworkPath);

try {
  log('Loading...');
  const motely = await loadMotelyNode({ frameworkPath });
  log('Loaded!');

  const version = motely.getVersion();
  log('Version: ' + JSON.stringify(version));

  log('DONE');
} catch (err) {
  log('ERROR: ' + err.message);
  log('STACK: ' + err.stack);
}

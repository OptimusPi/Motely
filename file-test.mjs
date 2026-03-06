console.log('TEST START');

import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { writeFileSync } from 'node:fs';

const log = (msg) => {
  console.log(msg);
  writeFileSync('test-output.log', msg + '\n', { flag: 'a' });
};

log('Dirname: ' + __dirname);

const frameworkPath = join(__dirname, 'Motely.npm', 'test-extract', 'package', '_framework');
log('Framework: ' + frameworkPath);

try {
  const dotnetJsPath = join(frameworkPath, 'dotnet.js');
  log('Loading: ' + dotnetJsPath);
  
  const mod = await import(dotnetJsPath);
  log('Module loaded, keys: ' + Object.keys(mod).join(', '));
  log('dotnet type: ' + typeof mod.dotnet);
  
  if (mod.dotnet) {
    log('Creating runtime...');
    const runtime = await mod.dotnet.create();
    log('Runtime created!');
    
    const config = runtime.getConfig();
    log('Assembly: ' + config.mainAssemblyName);
    
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    log('Exports loaded: ' + Object.keys(exports).join(', '));
    
    const wasm = exports.Motely?.BrowserWasm?.MotelyWasmExports;
    log('WASM exports: ' + (wasm ? 'FOUND' : 'NOT FOUND'));
    
    if (wasm) {
      log('Getting version...');
      const version = await wasm.GetVersionAsync();
      log('Version: ' + version);
    }
  }
  
  log('TEST PASS');
} catch (e) {
  log('ERROR: ' + e.message);
  log('STACK: ' + e.stack);
}

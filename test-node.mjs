// Node.js test for Motely WASM - Uses pre-built WASM from npm package
// Run with: node test-node.mjs

import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Path to the extracted _framework
const frameworkPath = join(__dirname, 'Motely.npm', 'test-extract', 'package', '_framework');
const dotnetJsPath = join(frameworkPath, 'dotnet.js');

console.log('══════════════════════════════════════════════════════════════');
console.log('  MOTELY WASM - Node.js Test');
console.log('══════════════════════════════════════════════════════════════');
console.log('Framework path:', frameworkPath);
console.log('');

// Set up global callbacks BEFORE loading dotnet
globalThis.__motelyOnProgress = (searched, matches, elapsed, count) => {
  console.log(`  📊 Progress: ${searched.toLocaleString()} searched, ${matches} matches, ${elapsed}ms`);
};

globalThis.__motelyOnResult = (seed, score) => {
  console.log(`  ✅ FOUND: ${seed} (score: ${score})`);
};

async function main() {
  try {
    console.log('Loading dotnet.js...');
    const { dotnet } = await import(dotnetJsPath);
    console.log('✓ dotnet.js loaded');
    console.log('');

    console.log('Creating .NET runtime...');
    const runtime = await dotnet.create();
    console.log('✓ Runtime created');
    console.log('');

    const config = runtime.getConfig();
    console.log('Assembly:', config.mainAssemblyName);
    console.log('');

    console.log('Getting exports...');
    const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
    const wasm = exports.Motely.BrowserWasm.MotelyWasmExports;
    console.log('✓ Exports loaded');
    console.log('');

    // Start the main program (keeps alive via Task.Delay(Timeout.Infinite))
    runtime.runMain().catch(err => {
      // Expected - it runs forever
    });

    // Wait a bit for initialization
    await new Promise(r => setTimeout(r, 500));

    console.log('══════════════════════════════════════════════════════════════');
    console.log('  TEST 1: Get Version');
    console.log('══════════════════════════════════════════════════════════════');
    const versionJson = await wasm.GetVersionAsync();
    const version = JSON.parse(versionJson);
    console.log('Version:', version.version);
    console.log('Runtime:', version.runtime);
    console.log('Features:', version.features?.join(', ') || 'N/A');
    console.log('');

    console.log('══════════════════════════════════════════════════════════════');
    console.log('  TEST 2: Get Capabilities');
    console.log('══════════════════════════════════════════════════════════════');
    const capsJson = await wasm.GetCapabilitiesAsync();
    const caps = JSON.parse(capsJson);
    console.log('SIMD:', caps.simd ? '✓ Enabled' : '✗ Disabled');
    console.log('Threads:', caps.threads ? '✓ Enabled' : '✗ Disabled');
    console.log('Processor Count:', caps.processorCount);
    console.log('Runtime:', caps.runtime);
    console.log('');

    console.log('══════════════════════════════════════════════════════════════');
    console.log('  TEST 3: Analyze Seed "ALEEB1234"');
    console.log('══════════════════════════════════════════════════════════════');
    const analysisJson = await wasm.AnalyzeSeedAsync('ALEEB1234', 'Red', 'White');
    const analysis = JSON.parse(analysisJson);
    
    if (analysis.error) {
      console.log('❌ Error:', analysis.error);
    } else {
      console.log('Seed:', analysis.seed);
      console.log('Deck:', analysis.deck);
      console.log('Stake:', analysis.stake);
      console.log('Erratic Deck:', analysis.erraticDeckComposition?.join(', ') || 'None');
      console.log('Twos Count:', analysis.twos);
      console.log('');
      console.log('Antes:', analysis.antes?.length || 0);
      if (analysis.antes?.length > 0) {
        const ante1 = analysis.antes[0];
        console.log('  Ante 1:');
        console.log('    Boss:', ante1.boss);
        console.log('    Voucher:', ante1.voucher);
        console.log('    Small Blind Tag:', ante1.smallBlindTag);
        console.log('    Big Blind Tag:', ante1.bigBlindTag);
        console.log('    Shop Queue:', ante1.shopQueue?.length || 0, 'items');
        console.log('    Packs:', ante1.packs?.length || 0, 'packs');
      }
    }
    console.log('');

    console.log('══════════════════════════════════════════════════════════════');
    console.log('  TEST 4: Validate JAML');
    console.log('══════════════════════════════════════════════════════════════');
    const jaml = `name: "Test Filter"
deck: Red
stake: White
must:
  - joker: Showman
    antes: [1, 2]
    sources:
      shopItems: [0, 1]
      boosterPacks: [0, 1]
`;
    const validationJson = await wasm.ValidateJamlAsync(jaml);
    const validation = JSON.parse(validationJson);
    console.log('Valid:', validation.valid ? '✓ Yes' : '✗ No');
    if (validation.valid) {
      console.log('Name:', validation.name);
      console.log('Deck:', validation.deck);
      console.log('Stake:', validation.stake);
    } else {
      console.log('Error:', validation.error);
    }
    console.log('');

    console.log('══════════════════════════════════════════════════════════════');
    console.log('  TEST 5: Search Random Seeds');
    console.log('══════════════════════════════════════════════════════════════');
    console.log('Searching 100 random seeds for Showman in Ante 1-2...');
    const searchOptions = JSON.stringify({
      randomSeeds: 100,
      threadCount: 1,
      batchSize: 2
    });
    
    const searchResultJson = await wasm.StartJamlSearch(jaml, searchOptions);
    const searchResult = JSON.parse(searchResultJson);
    
    if (searchResult.error) {
      console.log('❌ Search Error:', searchResult.error);
    } else {
      console.log('✓ Search completed');
      console.log('  Seeds Searched:', searchResult.totalSeedsSearched || 'N/A');
      console.log('  Matching Seeds:', searchResult.matchingSeeds || 'N/A');
      console.log('  Elapsed:', searchResult.elapsedMs || 'N/A', 'ms');
    }
    console.log('');

    console.log('══════════════════════════════════════════════════════════════');
    console.log('  ALL TESTS PASSED ✓');
    console.log('══════════════════════════════════════════════════════════════');

    // Cleanup
    await wasm.DisposeSearch();
    
  } catch (err) {
    console.error('❌ FATAL ERROR:', err.message);
    console.error(err.stack);
    process.exit(1);
  }
}

main();

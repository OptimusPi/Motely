// Example usage of motely-node
import { loadMotely } from './index.js';

async function main() {
  console.log('Loading Motely WASM runtime...');
  const motely = await loadMotely();

  console.log('\n=== Capabilities ===');
  const caps = await motely.getCapabilities();
  console.log('Runtime:', caps.runtime);
  console.log('SIMD:', caps.simd);
  console.log('Threads:', caps.threads);
  console.log('Processor Count:', caps.processorCount);
  console.log('Version:', caps.version);

  console.log('\n=== Validating JAML ===');
  const jaml = `
name: "Test Filter"
deck: Red
stake: White
filters:
  - joker: Joker
    ante: 1
`;
  
  const validation = await motely.validateJaml(jaml);
  console.log('Valid:', validation.valid);
  if (validation.valid) {
    console.log('Name:', validation.name);
    console.log('Deck:', validation.deck);
    console.log('Stake:', validation.stake);
  } else {
    console.log('Error:', validation.error);
  }

  console.log('\n=== Analyzing Seed ===');
  try {
    const analysis = await motely.analyzeSeed('ABCD1234', 'Red', 'White');
    console.log('Seed:', analysis.seed);
    console.log('Deck:', analysis.deck);
    console.log('Stake:', analysis.stake);
    console.log('Antes:', analysis.antes.length);
    if (analysis.antes.length > 0) {
      console.log('Ante 1 Boss:', analysis.antes[0].boss);
      console.log('Ante 1 Voucher:', analysis.antes[0].voucher);
    }
  } catch (err) {
    console.error('Analysis error:', err.message);
  }

  console.log('\n=== Searching Seeds ===');
  let matchCount = 0;
  await motely.startJamlSearch(jaml, {
    randomSeeds: 100,
    onProgress: (searched, matches, elapsed, count) => {
      if (searched % 20 === 0) {
        console.log(`Progress: ${searched} seeds searched, ${matches} matches, ${elapsed}ms elapsed`);
      }
    },
    onResult: (seed, score) => {
      matchCount++;
      console.log(`Found: ${seed} (score: ${score})`);
    }
  });

  console.log(`\nSearch complete! Found ${matchCount} matching seeds.`);

  motely.dispose();
  console.log('\n✓ Done');
}

main().catch(err => {
  console.error('Error:', err);
  process.exit(1);
});

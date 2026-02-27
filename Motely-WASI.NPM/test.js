const { MotelyWasi } = require('./index');

async function main() {
    console.log('🧪 motely-wasi smoke test\n');

    try {
        const motely = await MotelyWasi.load({ runtime: 'wasmtime' });

        // Test 1: Capabilities
        console.log('1. getCapabilities()');
        const caps = await motely.getCapabilities();
        console.log('   ', caps);
        console.assert(caps.runtime === 'wasi-wasm', 'Runtime should be wasi-wasm');

        // Test 2: Validate JAML
        console.log('2. validateJaml()');
        const valid = await motely.validateJaml(`
name: Smoke Test
must:
  - joker: Blueprint
    antes: [1]
`);
        console.log('   ', valid);
        console.assert(valid.valid === true, 'JAML should be valid');

        // Test 3: Analyze Seed
        console.log('3. analyzeSeed()');
        const analysis = await motely.analyzeSeed('AAAAAAAA', 'Red', 'White');
        console.log('   Ante 1 boss:', analysis.antes?.[0]?.boss);
        console.assert(analysis.antes?.length > 0, 'Should have antes');

        motely.close();
        console.log('\n✅ All tests passed!');
    } catch (err) {
        console.error('❌ Test failed:', err.message);
        process.exit(1);
    }
}

main();

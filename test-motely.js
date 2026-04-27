const { MotelyWasm, Motely } = require('motely-wasm');

async function run() {
    await Bootsharp.import(); // wait, Bootsharp is needed
}

const { Bootsharp } = require('motely-wasm');
MotelyWasm.createSearchContext("1", 0, 0); // wait this is wrong

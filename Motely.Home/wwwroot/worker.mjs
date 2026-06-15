import bootsharp from '/wasm/index.mjs';
import { Program } from '/wasm/generated/modules/motely/wasm.g.mjs';

await bootsharp.boot();
postMessage({ type: 'ready' });

Program.onProgress.subscribe(p => postMessage({ type: 'progress', data: p }));
Program.onSeedMatch.subscribe(s => postMessage({ type: 'seed', data: s }));
Program.onScoredResult.subscribe(r => postMessage({ type: 'result', data: r }));

onmessage = ({ data: { type, jaml, count, startBatch, batchChars } }) => {
    let config;
    try {
        config = Program.fromJaml(jaml);
    } catch (e) {
        postMessage({ type: 'parseError', message: e.message });
        return;
    }

    try {
        if (type === 'random') {
            Program.runRandomSearch(config, count ?? 1_000_000);
        } else {
            Program.runSequentialSearch(
                config,
                BigInt(startBatch ?? 0),
                9223372036854775807n,
                batchChars ?? 4
            );
        }
        postMessage({ type: 'done' });
    } catch (e) {
        postMessage({ type: 'error', message: e.message });
    }
};

import bootsharp, * as mod from './index.mjs';

const MW = mod.Motely?.Executors?.MotelyWasm || mod.MotelyWasm;

self.onmessage = async (e) => {
    const { jaml, startBatch, endBatch } = e.data;

    try {
        await bootsharp.boot();
        self.postMessage({ type: 'booted' });

        const err = MW.validateJaml(jaml);
        if (err) {
            self.postMessage({ type: 'error', msg: err });
            return;
        }
        self.postMessage({ type: 'validated' });

        MW.onProgress.subscribe((searched, found, elapsedMs) => {
            self.postMessage({ type: 'progress', searched: Number(searched), found: Number(found), ms: Number(elapsedMs) });
        });

        MW.onResult.subscribe((seed, score) => {
            self.postMessage({ type: 'result', seed, score });
        });

        const result = MW.runSearch(jaml, 1, 3, startBatch, endBatch);
        const [status, seedsFound, highestScore] = result.split('|');
        self.postMessage({ type: 'done', status, seedsFound: +seedsFound, highestScore: +highestScore });
    } catch (ex) {
        self.postMessage({ type: 'error', msg: ex.message });
    }
};

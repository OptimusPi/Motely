import motely, { Motely } from "motely-wasm";

// Boot motely immediately when this module is loaded
motely.boot().catch(console.error);

let activeSearch: any = null;

self.addEventListener('message', function(e) {
  const msg = e.data;

  if (msg.type === 'start') {
    const validation = Motely.MotelyWasm.validateJaml(msg.jaml);
    if (validation !== 'valid') { self.postMessage({ type: 'error', message: validation }); return; }

    function cleanup() {
      Motely.MotelyWasmEvents.notifyResult = () => {};
      Motely.MotelyWasmEvents.notifyProgress = () => {};
      Motely.MotelyWasmEvents.notifyComplete = () => {};
      activeSearch = null;
    }

    Motely.MotelyWasmEvents.notifyResult = function(seed: string, score: number, tallyColumns: any) {
      self.postMessage({ type: 'result', seed, score, tallyColumns: Array.from(tallyColumns) });
    };
    Motely.MotelyWasmEvents.notifyProgress = function(searched: bigint, matching: bigint) {
      self.postMessage({ type: 'progress', searched: searched.toString(), matching: matching.toString() });
    };
    Motely.MotelyWasmEvents.notifyComplete = function(status: string, searched: bigint, matched: bigint) {
      cleanup();
      self.postMessage({ type: 'complete', status, searched: searched.toString(), matched: matched.toString() });
    };

    try {
      const mode = msg.mode || 'random';

      if (mode === 'random') {
        activeSearch = Motely.MotelyWasm.startRandomSearch(msg.jaml, msg.count);
      } else if (mode === 'aesthetic') {
        activeSearch = Motely.MotelyWasm.startAestheticSearch(msg.jaml, msg.aesthetic);
      } else if (mode === 'seedList') {
        activeSearch = Motely.MotelyWasm.startSeedListSearch(msg.jaml, msg.seeds);
      } else if (mode === 'keyword') {
        activeSearch = Motely.MotelyWasm.startKeywordSearch(msg.jaml, msg.keywords, msg.padding || '');
      } else if (mode === 'sequential') {
        activeSearch = Motely.MotelyWasm.startSequentialSearch(msg.jaml, msg.batchCharCount, BigInt(msg.startBatch), BigInt(msg.endBatch));
      }
    } catch (err) {
      cleanup();
      self.postMessage({ type: 'error', message: String(err) });
    }
  } else if (msg.type === 'stop') {
    if (activeSearch) {
      activeSearch.cancel();
      activeSearch = null;
      self.postMessage({ type: 'cancelled' });
    }
  } else if (msg.type === 'get_tally_labels') {
    try {
      const labels = Motely.MotelyWasm.getTallyLabels(msg.jaml);
      self.postMessage({ type: 'tally_labels', labels: Array.from(labels) });
    } catch (err) {
      self.postMessage({ type: 'error', message: String(err) });
    }
  }
});

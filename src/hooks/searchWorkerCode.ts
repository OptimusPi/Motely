export const SEARCH_WORKER_CODE = `
let MotelyWasm = null;
let MotelyWasmEvents = null;
let Filters = null;
let activeSearch = null;

self.addEventListener('message', async function(e) {
  const msg = e.data;

  if (msg.type === 'init') {
    try {
      const mod = await import(msg.url);
      await mod.default.boot();
      MotelyWasm = mod.MotelyWasm;
      MotelyWasmEvents = mod.MotelyWasmEvents;
      Filters = mod.Filters;
      self.postMessage({ type: 'ready' });
    } catch (err) {
      self.postMessage({ type: 'error', message: String(err) });
    }
    return;
  }

  if (msg.type === 'start') {
    if (!MotelyWasm) { self.postMessage({ type: 'error', message: 'Not initialized' }); return; }
    const validation = MotelyWasm.validateJaml(msg.jaml);
    if (validation !== 'valid') { self.postMessage({ type: 'error', message: validation }); return; }

    let rId, pId, cId;
    function cleanup() {
      MotelyWasmEvents.onResult.unsubscribeById(rId);
      MotelyWasmEvents.onProgress.unsubscribeById(pId);
      MotelyWasmEvents.onComplete.unsubscribeById(cId);
      activeSearch = null;
    }

    rId = MotelyWasmEvents.onResult.subscribe(function(seed, score, tallyColumns) {
      self.postMessage({ type: 'result', seed, score, tallyColumns: Array.from(tallyColumns) });
    });
    pId = MotelyWasmEvents.onProgress.subscribe(function(searched, matching) {
      self.postMessage({ type: 'progress', searched: searched.toString(), matching: matching.toString() });
    });
    cId = MotelyWasmEvents.onComplete.subscribe(function(status, searched, matched) {
      cleanup();
      self.postMessage({ type: 'complete', status, searched: searched.toString(), matched: matched.toString() });
    });

    try {
      const mode = msg.mode || 'random';

      if (mode === 'random') {
        activeSearch = MotelyWasm.startRandomSearch(msg.jaml, msg.count);
      } else if (mode === 'aesthetic') {
        activeSearch = MotelyWasm.startAestheticSearch(msg.jaml, msg.aesthetic);
      } else if (mode === 'seedList') {
        activeSearch = MotelyWasm.startSeedListSearch(msg.jaml, msg.seeds);
      } else if (mode === 'keyword') {
        activeSearch = MotelyWasm.startKeywordSearch(msg.jaml, msg.keywords, msg.padding || '');
      } else if (mode === 'sequential') {
        activeSearch = MotelyWasm.startSequentialSearch(msg.jaml, msg.batchCharCount, BigInt(msg.startBatch), BigInt(msg.endBatch));
      } else {
        self.postMessage({ type: 'error', message: 'Unknown search mode: ' + mode });
        cleanup();
        return;
      }
    } catch (err) {
      cleanup();
      self.postMessage({ type: 'error', message: String(err) });
    }
    return;
  }

  if (msg.type === 'stop') {
    if (activeSearch) { activeSearch.cancel(); activeSearch = null; }
    self.postMessage({ type: 'cancelled' });
  }

  if (msg.type === 'get_tally_labels') {
    if (!MotelyWasm) { self.postMessage({ type: 'error', message: 'Not initialized' }); return; }
    try {
      const labels = MotelyWasm.getTallyLabels(msg.jaml);
      self.postMessage({ type: 'tally_labels', labels });
    } catch (err) {
      self.postMessage({ type: 'error', message: String(err) });
    }
  }
});
`;

import { loadMotely } from 'motely-wasm';

const app = document.getElementById('app')!;

async function run() {
  app.innerHTML = '<p>Loading Motely WASM…</p>';
  try {
    const api = await loadMotely('/motely-wasm');
    const version = await api.GetVersion();
    const info = typeof version === 'string' ? JSON.parse(version) : version;

    app.innerHTML = `
      <h1>Motely TypeScript</h1>
      <p><strong>Loaded.</strong> ${info.version ?? '—'} (${info.runtime ?? '—'})</p>
      <p>
        <label>Seed <input id="seed" value="TACO1111" /></label>
        <button id="analyze">Analyze</button>
      </p>
      <pre id="out">—</pre>
    `;

    const seedEl = document.getElementById('seed') as HTMLInputElement;
    const outEl = document.getElementById('out')!;
    (document.getElementById('analyze')!).onclick = async () => {
      const seed = seedEl.value.trim() || 'TACO1111';
      outEl.textContent = 'Analyzing…';
      try {
        const json = await api.AnalyzeSeed(seed, 'Red', 'White', 1, 8, '{}');
        outEl.textContent = json;
      } catch (e: unknown) {
        outEl.textContent = e instanceof Error ? e.message : String(e);
      }
    };
  } catch (e: unknown) {
    app.innerHTML = `<p style="color:red">Failed: ${e instanceof Error ? e.message : String(e)}</p>`;
  }
}

run();

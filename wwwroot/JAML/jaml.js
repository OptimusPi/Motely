// Status helper
const statusEl = document.getElementById('status');
function setStatus(msg) { if (statusEl) statusEl.textContent = msg; }

// Editor helpers
function setEditorMode(mode) {
  const mono = document.getElementById('monacoEditor');
  const plain = document.getElementById('filterJaml');
  if (mode === 'monaco') { mono.style.display = 'block'; plain.style.display = 'none'; }
  else { mono.style.display = 'none'; plain.style.display = 'block'; }
}
function getJamlValue() { return window.jamlEditor ? window.jamlEditor.getValue() : (document.getElementById('filterJaml').value || ''); }
function setJamlValue(val) {
  if (window.jamlEditor) window.jamlEditor.setValue(val || '');
  else document.getElementById('filterJaml').value = val || '';
}

// Tabs
function switchTab(name, btn) {
  document.querySelectorAll('.tab-content').forEach(e => e.classList.remove('active'));
  document.getElementById(name + '-tab').classList.add('active');
  document.querySelectorAll('.tab').forEach(b => b.classList.remove('active'));
  if (btn) btn.classList.add('active');
}

// Splitter
function initSplitter() {
  const splitter = document.getElementById('panelSplitter');
  const left = document.querySelector('.left-panel');
  const container = document.querySelector('.side-by-side');
  let dragging = false;
  const stacked = () => window.innerWidth <= 768;
  splitter.addEventListener('mousedown', (e) => { dragging = true; document.body.style.cursor = stacked() ? 'row-resize' : 'col-resize'; e.preventDefault(); });
  document.addEventListener('mouseup', () => { dragging = false; document.body.style.cursor = ''; if (window.jamlEditor) window.jamlEditor.layout(); });
  document.addEventListener('mousemove', (e) => {
    if (!dragging) return;
    if (stacked()) {
      const rect = left.getBoundingClientRect();
      const newH = Math.max(150, Math.min(window.innerHeight - 150, e.clientY - rect.top));
      left.style.height = `${newH}px`; left.style.flex = 'none';
    } else {
      const rect = container.getBoundingClientRect();
      const w = rect.width; let newW = e.clientX - rect.left;
      newW = Math.max(300, Math.min(w - 300, newW));
      left.style.flex = `0 0 ${(newW / w) * 100}%`;
    }
  });
}

// Data
let savedFilters = [];
let seedSources = [];
let currentSearchId = null;
let ws = null;
let columns = ['seed','score'];
let results = [];
let searchState = 'START'; // START | RUNNING

async function loadHealth() {
  try {
    const r = await fetch('/health');
    if (!r.ok) throw new Error('health not ok');
    setStatus('Server healthy');
    return true;
  } catch { setStatus('Server not running - check connection'); return false; }
}

async function loadFilters() {
  try {
    const r = await fetch('/filters');
    if (!r.ok) throw new Error('filters not ok');
    const data = await r.json();
    savedFilters = data.filters || data || [];
    const dd = document.getElementById('filtersDropdown');
    dd.innerHTML = '';
    savedFilters.forEach((f, i) => {
      const opt = document.createElement('option'); opt.value = i.toString(); opt.textContent = f.name || f.searchId || `Filter ${i}`;
      dd.appendChild(opt);
    });
    dd.onchange = () => {
      const idx = parseInt(dd.value); const f = savedFilters[idx];
      if (f && f.filterJaml) setJamlValue(f.filterJaml);
    };
    // If first filter exists, select and load
    if (savedFilters.length > 0) { dd.value = '0'; if (savedFilters[0].filterJaml) setJamlValue(savedFilters[0].filterJaml); }
  } catch (e) {
    const dd = document.getElementById('filtersDropdown');
    dd.innerHTML = '<option>Server not running - check connection</option>';
  }
}

async function loadSeedSources() {
  try {
    const r = await fetch('/seed-sources');
    if (!r.ok) throw new Error('seed sources not ok');
    const data = await r.json();
    seedSources = data.sources || [];
    const dd = document.getElementById('seedSourceDropdown');
    dd.innerHTML = '';
    seedSources.forEach(src => {
      const opt = document.createElement('option'); opt.value = src.key; opt.textContent = src.label; dd.appendChild(opt);
    });
    dd.value = 'all';
  } catch {
    const dd = document.getElementById('seedSourceDropdown');
    dd.innerHTML = '<option>Server not running - check connection</option>';
  }
}

function ensureWs() {
  if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) return;
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  ws = new WebSocket(`${proto}://${location.host}/ws`);
  ws.onopen = () => { if (currentSearchId) ws.send(JSON.stringify({ type: 'subscribe', searchId: currentSearchId })); };
  ws.onmessage = (evt) => {
    try {
      const msg = JSON.parse(evt.data);
      if (msg.type === 'result') {
        results.push({ seed: msg.result.seed, score: msg.result.score, tallies: msg.result.tallies });
        columns = msg.columns || columns;
        renderResults();
      } else if (msg.type === 'snapshot') {
        columns = msg.columns || columns;
        results = msg.results || results;
        renderResults();
      }
    } catch {}
  };
}

function renderResults() {
  const container = document.getElementById('resultsGrid');
  if (!results || results.length === 0) {
    container.innerHTML = '<div class="no-results"><p>No results yet</p></div>'; return;
  }
  let html = '<table class="results-table"><thead><tr>';
  columns.forEach(c => { html += `<th>${c}</th>`; });
  html += '</tr></thead><tbody>';
  results.forEach(r => {
    html += `<tr onclick="analyzeSeed('${r.seed}')"><td><code>${r.seed}</code></td><td>${r.score}</td>`;
    if (r.tallies) r.tallies.forEach(t => html += `<td>${t}</td>`);
    html += '</tr>';
  });
  html += '</tbody></table>';
  container.innerHTML = html;
}

function analyzeSeed(seed) {
  switchTab('analyze', document.querySelectorAll('.tab')[1]);
  document.getElementById('analyzeContainer').innerHTML =
    `<iframe src="https://miaklwalker.github.io/Blueprint/?seed=${seed}" style="width: 100%; height: 600px; border: none;"></iframe>`;
}

async function toggleSearch() {
  if (searchState === 'RUNNING') { await stopAll(); return; }
  await startSearch();
}

async function startSearch() {
  try {
    const jaml = getJamlValue().trim();
    if (!jaml) { setStatus('Enter a filter'); return; }
    const seedSource = document.getElementById('seedSourceDropdown')?.value || 'all';
    const r = await fetch('/search', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterJaml: jaml, seedCount: 0, seedSource })
    });
    const data = await r.json();
    if (!r.ok) { setStatus(`Search error: ${data.error || 'unknown'}`); return; }
    currentSearchId = data.searchId;
    results = data.results || [];
    columns = data.columns || columns;
    renderResults();
    ensureWs();
    if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify({ type: 'subscribe', searchId: currentSearchId }));
    searchState = 'RUNNING';
    document.getElementById('searchBtn').textContent = 'Stop Search';
    setStatus(`Running: ${currentSearchId}`);
  } catch (e) {
    setStatus(`Search failed: ${e.message}`);
  }
}

async function stopAll() {
  try {
    const r = await fetch('/search/stop-all', { method: 'POST' });
    if (r.ok) setStatus('Stopped all searches');
  } catch {}
  searchState = 'START';
  document.getElementById('searchBtn').textContent = 'Start Search';
}

function exportCsv() {
  if (!results || results.length === 0) return;
  const headers = columns;
  const csv = [headers.join(','), ...results.map(r => {
    const row = [r.seed, r.score];
    if (r.tallies) r.tallies.forEach(t => row.push(t));
    return row.join(',');
  })].join('\n');
  const blob = new Blob([csv], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a'); a.href = url; a.download = `results_${Date.now()}.csv`; a.click();
  URL.revokeObjectURL(url);
}

function formatJaml() {
  try {
    const jaml = getJamlValue();
    const obj = jsyaml.load(jaml);
    if (!obj) return;
    const formatted = jsyaml.dump(obj, { indent: 2, lineWidth: -1, noRefs: true, sortKeys: false });
    setJamlValue(formatted);
  } catch {}
}

function saveFilter() {
  // Minimal save – writes to filters directory via existing endpoint
  setStatus('Use legacy page to save filters for now.');
}

function shareLink() {
  if (!currentSearchId) { setStatus('No search ID to share'); return; }
  const url = new URL(window.location.origin + '/JAML');
  url.searchParams.set('search', currentSearchId);
  navigator.clipboard.writeText(url.toString());
  setStatus('Link copied');
}

// Settings
function openSettings() {
  document.getElementById('settingsModal').style.display = 'flex';
  
  // Sync dropdowns
  const fDd = document.getElementById('filtersDropdown');
  const sDd = document.getElementById('settingsFiltersDropdown');
  sDd.innerHTML = fDd.innerHTML;
  sDd.value = fDd.value;
  
  const srcDd = document.getElementById('seedSourceDropdown');
  const sSrcDd = document.getElementById('settingsSeedSourceDropdown');
  sSrcDd.innerHTML = srcDd.innerHTML;
  sSrcDd.value = srcDd.value;
}

function closeSettings() {
  document.getElementById('settingsModal').style.display = 'none';
  
  // Sync back to main UI
  const fDd = document.getElementById('filtersDropdown');
  const sDd = document.getElementById('settingsFiltersDropdown');
  if (fDd.value !== sDd.value) {
    fDd.value = sDd.value;
    fDd.onchange(); // Trigger load
  }
  
  const srcDd = document.getElementById('seedSourceDropdown');
  const sSrcDd = document.getElementById('settingsSeedSourceDropdown');
  if (srcDd.value !== sSrcDd.value) {
    srcDd.value = sSrcDd.value;
  }
}

function handleBackdrop(e) { if (e.target.id === 'settingsModal') closeSettings(); }

function settingsSelectFilter() {
  // Just update local state, sync on close or apply? 
  // For now, let's load it immediately to show preview in main UI behind modal?
  const sDd = document.getElementById('settingsFiltersDropdown');
  const fDd = document.getElementById('filtersDropdown');
  fDd.value = sDd.value;
  fDd.onchange();
}

function settingsSeedSourceChanged() {
  const sSrcDd = document.getElementById('settingsSeedSourceDropdown');
  const srcDd = document.getElementById('seedSourceDropdown');
  srcDd.value = sSrcDd.value;
}

async function renameFilter() {
  const dd = document.getElementById('settingsFiltersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];
  if (!filter || !filter.filePath) return;

  const newName = prompt('New name:', filter.name);
  if (!newName || newName === filter.name) return;

  try {
    const r = await fetch('/filters/rename', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterId: filter.filePath, newName })
    });
    if (!r.ok) throw new Error('Rename failed');
    setStatus(`Renamed to ${newName}`);
    await loadFilters();
    openSettings(); // Refresh dropdowns
  } catch (e) { setStatus(e.message); }
}

async function cloneFilter() {
  const dd = document.getElementById('settingsFiltersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];
  if (!filter || !filter.filePath) return;

  const newName = prompt('New name for clone:', filter.name + ' Copy');
  if (!newName) return;

  try {
    const r = await fetch('/filters/clone', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterId: filter.filePath, newName })
    });
    if (!r.ok) throw new Error('Clone failed');
    setStatus(`Cloned to ${newName}`);
    await loadFilters();
    openSettings(); // Refresh dropdowns
  } catch (e) { setStatus(e.message); }
}

async function deleteFilter() {
  const dd = document.getElementById('settingsFiltersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];
  if (!filter || !filter.filePath) return;

  if (!confirm(`Delete "${filter.name}"?`)) return;

  try {
    const r = await fetch(`/filters/${encodeURIComponent(filter.filePath)}`, { method: 'DELETE' });
    if (!r.ok) throw new Error('Delete failed');
    setStatus(`Deleted ${filter.name}`);
    await loadFilters();
    openSettings(); // Refresh dropdowns
  } catch (e) { setStatus(e.message); }
}

window.onMonacoReady = async function () {
  initSplitter();
  await loadHealth();
  await loadFilters();
  await loadSeedSources();
};

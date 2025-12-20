// Status helper
const statusEl = document.getElementById('status');
function setStatus(msg) { if (statusEl) statusEl.textContent = msg; }

// Editor helpers
function setEditorMode(mode) {
  const mono = document.getElementById('monacoEditor');
  const plain = document.getElementById('filterJaml');
  const monoBtn = document.getElementById('monacoBtn');
  const plainBtn = document.getElementById('plainBtn');

  if (mode === 'monaco') { 
    mono.style.display = 'block'; 
    plain.style.display = 'none'; 
    monoBtn.classList.add('active');
    plainBtn.classList.remove('active');
  } else { 
    mono.style.display = 'none'; 
    plain.style.display = 'block'; 
    monoBtn.classList.remove('active');
    plainBtn.classList.add('active');
  }
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

  const isStacked = () => getComputedStyle(container).flexDirection === 'column';

  const startDrag = (e) => {
    dragging = true;
    document.body.style.cursor = isStacked() ? 'row-resize' : 'col-resize';
    if (e.type === 'touchstart') e.preventDefault(); // Prevent scroll start
  };

  const endDrag = () => {
    dragging = false;
    document.body.style.cursor = '';
    if (window.jamlEditor) window.jamlEditor.layout();
  };

  const onDrag = (e) => {
    if (!dragging) return;
    
    let clientX, clientY;
    if (e.type.startsWith('touch')) {
      clientX = e.touches[0].clientX;
      clientY = e.touches[0].clientY;
      e.preventDefault(); // Prevent scrolling
    } else {
      clientX = e.clientX;
      clientY = e.clientY;
    }

    if (isStacked()) {
      const rect = left.getBoundingClientRect();
      // Reset flex/width from landscape mode if present
      left.style.flex = 'none';
      
      const newH = Math.max(150, Math.min(window.innerHeight - 150, clientY - rect.top));
      left.style.height = `${newH}px`; 
    } else {
      const rect = container.getBoundingClientRect();
      // Reset height from portrait mode if present
      left.style.height = ''; 
      
      const w = rect.width; 
      let newW = clientX - rect.left;
      newW = Math.max(300, Math.min(w - 300, newW));
      left.style.flex = `0 0 ${(newW / w) * 100}%`;
    }
  };

  splitter.addEventListener('mousedown', startDrag);
  splitter.addEventListener('touchstart', startDrag, { passive: false });

  document.addEventListener('mouseup', endDrag);
  document.addEventListener('touchend', endDrag);
  document.addEventListener('touchcancel', endDrag);

  document.addEventListener('mousemove', onDrag);
  document.addEventListener('touchmove', onDrag, { passive: false });
}

// Data
let savedFilters = [];
let seedSources = [];
let currentSearchId = null;
let ws = null;
let columns = ['seed','score'];
let results = [];
let sortCol = 'score';
let sortAsc = false;
let searchState = 'START'; // START | RUNNING
let colorModeActive = false;

async function loadHealth() {
  try {
    const r = await fetch('/health');
    if (!r.ok) throw new Error('health not ok');
    setStatus('Ready');
    return true;
  } catch { setStatus('Offline'); return false; }
}

async function loadFilters(autoSelect = true) {
  const dd = document.getElementById('filtersDropdown');
  dd.innerHTML = '<option>Loading...</option>';
  try {
    const r = await fetch('/filters');
    if (!r.ok) throw new Error('filters not ok');
    const data = await r.json();
    savedFilters = data.filters || data || [];
    
    if (savedFilters.length === 0) {
      dd.innerHTML = '<option>No filters found</option>';
      return;
    }

    dd.innerHTML = '';
    savedFilters.forEach((f, i) => {
      const opt = document.createElement('option'); opt.value = i.toString(); opt.textContent = f.name || f.searchId || `Filter ${i}`;
      dd.appendChild(opt);
    });
    dd.onchange = async () => {
      const idx = parseInt(dd.value);
      await selectFilterAndRun(idx);
    };
    // If first filter exists, select and load
    if (autoSelect && savedFilters.length > 0) { 
      selectFilterAndRun(0); 
    }
  } catch (e) {
    dd.innerHTML = '<option>Offline / Error</option>';
    setStatus('Failed to load filters: ' + e.message);
  }
}

async function selectFilterAndRun(idx) {
  const f = savedFilters[idx];
  if (!f) return;

  const dd = document.getElementById('filtersDropdown');
  if (dd.value !== idx.toString()) dd.value = idx.toString();

  if (f.filterJaml) setJamlValue(f.filterJaml);

  // Clear current UI results and show loading
  results = [];
  renderResults();
  
  // 1. Instant Fetch of existing results via GET
  const searchId = f.searchId;
  if (searchId) {
    try {
      setStatus('Fetching existing seeds...');
      const r = await fetch(`/search?id=${encodeURIComponent(searchId)}`);
      if (r.ok) {
        const data = await r.json();
        if (data.results && data.results.length > 0) {
          results = data.results;
          columns = data.columns || columns;
          renderResults();
          setStatus(`Found ${results.length} existing seeds`);
        }
      }
    } catch (e) {
      console.warn('Failed to fetch existing results:', e);
    }
  }

  // 2. Auto-Start/Restart Search via POST (handles streaming)
  if (searchState === 'RUNNING') await stopAll();
  
  await startSearch();
}

async function loadSeedSources() {
  const dd = document.getElementById('seedSourceDropdown');
  dd.innerHTML = '<option>Loading...</option>';
  try {
    const r = await fetch('/seed-sources');
    if (!r.ok) throw new Error('seed sources not ok');
    const data = await r.json();
    seedSources = data.sources || [];
    
    if (seedSources.length === 0) {
        dd.innerHTML = '<option value="all">All Seeds (Default)</option>';
        return;
    }

    dd.innerHTML = '';
    seedSources.forEach(src => {
      const opt = document.createElement('option'); opt.value = src.key; opt.textContent = src.label; dd.appendChild(opt);
    });
    dd.value = 'all';
  } catch (e) {
    dd.innerHTML = '<option value="all">Offline (Default)</option>';
    setStatus('Failed to load sources: ' + e.message);
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
  
  // Sort results
  const sorted = [...results].sort((a, b) => {
    let valA = a[sortCol];
    let valB = b[sortCol];
    
    // Handle numeric strings
    if (typeof valA === 'string' && !isNaN(valA)) valA = parseFloat(valA);
    if (typeof valB === 'string' && !isNaN(valB)) valB = parseFloat(valB);
    
    if (valA < valB) return sortAsc ? -1 : 1;
    if (valA > valB) return sortAsc ? 1 : -1;
    return 0;
  });

  let html = '<table class="results-table"><thead><tr>';
  columns.forEach(c => { 
    const arrow = sortCol === c ? (sortAsc ? ' ▲' : ' ▼') : '';
    // Escape quotes in column name for onclick attribute
    const safeCol = c.replace(/'/g, "\\'");
    html += `<th onclick="toggleSort('${safeCol}')">${c}${arrow}</th>`; 
  });
  html += '</tr></thead><tbody>';
  sorted.forEach(r => {
    html += `<tr onclick="analyzeSeed('${r.seed}')">`;
    columns.forEach((col, idx) => {
      let val = r[col];
      // Special case for seeds column
      if (col === 'seed') {
        html += `<td><code>${val}</code></td>`;
        return;
      }

      // Handle tallies if it's not seed or score
      if (col !== 'seed' && col !== 'score' && r.tallies) {
        // Find index in tallies. Usually columns are [seed, score, tally1, tally2...]
        // So tally1 is at index 2 in columns.
        const tallyIdx = idx - 2;
        val = (r.tallies && r.tallies[tallyIdx] !== undefined) ? r.tallies[tallyIdx] : 0;
      } else {
        val = r[col];
      }

      let cls = '';
      if (colorModeActive) {
        cls = getColorClass(val);
      }
      html += `<td class="${cls}">${val}</td>`;
    });
    html += '</tr>';
  });
  html += '</tbody></table>';
  container.innerHTML = html;
}

function toggleColorMode() {
  colorModeActive = !colorModeActive;
  renderResults();
  setStatus(colorModeActive ? 'Color mode enabled' : 'Color mode disabled');
}

function getColorClass(val) {
  let n = parseFloat(val);
  if (isNaN(n)) return '';
  
  if (n <= 0) return 'color-mode-0';
  if (n === 1) return 'color-mode-1';
  if (n === 2) return 'color-mode-2';
  if (n === 3) return 'color-mode-3';
  if (n === 4) return 'color-mode-4';
  if (n === 5) return 'color-mode-5';
  if (n === 6) return 'color-mode-6';
  if (n === 7) return 'color-mode-7';
  if (n === 8) return 'color-mode-8';
  if (n > 8) return 'color-mode-9';
  return '';
}

function toggleSort(col) {
  console.log('Sorting by', col);
  if (sortCol === col) {
    sortAsc = !sortAsc;
  } else {
    sortCol = col;
    sortAsc = false; // Default desc for new col
  }
  renderResults();
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
    
    // Get overrides
    const seedSource = document.getElementById('seedSourceDropdown')?.value || 'all';
    
    const r = await fetch('/search', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        filterJaml: jaml, 
        seedCount: 0, 
        seedSource
      })
    });
    const data = await r.json();
    if (!r.ok) { setStatus(`Error: ${data.error || 'unknown'}`); return; }
    currentSearchId = data.searchId;
    results = data.results || [];
    columns = data.columns || columns;
    renderResults();
    ensureWs();
    if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify({ type: 'subscribe', searchId: currentSearchId }));
    searchState = 'RUNNING';
    document.getElementById('searchBtn').textContent = 'Stop Search';
    document.getElementById('searchBtn').classList.add('button-danger');
    setStatus(`Running...`);
  } catch (e) {
    setStatus(`Failed: ${e.message}`);
  }
}

async function stopAll() {
  try {
    const r = await fetch('/search/stop-all', { method: 'POST' });
    if (r.ok) setStatus('Stopped');
  } catch {}
  searchState = 'START';
  const btn = document.getElementById('searchBtn');
  btn.textContent = 'Start Search';
  btn.classList.remove('button-danger');
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
    
    // Dump with default block style
    let formatted = jsyaml.dump(obj, { indent: 2, lineWidth: -1, noRefs: true, sortKeys: false });

    // Post-process to collapse specific numeric/scalar arrays
    const targetKeys = ['antes', 'shop_slots', 'rolls', 'stakes', 'decks', 'versions'];
    targetKeys.forEach(k => {
      const regex = new RegExp(`(\\n\\s*${k}:)\\s*\\n((?:\\s+-\\s+[\\w\\d.\\"\\']+\\s*(?:\\n|$))+)`, 'g');
      formatted = formatted.replace(regex, (m, keyPart, valPart) => {
        const lines = valPart.split('\n').filter(l => l.trim().length > 0);
        const items = lines.map(l => l.trim().replace(/^-\s+/, ''));
        return `${keyPart} [${items.join(', ')}]\n`;
      });
    });

    setJamlValue(formatted);
  } catch (e) {
    console.error(e);
    setStatus('Format error: ' + e.message);
  }
}

async function saveFilter() {
  const jaml = getJamlValue();
  if (!jaml) { setStatus('Nothing to save'); return; }

  const dd = document.getElementById('filtersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];

  let filename = filter?.filePath;

  // If it's an unsaved/temp filter, or no filter selected, ask for a name
  if (!filter || !filename || filename.startsWith('_UNSAVED_') || filename.includes('{unsaved}')) {
    showInputModal('Save Filter As', filter?.name || 'NewFilter', async (name) => {
      if (!name) return;
      
      const newFilename = name.endsWith('.jaml') ? name : `${name}.jaml`;
      await performSave(newFilename, jaml);
    });
    return;
  }

  // Regular save
  await performSave(filename, jaml);
}

async function performSave(filename, jaml) {
  try {
    const r = await fetch('/filters/update', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterId: filename, filterJaml: jaml })
    });

    if (!r.ok) {
      const err = await r.json();
      throw new Error(err.error || 'Save failed');
    }

    const data = await r.json();
    setStatus(`Saved ${data.filePath}`);
    
    // Reload filters to pick up the changes/new file (don't auto-select first one)
    await loadFilters(false);
    
    // Try to select and run the one we just saved
    const newIdx = savedFilters.findIndex(f => f.filePath === data.filePath);
    if (newIdx >= 0) {
      await selectFilterAndRun(newIdx);
    }
  } catch (e) {
    setStatus(`Error: ${e.message}`);
  }
}

function shareLink() {
  if (!currentSearchId) { setStatus('No search ID'); return; }
  const url = new URL(window.location.href);
  url.search = '';
  url.searchParams.set('search', currentSearchId);
  navigator.clipboard.writeText(url.toString());
  setStatus('Link copied');
}

// Input Modal
let inputModalCallback = null;

function showInputModal(title, initialValue, callback, message = null) {
  document.getElementById('inputModalTitle').textContent = title;
  const input = document.getElementById('inputModalValue');
  input.value = initialValue || '';
  
  // Clear errors
  clearInputError();

  const msgEl = document.getElementById('inputModalMessage');
  if (message) {
    msgEl.textContent = message;
    msgEl.style.display = 'block';
  } else {
    msgEl.style.display = 'none';
  }

  inputModalCallback = callback;
  
  const modal = document.getElementById('inputModal');
  modal.style.display = 'flex';
  
  input.focus();
  input.select();
  
  // Bind confirm button
  const confirmBtn = document.getElementById('inputModalConfirm');
  confirmBtn.onclick = () => {
    const val = input.value.trim();
    if (!val) {
      showInputError('Value cannot be empty');
      return;
    }
    // Simple sanitization check
    if (val.match(/[<>:"\/\\|?*]/)) {
      showInputError('Invalid characters in name');
      return;
    }

    if (inputModalCallback) {
      inputModalCallback(val);
      closeInputModal();
    }
  };
}

function showInputError(msg) {
  const errEl = document.getElementById('inputErrorMsg');
  const input = document.getElementById('inputModalValue');
  errEl.textContent = msg;
  errEl.style.display = 'block';
  input.setAttribute('aria-invalid', 'true');
  input.style.borderColor = 'var(--balatro-red)';
}

function clearInputError() {
  const errEl = document.getElementById('inputErrorMsg');
  const input = document.getElementById('inputModalValue');
  errEl.style.display = 'none';
  input.setAttribute('aria-invalid', 'false');
  input.style.borderColor = '';
}

function closeInputModal() {
  document.getElementById('inputModal').style.display = 'none';
  inputModalCallback = null;
}

function handleInputKey(e) {
  if (e.key === 'Enter') {
    document.getElementById('inputModalConfirm').click();
  } else if (e.key === 'Escape') {
    closeInputModal();
  }
}

// Settings
function openSettings() {
  document.getElementById('settingsModal').style.display = 'flex';
}

function closeSettings() {
  document.getElementById('settingsModal').style.display = 'none';
}

function handleBackdrop(e) { 
  if (e.target.id === 'settingsModal') closeSettings();
  if (e.target.id === 'inputModal') closeInputModal();
}

async function renameFilter() {
  const dd = document.getElementById('filtersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];
  if (!filter || !filter.filePath) return;

  showInputModal('Rename Filter', filter.name, async (newName) => {
    if (!newName || newName === filter.name) return;

    try {
      const r = await fetch('/filters/rename', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filterId: filter.filePath, newName })
      });
      if (!r.ok) throw new Error('Rename failed');
      setStatus(`Renamed to ${newName}`);
      await loadFilters(false);
      // Try to select the renamed one
      const newIdx = savedFilters.findIndex(f => f.name === newName);
      if (newIdx >= 0) await selectFilterAndRun(newIdx);
      closeSettings();
    } catch (e) { setStatus(e.message); }
  });
}

async function cloneFilter() {
  const dd = document.getElementById('filtersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];
  if (!filter || !filter.filePath) return;

  showInputModal('Clone Filter', filter.name + ' Copy', async (newName) => {
    if (!newName) return;

    try {
      const r = await fetch('/filters/clone', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ filterId: filter.filePath, newName })
      });
      if (!r.ok) throw new Error('Clone failed');
      setStatus(`Cloned to ${newName}`);
      await loadFilters(false);
      // Try to select the cloned one
      const newIdx = savedFilters.findIndex(f => f.name === newName);
      if (newIdx >= 0) await selectFilterAndRun(newIdx);
      closeSettings();
    } catch (e) { setStatus(e.message); }
  });
}

async function deleteFilter() {
  const dd = document.getElementById('filtersDropdown');
  const idx = parseInt(dd.value);
  const filter = savedFilters[idx];
  if (!filter || !filter.filePath) return;

  if (!confirm(`Delete "${filter.name}"?`)) return;

  try {
    const r = await fetch(`/filters/${encodeURIComponent(filter.filePath)}`, { method: 'DELETE' });
    if (!r.ok) throw new Error('Delete failed');
    setStatus(`Deleted ${filter.name}`);
    await loadFilters();
    closeSettings();
  } catch (e) { setStatus(e.message); }
}

window.onMonacoReady = async function () {
  initSplitter();
  await loadHealth();
  await loadSeedSources();

  // Check for shared search link
  const urlParams = new URLSearchParams(window.location.search);
  const sharedSearchId = urlParams.get('search');

  if (sharedSearchId) {
    try {
      setStatus('Loading shared search...');
      const r = await fetch(`/search?id=${encodeURIComponent(sharedSearchId)}`);
      if (r.ok) {
        const data = await r.json();
        if (data.filterJaml) setJamlValue(data.filterJaml);
        
        currentSearchId = sharedSearchId;
        results = data.results || [];
        columns = data.columns || columns;
        renderResults();
        
        if (data.status === 'running') {
          searchState = 'RUNNING';
          document.getElementById('searchBtn').textContent = 'Stop Search';
          document.getElementById('searchBtn').classList.add('button-danger');
          ensureWs();
        } else {
          searchState = 'START';
          document.getElementById('searchBtn').textContent = 'Start Search';
          document.getElementById('searchBtn').classList.remove('button-danger');
        }
        
        // Also load filters in background so dropdown is populated
        await loadFilters(false); // Pass false to NOT auto-select first filter
        return;
      }
    } catch (e) {
      console.error('Failed to load shared search', e);
    }
  }

  // Normal flow
  await loadFilters(true);
};
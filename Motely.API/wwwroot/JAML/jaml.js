// Global state
let colorModeActive = false;

// Status helper
const statusEl = document.getElementById('status');
function setStatus(msg) { if (statusEl) statusEl.textContent = msg; }

// Editor helpers
let monacoMode = false; // Track current editor mode

function toggleMonaco() {
  const mono = document.getElementById('monacoEditor');
  const plain = document.getElementById('filterJaml');
  const toggleBtn = document.getElementById('monacoToggle');
  
  if (!mono || !plain) {
    console.warn('Editor elements not found');
    return;
  }
  
  monacoMode = !monacoMode;
  
  if (monacoMode) {
    // Switch to Monaco
    // Ensure Monaco editor is initialized
    if (!window.jamlEditor) {
      setStatus('Initializing Monaco editor...');
      // Monaco should already be initialized, but if not, we'll show plain editor
      if (typeof monaco === 'undefined' || typeof require === 'undefined') {
        setStatus('Monaco editor not available - using plain editor');
        monacoMode = false;
        return;
      }
    }
    
    // Sync content from plain editor to Monaco
    if (window.jamlEditor) {
      const plainValue = plain.value || '';
      window.jamlEditor.setValue(plainValue);
    }
    
    mono.style.display = 'block';
    plain.style.display = 'none';
    if (toggleBtn) toggleBtn.classList.add('active');
    if (window.jamlEditor) {
      setTimeout(() => window.jamlEditor.layout(), 100);
    }
    setStatus('Switched to Monaco editor');
  } else {
    // Switch to plain editor
    // Sync content from Monaco to plain editor
    if (window.jamlEditor) {
      const monacoValue = window.jamlEditor.getValue() || '';
      plain.value = monacoValue;
    }
    
    mono.style.display = 'none';
    plain.style.display = 'block';
    if (toggleBtn) toggleBtn.classList.remove('active');
    plain.focus();
    setStatus('Switched to plain editor');
  }
}

function setEditorMode(mode) {
  const mono = document.getElementById('monacoEditor');
  const plain = document.getElementById('filterJaml');
  const monoBtn = document.getElementById('monacoBtn');
  const plainBtn = document.getElementById('plainBtn');

  if (mode === 'monaco') { 
    mono.style.display = 'block'; 
    plain.style.display = 'none'; 
    if (monoBtn) monoBtn.classList.add('active');
    if (plainBtn) plainBtn.classList.remove('active');
    monacoMode = true;
  } else { 
    mono.style.display = 'none'; 
    plain.style.display = 'block'; 
    if (monoBtn) monoBtn.classList.remove('active');
    if (plainBtn) plainBtn.classList.add('active');
    monacoMode = false;
  }
}

function getJamlValue() { 
  if (monacoMode && window.jamlEditor) {
    return window.jamlEditor.getValue() || '';
  }
  const plain = document.getElementById('filterJaml');
  return plain ? (plain.value || '') : '';
}

function setJamlValue(val) {
  const value = val || '';
  if (monacoMode && window.jamlEditor) {
    window.jamlEditor.setValue(value);
  }
  const plain = document.getElementById('filterJaml');
  if (plain) {
    plain.value = value;
  }
  // Update columns from filter config when JAML changes (unless formatting)
  if (!isFormatting) {
    updateColumnsFromFilter();
  }
}

// Format JAML without invalidating filter
function formatJaml() {
  isFormatting = true;
  const jaml = getJamlValue().trim();
  
  if (!jaml) {
    isFormatting = false;
    return;
  }
  
  try {
    // Simple formatting: normalize indentation, clean up whitespace
    // For better formatting, could use a YAML library
    let formatted = jaml;
    
    // If Monaco is active, use Monaco's format document
    if (monacoMode && window.jamlEditor) {
      window.jamlEditor.getAction('editor.action.formatDocument').run();
      isFormatting = false;
      return;
    }
    
    // Basic formatting for plain editor
    // Split into lines, normalize indentation
    const lines = formatted.split('\n');
    formatted = lines.map(line => {
      // Preserve empty lines
      if (line.trim() === '') return '';
      // Normalize leading spaces (convert to 2 spaces per level)
      const match = line.match(/^(\s*)(.*)$/);
      if (match) {
        const indent = match[1];
        const content = match[2];
        // Count spaces and convert to consistent 2-space indentation
        const level = Math.floor(indent.length / 2);
        return '  '.repeat(level) + content;
      }
      return line;
    }).join('\n');
    
    setJamlValue(formatted);
    isFormatting = false;
    setStatus('JAML formatted');
  } catch (e) {
    isFormatting = false;
    setStatus(`Format error: ${e.message}`);
  }
}

// Hash filter structure (ignoring labels, comments, whitespace)
function hashFilterStructure(jaml) {
  if (!jaml) return '';
  
  try {
    // Remove comments
    let cleaned = jaml.replace(/#.*$/gm, '');
    
    // Remove label fields (they don't affect structure)
    cleaned = cleaned.replace(/label:\s*[^\n]+/gi, '');
    
    // Normalize whitespace
    cleaned = cleaned.replace(/\s+/g, ' ').trim();
    
    // Extract structural elements: should clauses with type, value, antes (not labels)
    // Simple hash - just use cleaned string length + first/last chars for now
    // In production, could use proper hash function
    return cleaned.length.toString() + cleaned.substring(0, 50) + cleaned.substring(Math.max(0, cleaned.length - 50));
  } catch (e) {
    console.warn('Failed to hash filter structure:', e);
    return jaml; // Fallback to full JAML
  }
}

// Invalidate filter and export to Fertilizer if needed
async function invalidateFilter() {
  if (!currentSearchId || results.length === 0) {
    isFilterInvalidated = true;
    renderResults();
    return;
  }
  
  try {
    setStatus('Exporting results to Fertilizer...');
    
    // Call fertilizer export endpoint (synchronous/blocking)
    const r = await fetch(`/search/${encodeURIComponent(currentSearchId)}/export-to-fertilizer`, {
      method: 'POST'
    });
    
    if (r.ok) {
      const data = await r.json();
      setStatus(`Exported ${data.exported || 0} seeds to Fertilizer`);
      isFilterInvalidated = true;
      renderResults();
    } else {
      const error = await r.json();
      setStatus(`Fertilizer export failed: ${error.error || 'Unknown error'}`);
      // Don't invalidate if export failed
    }
  } catch (e) {
    setStatus(`Fertilizer export error: ${e.message}`);
    // Don't invalidate if export failed
  }
}

// Parse JAML and update columns based on filter config
async function updateColumnsFromFilter() {
  // Skip invalidation check if formatting
  if (isFormatting) {
    isFormatting = false;
    return;
  }
  
  const jaml = getJamlValue().trim();
  if (!jaml) {
    // Default columns if no filter
    columns = ['seed', 'score'];
    lastValidFilterHash = null;
    isFilterInvalidated = false;
    renderResults();
    return;
  }
  
  // Calculate current filter structure hash
  const currentHash = hashFilterStructure(jaml);
  
  // Check if structure changed (not just labels)
  if (lastValidFilterHash !== null && currentHash !== lastValidFilterHash) {
    // Structure changed - invalidate
    await invalidateFilter();
  } else if (lastValidFilterHash === null) {
    // First time loading - set as valid
    lastValidFilterHash = currentHash;
    isFilterInvalidated = false;
  }
  
  try {
    // Call API to get column names from filter config
    const r = await fetch('/filters/columns', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ filterJaml: jaml })
    });
    
    if (r.ok) {
      const data = await r.json();
      if (data.columns && Array.isArray(data.columns)) {
        const newColumns = data.columns;
        
        // Check if column structure changed
        const columnsChanged = JSON.stringify(newColumns) !== JSON.stringify(lastColumnStructure);
        
        columns = newColumns;
        lastColumnStructure = [...newColumns];
        
        // If structure didn't change but we're updating columns, it's just label changes
        if (!columnsChanged && currentHash === lastValidFilterHash) {
          // Just label updates - don't invalidate
          isFilterInvalidated = false;
        }
        
        renderResults(); // Re-render to show new headers
      }
    }
  } catch (e) {
    console.warn('Failed to get columns from filter:', e);
    // Keep current columns on error
  }
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
  const splitter = document.getElementById('panelSplitter1') || document.getElementById('panelSplitter');
  const left = document.querySelector('.left-panel');
  const container = document.querySelector('.side-by-side');
  if (!splitter || !left || !container) {
    console.warn('initSplitter: Missing elements', { splitter: !!splitter, left: !!left, container: !!container });
    return;
  }
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
      // Allow sliding all the way - no constraints
      newW = Math.max(0, Math.min(w, newW));
      left.style.flex = `0 0 ${(newW / w) * 100}%`;
    }
  };

  if (!splitter) {
    console.error('initSplitter: splitter is null, cannot add event listeners');
    return;
  }

  splitter.addEventListener('mousedown', startDrag);
  splitter.addEventListener('touchstart', startDrag, { passive: false });

  document.addEventListener('mouseup', endDrag);
  document.addEventListener('touchend', endDrag);
  document.addEventListener('touchcancel', endDrag);

  document.addEventListener('mousemove', onDrag);
  document.addEventListener('touchmove', onDrag, { passive: false });
}

// Initialize top grabber to slide tray up/down
function initTopGrabber() {
  const topGrabber = document.getElementById('topGrabber');
  const topTray = document.getElementById('topTray');
  if (!topGrabber || !topTray) {
    console.warn('initTopGrabber: Missing elements', { topGrabber: !!topGrabber, topTray: !!topTray });
    return;
  }
  console.log('initTopGrabber: Initialized');
  
  // Set initial height if not set
  const initialHeight = topTray.getBoundingClientRect().height || 80;
  topTray.style.flex = `0 0 ${initialHeight}px`;
  topTray.style.height = `${initialHeight}px`;
  
  let isDragging = false;
  let startY = 0;
  let startHeight = 0;
  
  const startDrag = (e) => {
    isDragging = true;
    const clientY = e.type.startsWith('touch') ? e.touches[0].clientY : e.clientY;
    const trayRect = topTray.getBoundingClientRect();
    startHeight = trayRect.height;
    startY = clientY;
    
    document.body.style.cursor = 'row-resize';
    document.body.style.userSelect = 'none';
    e.preventDefault();
    e.stopPropagation();
  };
  
  const onDrag = (e) => {
    if (!isDragging) return;
    
    const clientY = e.type.startsWith('touch') ? e.touches[0].clientY : e.clientY;
    const deltaY = clientY - startY;
    let newHeight = startHeight + deltaY;
    
    // Constrain height - allow sliding all the way up (min 0) or down
    const minHeight = 0;
    const maxHeight = window.innerHeight; // Allow sliding all the way
    newHeight = Math.max(minHeight, Math.min(maxHeight, newHeight));
    
    // Apply height using flex-basis for proper flex behavior
    topTray.style.flex = `0 0 ${newHeight}px`;
    topTray.style.height = `${newHeight}px`;
    topTray.style.minHeight = `${newHeight}px`;
    topTray.style.maxHeight = `${newHeight}px`;
    
    // Hide content if collapsed
    if (newHeight < 50) {
      topTray.style.overflow = 'hidden';
      topTray.style.opacity = '0.3';
    } else {
      topTray.style.overflow = '';
      topTray.style.opacity = '1';
    }
    
    e.preventDefault();
    e.stopPropagation();
  };
  
  const endDrag = () => {
    if (!isDragging) return;
    isDragging = false;
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  };
  
  // Add event listeners
  topGrabber.addEventListener('mousedown', startDrag);
  topGrabber.addEventListener('touchstart', startDrag, { passive: false });
  topGrabber.addEventListener('click', (e) => console.log('Top grabber clicked', e));
  document.addEventListener('mousemove', onDrag);
  document.addEventListener('touchmove', onDrag, { passive: false });
  document.addEventListener('mouseup', endDrag);
  document.addEventListener('touchend', endDrag);
  document.addEventListener('touchcancel', endDrag);
  document.addEventListener('mouseleave', endDrag);
  
  console.log('Top grabber initialized');
}

// Data
let savedFilters = [];
let seedSources = [];
let currentSearchId = null;
let signalRConnection = null;
let columns = ['seed','score'];
let results = [];
let sortCol = 'score';
let sortAsc = false;
let searchState = 'START'; // START | RUNNING
let isSettingDropdownProgrammatically = false; // Flag to prevent onchange from firing when setting dropdown programmatically
let statusPollInterval = null; // Interval for polling search status
let lastValidFilterHash = null; // Hash of last valid filter structure
let isFilterInvalidated = false; // Flag indicating filter structure changed
let lastColumnStructure = []; // Array of column names from last valid filter
let isFormatting = false; // Flag to track when format button is used

async function loadHealth() {
  try {
    const r = await fetch('/health');
    if (!r.ok) throw new Error('health not ok');
    setStatus('Ready');
    return true;
  } catch { setStatus('Offline'); return false; }
}

async function loadFilters(autoSelect = true) {
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  if (!dd) {
    console.error('loadFilters: filterSelect or filtersDropdown element not found');
    return;
  }
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
      // Don't trigger if we're setting the dropdown programmatically
      if (isSettingDropdownProgrammatically) return;
      const idx = parseInt(dd.value);
      await selectFilterAndRun(idx);
    };
    // If first filter exists, select and load (but don't auto-start search on initial load)
    if (autoSelect && savedFilters.length > 0) { 
      await selectFilter(0, false); 
    }
  } catch (e) {
    dd.innerHTML = '<option>Offline / Error</option>';
    setStatus('Failed to load filters: ' + e.message);
  }
}

async function selectFilter(idx, autoStart = true) {
  const f = savedFilters[idx];
  if (!f) return;

  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
  // Set flag to prevent onchange from firing when we set the value programmatically
  isSettingDropdownProgrammatically = true;
  try {
    if (dd.value !== idx.toString()) dd.value = idx.toString();
  } finally {
    // Reset flag after a microtask to ensure the value is set before onchange could fire
    Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
  }

  if (f.filterJaml) {
    setJamlValue(f.filterJaml);
    // Reset invalidation when loading a filter
    const currentHash = hashFilterStructure(f.filterJaml);
    lastValidFilterHash = currentHash;
    isFilterInvalidated = false;
    
    // Update columns from filter config
    if (f.columns && Array.isArray(f.columns)) {
      columns = f.columns;
      lastColumnStructure = [...f.columns];
    } else {
      updateColumnsFromFilter();
    }
  }

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
          // Normalize results to ensure they have tallies array and no undefined values
          results = data.results.map(r => {
            const seed = (r.seed || r.Seed || '').toString();
            const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
            const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
            return {
              seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
              score: score,
              tallies: tallies
            };
          });
          columns = data.columns || columns;
          renderResults();
          setStatus(`Found ${results.length} existing seeds`);
        }
        // Update search state based on server status
        if (data.status === 'running') {
          searchState = 'RUNNING';
          currentSearchId = searchId;
          document.getElementById('searchBtn').textContent = 'Stop Search';
          document.getElementById('searchBtn').classList.add('button-danger');
          ensureWs();
          startStatusPolling(); // Start polling to keep state in sync
        } else {
          searchState = 'START';
          document.getElementById('searchBtn').textContent = 'Start Search';
          document.getElementById('searchBtn').classList.remove('button-danger');
          stopStatusPolling(); // Stop polling if not running
        }
      }
    } catch (e) {
      console.warn('Failed to fetch existing results:', e);
    }
  }

  // 2. Only auto-start if requested (not on initial page load)
  if (autoStart) {
    if (searchState === 'RUNNING') await stopAll();
    await startSearch();
  }
}

async function selectFilterAndRun(idx) {
  await selectFilter(idx, true);
}

async function loadSeedSources() {
  const dd = document.getElementById('seedSourceDropdown');
  if (!dd) {
    console.error('loadSeedSources: seedSourceDropdown element not found');
    return;
  }
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
  if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) return;

  if (!signalRConnection) {
    signalRConnection = new signalR.HubConnectionBuilder()
      .withUrl('/searchHub')
      .build();
    
    // Handle result messages (can be string JSON or object)
    signalRConnection.on('Result', (message) => {
      let resultData;
      if (typeof message === 'string') {
        try {
          resultData = JSON.parse(message);
        } catch (e) {
          console.error('Failed to parse SignalR Result message:', e);
          return;
        }
      } else {
        resultData = message;
      }
      
      // Handle different message types
      if (resultData.type === 'filters_changed') {
        // Reload filters when they change (e.g., when a new filter is saved)
        loadFilters(false).catch(e => console.warn('Failed to reload filters:', e));
        return;
      } else if (resultData.type === 'result' && resultData.result) {
        // New result found
        const r = resultData.result || {};
        // Normalize result to ensure consistent property names and no undefined values
        const normalizedResult = {
          seed: (r.seed || r.Seed || '').toString(),
          score: (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0)),
          tallies: Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : [])
        };
        // Ensure seed is never empty/undefined
        if (!normalizedResult.seed || normalizedResult.seed === 'undefined' || normalizedResult.seed === 'null') {
          normalizedResult.seed = '';
        }
        results.push(normalizedResult);
        columns = resultData.columns || columns;
        renderResults();
      } else if (resultData.type === 'progress') {
        // Progress update - show stats
        if (resultData.searchId === currentSearchId) {
          const seedsPerSec = resultData.seedsPerSecond || 0;
          const seedsSearched = resultData.seedsSearched || 0;
          const seedsFound = resultData.seedsFound || results.length;
          const progress = resultData.totalBatches > 0 
            ? Math.round((resultData.currentBatch / resultData.totalBatches) * 100)
            : 0;
          
          setStatus(`Searching... ${seedsSearched.toLocaleString()} seeds | ${seedsPerSec.toFixed(0)} seeds/sec | Found: ${seedsFound} | ${progress}%`);
        }
      } else if (resultData.type === 'search_completed') {
        // Search finished naturally
        if (resultData.searchId === currentSearchId) {
          stopStatusPolling(); // Stop polling since search is done
          searchState = 'START';
          const btn = document.getElementById('searchBtn');
          btn.textContent = 'Start Search';
          btn.classList.remove('button-danger');
          btn.disabled = false;
          
          const seedsFound = resultData.seedsFound || results.length;
          const seedsSearched = resultData.seedsSearched || 0;
          setStatus(`Search completed! Found ${seedsFound} seeds from ${seedsSearched.toLocaleString()} searched`);
        }
      } else if (resultData.type === 'search_failed') {
        // Search failed
        if (resultData.searchId === currentSearchId) {
          stopStatusPolling(); // Stop polling since search is done
          searchState = 'START';
          const btn = document.getElementById('searchBtn');
          btn.textContent = 'Start Search';
          btn.classList.remove('button-danger');
          btn.disabled = false;
          setStatus(`Search failed: ${resultData.error || 'Unknown error'}`);
        }
      } else if (resultData.type === 'search_halted') {
        // Search was stopped
        if (resultData.searchId === currentSearchId) {
          stopStatusPolling(); // Stop polling since search is done
          searchState = 'START';
          const btn = document.getElementById('searchBtn');
          btn.textContent = 'Start Search';
          btn.classList.remove('button-danger');
          btn.disabled = false;
          setStatus('Search stopped');
        }
      }
    });
    
    signalRConnection.on('Snapshot', (snapshotResults, snapshotColumns) => {
      columns = snapshotColumns || columns;
      // Normalize snapshot results to ensure consistent format
      if (snapshotResults && Array.isArray(snapshotResults)) {
        results = snapshotResults.map(r => {
          const seed = (r.seed || r.Seed || '').toString();
          const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
          const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
          return {
            seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
            score: score,
            tallies: tallies
          };
        });
      } else {
        results = results || [];
      }
      renderResults();
    });
  }
  signalRConnection.start()
    .then(() => {
      if (currentSearchId) {
        signalRConnection.invoke('JoinSearchGroup', currentSearchId);
      }
    })
    .catch(err => console.error('SignalR connection error:', err));
}

// Helper function to extract value from result based on column name
function getValueFromResult(result, col, colIndex) {
  if (!result) return '';
  
  if (col === 'seed') {
    // Handle both camelCase and PascalCase, ensure never undefined
    const seed = result.seed || result.Seed || '';
    return seed !== undefined && seed !== null && seed !== 'undefined' ? seed : '';
  }
  if (col === 'score') {
    // Handle both camelCase and PascalCase
    const score = result.score || result.Score || 0;
    return (typeof score === 'number' && !isNaN(score)) ? score : 0;
  }
  // Custom tally column - access from tallies array
  // Columns are [seed, score, tally1, tally2...], so tally1 is at index 2
  if (result.tallies && colIndex >= 2) {
    const tallyIdx = colIndex - 2;
    const tallies = result.tallies || result.Tallies || [];
    return (tallies[tallyIdx] !== undefined && tallies[tallyIdx] !== null) ? tallies[tallyIdx] : 0;
  }
  return 0; // Default for missing values
}

function renderResults() {
  const container = document.getElementById('resultsGrid');
  if (!results || results.length === 0) {
    // Show empty table with headers instead of "No results yet"
    // Columns should already be set from filter config (seed, score, + should clause columns)
    if (!columns || columns.length === 0) {
      // Fallback to default columns if none set
      columns = ['seed', 'score'];
    }
    
    // Wrap table in container for overlay positioning
    let html = '<div class="table-wrapper">';
    html += '<table class="results-table"><thead><tr>';
    columns.forEach((c, idx) => { 
      const arrow = sortCol === c ? (sortAsc ? ' ▲' : ' ▼') : '';
      const safeCol = c.replace(/'/g, "\\'");
      const safeColHtml = c.replace(/"/g, '&quot;');
      // Add right-click context menu for editing (except seed/score)
      const canEdit = idx >= 2; // Only should clause columns can be edited
      const contextMenu = canEdit ? `oncontextmenu="event.preventDefault(); editColumnLabel(${idx}, '${safeColHtml}'); return false;"` : '';
      html += `<th ${contextMenu} onclick="toggleSort('${safeCol}')" title="${canEdit ? 'Right-click to edit label' : ''}">${c}${arrow}</th>`; 
    });
    // Add + button column
    html += `<th class="add-column-btn" onclick="addColumn()" title="Add new column">+</th>`;
    html += '</tr></thead><tbody></tbody></table>';
    html += '</div>';
    container.innerHTML = html;
    return;
  }
  
  // Find the column index for sorting
  const sortColIndex = columns.indexOf(sortCol);
  
  // Sort results
  const sorted = [...results].sort((a, b) => {
    let valA = getValueFromResult(a, sortCol, sortColIndex);
    let valB = getValueFromResult(b, sortCol, sortColIndex);
    
    // Handle numeric strings (for seed column)
    if (sortCol === 'seed') {
      // String comparison for seeds
      if (valA < valB) return sortAsc ? -1 : 1;
      if (valA > valB) return sortAsc ? 1 : -1;
      return 0;
    }
    
    // Numeric comparison for score and tallies
    // Ensure we're comparing numbers
    if (typeof valA === 'string' && !isNaN(valA)) valA = parseFloat(valA);
    if (typeof valB === 'string' && !isNaN(valB)) valB = parseFloat(valB);
    
    // Handle null/undefined
    if (valA == null) valA = 0;
    if (valB == null) valB = 0;
    
    if (valA < valB) return sortAsc ? -1 : 1;
    if (valA > valB) return sortAsc ? 1 : -1;
    return 0;
  });

  // Wrap table in container for overlay positioning
  const hasOverlay = isFilterInvalidated && results.length > 0;
  
  let html = '<div class="table-wrapper">';
  html += '<table class="results-table"><thead><tr>';
  columns.forEach((c, idx) => { 
    const arrow = sortCol === c ? (sortAsc ? ' ▲' : ' ▼') : '';
    // Escape quotes in column name for onclick attribute
    const safeCol = c.replace(/'/g, "\\'");
    const safeColHtml = c.replace(/"/g, '&quot;');
    // Add right-click context menu for editing (except seed/score)
    const canEdit = idx >= 2; // Only should clause columns can be edited
    const contextMenu = canEdit ? `oncontextmenu="event.preventDefault(); editColumnLabel(${idx}, '${safeColHtml}'); return false;"` : '';
    html += `<th ${contextMenu} onclick="toggleSort('${safeCol}')" title="${canEdit ? 'Right-click to edit label' : ''}">${c}${arrow}</th>`; 
  });
  // Add + button column
  html += `<th class="add-column-btn" onclick="addColumn()" title="Add new column">+</th>`;
  html += '</tr></thead><tbody>';
  sorted.forEach(r => {
    html += `<tr onclick="analyzeSeed('${r.seed}')">`;
    columns.forEach((col, idx) => {
      // Use getValueFromResult helper to properly extract values
      let val = getValueFromResult(r, col, idx);
      
      // Ensure val is never undefined or null
      if (val === undefined || val === null || val === 'undefined') {
        val = col === 'seed' ? '' : 0;
      }
      
      // Special case for seeds column
      if (col === 'seed') {
        html += `<td><code>${val || ''}</code></td>`;
        return;
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
  
  // Add overlay if filter is invalidated (covers tbody only, not thead)
  if (hasOverlay) {
    html += `<div class="results-overlay">
      <div class="overlay-message">
        Results may be outdated - filter structure changed. Save to restart search.
      </div>
    </div>`;
  }
  
  html += '</div>';
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

function handleSearchClick() {
  const searchBtn = document.getElementById('searchBtn');
  if (!searchBtn || searchBtn.disabled) return;
  toggleSearch();
}

async function toggleSearch() {
  if (searchState === 'RUNNING') { 
    // Update UI immediately for responsiveness
    searchState = 'START';
    const btn = document.getElementById('searchBtn');
    btn.textContent = 'Stopping...';
    btn.disabled = true;
    
    await stopAll(); 
    return; 
  }
  await startSearch();
}

async function startSearch() {
  try {
    const jaml = getJamlValue().trim();
    if (!jaml) { setStatus('Enter a filter'); return; }
    
    // Reset invalidation state when starting new search
    const currentHash = hashFilterStructure(jaml);
    lastValidFilterHash = currentHash;
    isFilterInvalidated = false;
    
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
    // Normalize results to ensure they have tallies array and no undefined values
    results = (data.results || []).map(r => {
      const seed = (r.seed || r.Seed || '').toString();
      const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
      const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
      return {
        seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
        score: score,
        tallies: tallies
      };
    });
    columns = data.columns || columns;
    renderResults();
    ensureWs();
    if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
      signalRConnection.invoke('JoinSearchGroup', currentSearchId);
    }
    searchState = 'RUNNING';
    document.getElementById('searchBtn').textContent = 'Stop Search';
    document.getElementById('searchBtn').classList.add('button-danger');
    setStatus(`Running...`);
    
    // Start polling search status to keep button state in sync
    startStatusPolling();
    
    // Reload filters to pick up the newly saved filter (if it was auto-saved)
    setTimeout(() => {
      loadFilters(false).catch(e => console.warn('Failed to reload filters after search start:', e));
    }, 500); // Small delay to ensure filter is saved on server
  } catch (e) {
    setStatus(`Failed: ${e.message}`);
  }
}

async function stopAll() {
  // Stop status polling
  stopStatusPolling();
  
  // UI already updated in toggleSearch for immediate feedback
  try {
    const r = await fetch('/search/stop-all', { method: 'POST' });
    if (r.ok) setStatus('Stopped');
  } catch (e) {
    setStatus(`Error stopping: ${e.message}`);
  }
  
  // Ensure UI is in correct state
  searchState = 'START';
  currentSearchId = null;
  const btn = document.getElementById('searchBtn');
  btn.textContent = 'Start Search';
  btn.classList.remove('button-danger');
  btn.disabled = false;
}

function startStatusPolling() {
  // Clear any existing interval
  stopStatusPolling();
  
  // Poll search status every 2 seconds to keep button state in sync
  statusPollInterval = setInterval(async () => {
    if (!currentSearchId || searchState !== 'RUNNING') {
      stopStatusPolling();
      return;
    }
    
    try {
      const r = await fetch(`/search?id=${encodeURIComponent(currentSearchId)}`);
      if (r.ok) {
        const data = await r.json();
        
        // Update button state based on server status
        if (data.status === 'running') {
          // Still running - update stats
          if (data.seedsPerSecond !== undefined) {
            const seedsSearched = data.seedsSearched || 0;
            const seedsFound = data.seedsFound || results.length;
            const progress = data.progressPercent || 0;
            setStatus(`Searching... ${seedsSearched.toLocaleString()} seeds | ${data.seedsPerSecond.toFixed(0)} seeds/sec | Found: ${seedsFound} | ${progress}%`);
          }
        } else {
          // Search completed or stopped
          searchState = 'START';
          const btn = document.getElementById('searchBtn');
          btn.textContent = 'Start Search';
          btn.classList.remove('button-danger');
          btn.disabled = false;
          
          const seedsFound = data.seedsFound || results.length;
          const seedsSearched = data.seedsSearched || 0;
          setStatus(`Search completed! Found ${seedsFound} seeds from ${seedsSearched.toLocaleString()} searched`);
          
          stopStatusPolling();
        }
      }
    } catch (e) {
      console.warn('Status poll error:', e);
    }
  }, 2000); // Poll every 2 seconds
}

function stopStatusPolling() {
  if (statusPollInterval) {
    clearInterval(statusPollInterval);
    statusPollInterval = null;
  }
}

async function clearResults() {
  // Export to Fertilizer first if we have results
  if (currentSearchId && results.length > 0) {
    try {
      setStatus('Exporting to Fertilizer before clearing...');
      const exportR = await fetch(`/search/${encodeURIComponent(currentSearchId)}/export-to-fertilizer`, {
        method: 'POST'
      });
      
      if (exportR.ok) {
        const exportData = await exportR.json();
        setStatus(`Exported ${exportData.exported || 0} seeds to Fertilizer`);
      } else {
        const error = await exportR.json();
        if (!confirm(`Fertilizer export failed: ${error.error || 'Unknown error'}. Clear results anyway?`)) {
          return; // User cancelled
        }
      }
    } catch (e) {
      if (!confirm(`Fertilizer export error: ${e.message}. Clear results anyway?`)) {
        return; // User cancelled
      }
    }
  }
  
  if (!currentSearchId) {
    // No active search - just clear UI
    results = [];
    isFilterInvalidated = false;
    renderResults();
    setStatus('Results cleared');
    return;
  }
  
  try {
    setStatus('Clearing results...');
    // Call API to delete the database file
    const r = await fetch(`/search/${encodeURIComponent(currentSearchId)}/results`, {
      method: 'DELETE'
    });
    
    if (r.ok) {
      // Clear UI results
      results = [];
      isFilterInvalidated = false;
      renderResults();
      setStatus('Results cleared from database');
    } else {
      const data = await r.json();
      setStatus(`Error: ${data.error || 'Failed to clear results'}`);
    }
  } catch (e) {
    setStatus(`Error clearing results: ${e.message}`);
    // Still clear UI even if API call fails
    results = [];
    isFilterInvalidated = false;
    renderResults();
  }
  
  // Re-render icons in case they got cleared
  if (typeof lucide !== 'undefined') {
    lucide.createIcons();
  }
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

  // Check if filter is invalidated and prompt user
  if (isFilterInvalidated && results.length > 0) {
    const response = confirm(
      'Filter structure changed. Results may be outdated.\n\n' +
      'Restart search with new filter?\n\n' +
      'OK = Yes, restart search\n' +
      'Cancel = No, just save'
    );
    
    if (response) {
      // User chose "Yes, restart search"
      // Stop current search if running
      if (currentSearchId && searchState === 'RUNNING') {
        await stopAll();
      }
      
      // Clear results
      results = [];
      isFilterInvalidated = false;
      renderResults();
      
      // Save filter first
      await saveFilterInternal();
      
      // Auto-start new search
      await startSearch();
      return;
    }
    // User chose "No, just save" - continue with save
  }
  
  await saveFilterInternal();
}

async function saveFilterInternal() {
  const jaml = getJamlValue();
  if (!jaml) { setStatus('Nothing to save'); return; }

  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
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
    
    // Update dropdown to select the saved filter, but DON'T reload the editor content
    // (keep current edits - the file was already saved with them)
    const newIdx = savedFilters.findIndex(f => f.filePath === data.filePath);
    if (newIdx >= 0) {
      const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
      dd.value = newIdx.toString();
      // Update the savedFilters entry with current jaml so it matches what's saved
      if (savedFilters[newIdx]) {
        savedFilters[newIdx].filterJaml = jaml;
        savedFilters[newIdx].filePath = data.filePath;
      }
      
      // Reset invalidation state after save
      const currentHash = hashFilterStructure(jaml);
      lastValidFilterHash = currentHash;
      isFilterInvalidated = false;
      renderResults();
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
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
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
      // Try to select the renamed one (don't auto-start)
      const newIdx = savedFilters.findIndex(f => f.name === newName);
      if (newIdx >= 0) await selectFilter(newIdx, false);
      closeSettings();
    } catch (e) { setStatus(e.message); }
  });
}

async function cloneFilter() {
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
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
      // Try to select the cloned one (don't auto-start)
      const newIdx = savedFilters.findIndex(f => f.name === newName);
      if (newIdx >= 0) await selectFilter(newIdx, false);
      closeSettings();
    } catch (e) { setStatus(e.message); }
  });
}

async function deleteFilter() {
  const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
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
  // Wait a tick to ensure DOM is fully ready
  setTimeout(() => {
    initSplitter();
    initTopGrabber();
    // Ensure icons are rendered after DOM is ready
    if (typeof lucide !== 'undefined') {
      lucide.createIcons();
    }
    // Ensure plain editor is visible by default (Monaco hidden)
    const mono = document.getElementById('monacoEditor');
    const plain = document.getElementById('filterJaml');
    if (mono && plain) {
      mono.style.display = 'none';
      plain.style.display = 'block';
      monacoMode = false;
    }
  }, 0);
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
        // Normalize results to ensure they have tallies array
        // Normalize results to ensure they have tallies array and no undefined values
        results = (data.results || []).map(r => {
          const seed = (r.seed || r.Seed || '').toString();
          const score = (typeof r.score === 'number' ? r.score : (typeof r.Score === 'number' ? r.Score : 0));
          const tallies = Array.isArray(r.tallies) ? r.tallies : (Array.isArray(r.Tallies) ? r.Tallies : []);
          return {
            seed: (seed && seed !== 'undefined' && seed !== 'null') ? seed : '',
            score: score,
            tallies: tallies
          };
        });
        columns = data.columns || columns;
        renderResults();
        
        if (data.status === 'running') {
          searchState = 'RUNNING';
          document.getElementById('searchBtn').textContent = 'Stop Search';
          document.getElementById('searchBtn').classList.add('button-danger');
          ensureWs();
          startStatusPolling(); // Start polling to show performance stats
        } else {
          searchState = 'START';
          document.getElementById('searchBtn').textContent = 'Start Search';
          document.getElementById('searchBtn').classList.remove('button-danger');
          stopStatusPolling();
        }
        
        // Show performance stats if available
        if (data.seedsPerSecond !== undefined || data.seedsSearched !== undefined) {
          const seedsSearched = data.seedsSearched || 0;
          const seedsPerSec = data.seedsPerSecond || 0;
          const progress = data.progressPercent || 0;
          const seedsFound = data.seedsFound || results.length;
          if (data.status === 'running') {
            setStatus(`Searching... ${seedsSearched.toLocaleString()} seeds | ${seedsPerSec.toFixed(0)} seeds/sec | Found: ${seedsFound} | ${progress}%`);
          } else {
            setStatus(`Search completed! Found ${seedsFound} seeds from ${seedsSearched.toLocaleString()} searched`);
          }
        }
        
        // Load filters and select the matching one
        await loadFilters(false); // Pass false to NOT auto-select first filter
        
        // Find and select the filter that matches the loaded JAML
        if (data.filterJaml) {
          const loadedJaml = data.filterJaml.trim();
          const matchingIdx = savedFilters.findIndex(f => {
            const filterJaml = (f.filterJaml || '').trim();
            return filterJaml === loadedJaml;
          });
          
          if (matchingIdx >= 0) {
            // Select the matching filter in dropdown
            const dd = document.getElementById('filterSelect') || document.getElementById('filtersDropdown');
            if (dd) {
              isSettingDropdownProgrammatically = true;
              dd.value = matchingIdx.toString();
              Promise.resolve().then(() => { isSettingDropdownProgrammatically = false; });
            }
          }
        }
        
        return;
      }
    } catch (e) {
      console.error('Failed to load shared search', e);
    }
  }

  // Normal flow - always load filters and seed sources
  await loadFilters(true);
  
  // Update columns from current filter on page load
  updateColumnsFromFilter();
  
  setStatus('Ready');
};

// New Filter function
function newFilter() {
  const today = new Date().toISOString().split('T')[0];
  const newFilterTemplate = `dateCreated: ${today}
name: New Filter
author: User

must:
  - joker: Blueprint

should:
`;
  setJamlValue(newFilterTemplate);
  setStatus('Created new filter - click Save to save it');
  
  // Clear the filter dropdown selection so it's treated as unsaved
  const dd = document.getElementById('filterSelect');
  if (dd) {
    dd.value = '';
  }
  
  // Reset invalidation for new filter
  const currentHash = hashFilterStructure(newFilterTemplate);
  lastValidFilterHash = currentHash;
  isFilterInvalidated = false;
  
  // Update columns from new filter
  updateColumnsFromFilter();
}

// Edit column label (right-click handler)
async function editColumnLabel(columnIndex, currentLabel) {
  // Column index 0 = seed, 1 = score, 2+ = should clauses
  if (columnIndex < 2) {
    alert('Seed and Score columns cannot be edited');
    return;
  }
  
  const newLabel = prompt(`Edit label for column "${currentLabel}":`, currentLabel);
  if (newLabel === null) return; // User cancelled
  
  const jaml = getJamlValue().trim();
  if (!jaml) return;
  
  // Find the corresponding should clause (columnIndex - 2)
  const shouldClauseIndex = columnIndex - 2;
  
  // Parse JAML to find should clauses and update the label
  // This is a simplified approach - in production might want more robust parsing
  try {
    // Call API to update label
    const r = await fetch('/filters/update-column-label', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        filterJaml: jaml,
        columnIndex: shouldClauseIndex,
        newLabel: newLabel.trim()
      })
    });
    
    if (r.ok) {
      const data = await r.json();
      if (data.filterJaml) {
        setJamlValue(data.filterJaml);
        // Trigger invalidation since structure changed
        await invalidateFilter();
      }
    } else {
      setStatus('Failed to update column label');
    }
  } catch (e) {
    setStatus(`Error updating label: ${e.message}`);
  }
}

// Add new column
async function addColumn() {
  const columnType = prompt('Enter column type (joker, spectralCard, tarotCard, etc.):', 'joker');
  if (!columnType) return;
  
  const columnValue = prompt('Enter column value (card name):', 'Blueprint');
  if (!columnValue) return;
  
  const jaml = getJamlValue().trim();
  if (!jaml) {
    alert('No filter loaded');
    return;
  }
  
  // Add new should clause to JAML
  const newClause = `\nshould:\n  - ${columnType}: ${columnValue}`;
  
  // Simple append - in production might want smarter insertion
  const updatedJaml = jaml + newClause;
  setJamlValue(updatedJaml);
  
  // Trigger invalidation
  await invalidateFilter();
}
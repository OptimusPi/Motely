// ================================================
// JAML Web UI - JavaScript Functions
// ================================================

// JAML Taglines (from TUI)
const jamlTaglines = [
    "Jetting At Maximum Lightspeed",
    "Jokers Are My Legacy", 
    "Jimbo's Awesome Markup Language",
    "Just Another Markup Language",
    "Jokes And Memes Language",
    "JSON Ain't Markup Language",
    "Jury-rigged And Mostly Legal",
    "Just Absolutely Mental Logic",
    "Janky Ace Markup Language",

    "Jokers And Multiplier Love",
    "Jimbo's Ante-Multiplier Logic",
    "Jackpotting All My Luck",
    "Jokers And Motley Legends",
    "Jimbo's All-in Multiplier Logic",
    "Jokers And Multiplier Loot",
    "Jimbo's Ante-Meme Language",

    "Jimbo's Absurd Meme Lab",
    "Janky API Markup Language",
    "Justice And Machine Learning",
];

// Global State
let isSearching = false;
let searchAborted = false;
let currentSearchId = null;
let currentSearchJaml = null; // The JAML content that started the current search
let currentBatchSize = 1000000;
let isProgrammaticEdit = false; // Flag to ignore programmatic setJamlValue calls
let totalSeedsSearched = 0;
let searchResults = [];
let searchColumns = ['seed', 'score'];
let savedFilters = [];
let sortColumn = 'score';
let sortDirection = 'desc'; // 'asc' or 'desc'
const maxRows = 1000; // Display limit message (API returns up to 1000)

let runningSearchIds = [];

let selectedFilterFilePath = null;
let selectedFilterBaseHash = null;
let isFilterDirty = false;

let seedSources = [];
let currentSeedSource = 'all';
let previousSeedSource = 'all';
let isHydrationMode = false;

let ws = null;
let wsDesiredSearchId = null;

// Global editor functions
function getJamlValue() {
    if (window.jamlEditor) {
        return window.jamlEditor.getValue();
    }
    const textarea = document.getElementById('filterJaml');
    return textarea ? textarea.value : '';
}

function setJamlValue(val) {
    const textarea = document.getElementById('filterJaml');
    if (textarea) {
        textarea.value = val;
    }
    if (window.jamlEditor) {
        window.jamlEditor.setValue(val);
    }
}

function showStatus(message) {
    // Update the "Results" header in the right panel with status info
    const statusElement = document.getElementById('status');
    if (statusElement) {
        statusElement.textContent = message;
    }
}

function formatJaml() {
    const jaml = getJamlValue();
    if (!jaml.trim()) {
        showStatus('Nothing to format');
        return;
    }

    try {
        // Parse YAML to object
        const obj = jsyaml.load(jaml);
        if (!obj) {
            showStatus('Could not parse JAML');
            return;
        }

        // Dump with default block style
        const formatted = jsyaml.dump(obj, {
            indent: 2,
            lineWidth: -1,
            noRefs: true,
            sortKeys: false
        });

        // Post-process: collapse arrays of primitives to single line [1, 2, 3]
        const result = collapseSimpleArrays(formatted);

        isProgrammaticEdit = true;
        setJamlValue(result);
        isProgrammaticEdit = false;

        showStatus('Formatted!');
    } catch (e) {
        showStatus(`Format error: ${e.message}`);
    }
}

function collapseSimpleArrays(yamlStr) {
    const lines = yamlStr.split('\n');
    const result = [];
    let i = 0;

    while (i < lines.length) {
        const line = lines[i];
        // Match a key with no inline value (array will follow)
        // Also match keys with YAML anchors like "shopSlots: &o0"
        const match = line.match(/^(\s*)([a-zA-Z_][a-zA-Z0-9_]*):\s*(&\w+)?\s*$/);

        if (match) {
            const indent = match[1];
            const key = match[2];
            const anchor = match[3] || '';
            const arrayIndent = indent + '  ';
            const arrayItems = [];
            let j = i + 1;
            let isSimpleArray = true;

            // Collect array items
            while (j < lines.length) {
                const itemLine = lines[j];
                // Match "  - value" or "  - 'value'" with flexible spacing
                const itemMatch = itemLine.match(/^(\s*)-\s+(.*)$/);
                
                if (itemMatch && itemMatch[1] === arrayIndent) {
                    const value = itemMatch[2].trim();
                    if (isSimpleValue(value)) {
                        arrayItems.push(value);
                        j++;
                    } else {
                        isSimpleArray = false;
                        break;
                    }
                } else if (itemLine.trim() === '') {
                    j++;
                } else {
                    break;
                }
            }

            if (isSimpleArray && arrayItems.length > 0) {
                const anchorPart = anchor ? ` ${anchor}` : '';
                result.push(`${indent}${key}:${anchorPart} [${arrayItems.join(', ')}]`);
                i = j;
                continue;
            }
        }

        result.push(line);
        i++;
    }

    return result.join('\n');
}

function isSimpleValue(value) {
    if (!value || value.trim() === '') return false;
    if (/^-?\d+(\.\d+)?$/.test(value)) return true;
    if (/^["'].*["']$/.test(value)) return true;
    if (/^[a-zA-Z0-9_-]+$/.test(value)) return true;
    if (!value.includes(':') && !value.startsWith('{') && !value.startsWith('[')) return true;
    return false;
}

function ensureWebSocket() {
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) {
        return;
    }

    const proto = location.protocol === 'https:' ? 'wss' : 'ws';
    ws = new WebSocket(`${proto}://${location.host}/ws`);

    ws.onopen = () => {
        try {
            if (wsDesiredSearchId) {
                ws.send(JSON.stringify({ type: 'subscribe', searchId: wsDesiredSearchId }));
            }
        } catch (e) {
            console.error('WebSocket subscribe error:', e);
        }
    };

    ws.onclose = () => {
        ws = null;
    };

    ws.onmessage = (evt) => {
        try {
            const msg = JSON.parse(evt.data);
            if (!msg || !msg.type) return;

            if (msg.type === 'snapshot') {
                if (msg.searchId) {
                    currentSearchId = msg.searchId;

                    if (msg.isBackgroundRunning) {
                        if (!runningSearchIds.includes(msg.searchId)) runningSearchIds.push(msg.searchId);
                        if (searchButtonState !== 'STOPPING') updateSearchButton('RUNNING', 0);
                    } else {
                        runningSearchIds = runningSearchIds.filter(id => id !== msg.searchId);
                        updateSearchButton('CONTINUE', (msg.progressPercent || 0) / 100);
                    }

                    if (msg.lastError) {
                        showStatus(`Search error: ${msg.lastError}`);
                    }

                    if (msg.results && Array.isArray(msg.results)) {
                        searchResults = msg.results;
                        if (searchResults.length > 0) {
                            document.getElementById('shareBtn').disabled = false;
                        }
                        displayResults({ results: searchResults, columns: msg.columns || searchColumns });
                    }

                    loadFilters();
                }
                return;
            }

            if (msg.type === 'search_failed') {
                if (msg.searchId && currentSearchId && msg.searchId !== currentSearchId) return;
                isSearching = false;
                updateSearchButton('START', 0);
                showStatus(`Search failed: ${msg.error || 'unknown error'}`);
                if (msg.searchId) {
                    runningSearchIds = runningSearchIds.filter(id => id !== msg.searchId);
                }
                loadFilters();
                return;
            }

            if (msg.type === 'progress') {
                if (msg.searchId && currentSearchId && msg.searchId !== currentSearchId) return;
                const seedsSearched = msg.seedsSearched || 0;
                const seedsPerSecond = msg.seedsPerSecond || 0;
                const foundCount = msg.seedsFound || searchResults.length;
                const threadsInUse = msg.threadsInUse || 0;

                const speedStr = seedsPerSecond >= 1000000
                    ? `${(seedsPerSecond / 1000000).toFixed(1)}M/s`
                    : seedsPerSecond >= 1000
                        ? `${(seedsPerSecond / 1000).toFixed(0)}K/s`
                        : `${seedsPerSecond.toFixed(0)}/s`;

                const batchInput = document.getElementById('batchOverride');
                if (batchInput && msg.currentBatch !== undefined) {
                    batchInput.value = msg.currentBatch;
                    batchInput.placeholder = `Current: ${msg.currentBatch}`;
                }

                if (searchButtonState !== 'STOPPING') {
                    updateSearchButton('RUNNING', 0);
                }

                const sid = msg.searchId || currentSearchId || '';
                const threadStr = threadsInUse > 0 ? `${threadsInUse}T` : '';
                showStatus(`${sid} | ${threadStr} | Batch ${msg.currentBatch || 0} | ${speedStr} | ${(seedsSearched / 1000000).toFixed(1)}M searched | ${foundCount} found`);
                return;
            }

            if (msg.type === 'result' && msg.result) {
                if (msg.searchId && currentSearchId && msg.searchId !== currentSearchId) return;
                const r = msg.result;
                mergeResults([{ seed: r.seed, score: r.score, tallies: r.tallies }]);
                if (searchResults.length > 0) {
                    document.getElementById('shareBtn').disabled = false;
                }
                displayResults({ results: searchResults, columns: msg.columns || searchColumns });
                return;
            }

            if (msg.type === 'filters_changed') {
                loadFilters();
                return;
            }

            if (msg.type === 'search_started') {
                if (msg.searchId) {
                    // Keep subscription target in sync if server announces start.
                    currentSearchId = msg.searchId;
                    subscribeToSearch(msg.searchId);
                    if (!runningSearchIds.includes(msg.searchId)) runningSearchIds.push(msg.searchId);
                    loadFilters();
                }
                return;
            }

            if (msg.type === 'search_halted') {
                if (msg.searchId && currentSearchId && msg.searchId !== currentSearchId) return;
                isSearching = false;
                updateSearchButton('CONTINUE', 0);
                showStatus(`Search halted: ${msg.reason || 'unknown'}`);
                if (msg.searchId) {
                    runningSearchIds = runningSearchIds.filter(id => id !== msg.searchId);
                }
                loadFilters();
                return;
            }
        } catch (e) {
            console.error('WebSocket message processing error:', e);
        }
    };
}

function subscribeToSearch(searchId) {
    wsDesiredSearchId = searchId || null;
    try {
        if (ws && ws.readyState === WebSocket.OPEN && wsDesiredSearchId) {
            ws.send(JSON.stringify({ type: 'subscribe', searchId: wsDesiredSearchId }));
        }
    } catch (e) {
        console.error('WebSocket subscribe error:', e);
    }
}

// Sync URL with current search ID (so refresh/bookmark works)
function updateUrlWithSearchId(searchId) {
    const url = new URL(window.location);
    if (searchId) {
        url.searchParams.set('search', searchId);
    } else {
        url.searchParams.delete('search');
    }
    // Update URL without reloading page
    window.history.replaceState({}, '', url);
}

function canonicalizeJamlForHash(jaml) {
    const text = (jaml ?? '').toString();
    if (!text.trim()) return '';
    if (typeof jsyaml !== 'undefined') {
        try {
            const parsed = jsyaml.load(text);
            return jsyaml.dump(parsed, {
                indent: 2,
                lineWidth: -1,
                noArrayIndent: true,
                sortKeys: false,
                quotingType: "'",
                forceQuotes: false,
                flowLevel: -1
            });
        } catch {
        }
    }
    return text.replace(/\s+/g, '');
}

function hashStringDjb2(str) {
    let h = 5381;
    for (let i = 0; i < str.length; i++) {
        h = ((h << 5) + h) + str.charCodeAt(i);
        h = h | 0;
    }
    return h.toString();
}

function computeJamlHash(jaml) {
    return hashStringDjb2(canonicalizeJamlForHash(jaml));
}

function upsertNameFieldClient(jaml, newName) {
    const safe = (newName ?? '').toString().trim();
    if (!safe) return jaml;

    if (/^name:\s*.+$/m.test(jaml)) {
        return jaml.replace(/^name:\s*.+$/m, `name: ${safe}`);
    }
    
    // If no name field exists, add it after the first line or at the beginning
    const lines = jaml.split('\n');
    if (lines.length === 0) return `name: ${safe}`;
    
    // Insert name after first line if first line is not empty, otherwise at beginning
    if (lines[0].trim()) {
        lines.splice(1, 0, `name: ${safe}`);
    } else {
        lines.unshift(`name: ${safe}`);
    }
    
    return lines.join('\n');
}

function extractFromJaml(jaml, field) {
    if (!jaml || !field) return null;
    
    const regex = new RegExp(`^${field}:\\s*(.+)$`, 'm');
    const match = jaml.match(regex);
    return match ? match[1].trim() : null;
}

// Toggle between Monaco and Plain text editor
let usePlainEditor = false;

function setEditorMode(mode) {
    const monacoContainer = document.getElementById('monacoEditor');
    const plainTextarea = document.getElementById('filterJaml');
    const monacoBtn = document.getElementById('monacoBtn');
    const plainBtn = document.getElementById('plainBtn');

    if (mode === 'plain') {
        // Switch to plain editor
        usePlainEditor = true;

        // Sync content from Monaco to textarea
        if (window.jamlEditor) {
            plainTextarea.value = window.jamlEditor.getValue();
        }
        monacoContainer.style.display = 'none';
        plainTextarea.style.display = 'block';

        // Update button states
        monacoBtn.classList.remove('active');
        plainBtn.classList.add('active');

        // Override getJamlValue/setJamlValue to use textarea
        window.getJamlValue = () => plainTextarea.value;
        window.setJamlValue = (val) => {
            plainTextarea.value = val;
            if (window.jamlEditor) window.jamlEditor.setValue(val);
        };

        // Add change listener to plain textarea
        plainTextarea.oninput = () => onUserJamlEdit();
    } else {
        // Switch to Monaco editor
        usePlainEditor = false;

        // Sync content from textarea to Monaco
        if (window.jamlEditor) {
            window.jamlEditor.setValue(plainTextarea.value);
        }
        plainTextarea.style.display = 'none';
        monacoContainer.style.display = 'block';

        // Update button states
        plainBtn.classList.remove('active');
        monacoBtn.classList.add('active');

        // Restore Monaco-based getJamlValue/setJamlValue
        window.getJamlValue = () => window.jamlEditor ? window.jamlEditor.getValue() : plainTextarea.value;
        window.setJamlValue = (val) => {
            plainTextarea.value = val;
            if (window.jamlEditor) window.jamlEditor.setValue(val);
        };
    }
}

// Called when user edits JAML (not programmatic loads) - invalidates current search
// No string comparison needed: ANY user edit means the filter might be different!
function onUserJamlEdit() {
    if (isProgrammaticEdit) return; // Ignore programmatic setJamlValue calls

    const dropdown = document.getElementById('savedSearches');
    const idx = dropdown ? dropdown.value : '';
    const hasSavedSelection = idx !== '' && !!selectedFilterFilePath;

    if (hasSavedSelection && selectedFilterBaseHash !== null) {
        const newHash = computeJamlHash(getJamlValue());
        isFilterDirty = newHash !== selectedFilterBaseHash;
    } else {
        isFilterDirty = false;
        selectedFilterFilePath = null;
        selectedFilterBaseHash = null;
    }

    currentSearchId = null;
    currentSearchJaml = null;
    updateUrlWithSearchId(null); // Clear URL - filter changed
    searchResults = [];
    updateSearchButton('START', 0);

    const batchInput = document.getElementById('batchOverride');
    if (batchInput) {
        batchInput.value = '';
        batchInput.placeholder = 'Batch #';
    }

    if (hasSavedSelection && isFilterDirty) {
        showStatus('Filter changed - save or start new search');
    } else {
        showStatus('Filter changed - ready to start new search');
    }

    const optIndex = idx !== '' ? parseInt(idx) : -1;
    if (dropdown && optIndex >= 0 && savedFilters[optIndex]) {
        const f = savedFilters[optIndex];
        const isRunning = !!(f.searchId && runningSearchIds.includes(f.searchId));
        const statusDot = isFilterDirty ? '🟡' : (isRunning ? '🟢' : '🔴');
        dropdown.options[optIndex + 1].text = `${statusDot} ${f.name}`;

        const settingsModal = document.getElementById('settingsModal');
        if (settingsModal && settingsModal.style.display !== 'none') {
            refreshSettingsModalUI();
        }
    }
}

function loadSavedSearch() {
    const dropdown = document.getElementById('savedSearches');
    const idx = dropdown.value;
    
    if (idx === '' || !savedFilters[parseInt(idx)]) {
        return;
    }
    
    const filter = savedFilters[parseInt(idx)];
    
    // Load the filter into the editor
    isProgrammaticEdit = true;
    setJamlValue(filter.filterJaml);
    isProgrammaticEdit = false;
    
    // Update state
    selectedFilterFilePath = filter.filePath;
    selectedFilterBaseHash = computeJamlHash(filter.filterJaml);
    isFilterDirty = false;
    
    // Update current search ID if available
    if (filter.searchId) {
        currentSearchId = filter.searchId;
        updateUrlWithSearchId(filter.searchId);
    }
    
    // Update dropdown status indicator and button state
    const isRunning = !!(filter.searchId && runningSearchIds.includes(filter.searchId));
    const statusDot = isFilterDirty ? '🟡' : (isRunning ? '🟢' : '🔴');
    dropdown.options[parseInt(idx) + 1].text = `${statusDot} ${filter.name}`;

    // Update button to reflect running state
    if (isRunning) {
        updateSearchButton('RUNNING', 0);
        showStatus(`Loaded: ${filter.name} (running)`);
        ensureWebSocket();
        subscribeToSearch(filter.searchId);
    } else if (filter.searchId) {
        updateSearchButton('CONTINUE', 0);
        showStatus(`Loaded: ${filter.name}`);
        ensureWebSocket();
        subscribeToSearch(filter.searchId);
    } else {
        updateSearchButton('START', 0);
        showStatus(`Loaded: ${filter.name}`);
    }
}

function settingsSelectFilter() {
    const dropdown = document.getElementById('settingsSavedSearches');
    const idx = dropdown.value;
    
    if (idx === '' || !savedFilters[parseInt(idx)]) {
        return;
    }
    
    const filter = savedFilters[parseInt(idx)];
    
    // Update the editor with the selected filter
    isProgrammaticEdit = true;
    setJamlValue(filter.filterJaml);
    isProgrammaticEdit = false;
    
    // Update state
    selectedFilterFilePath = filter.filePath;
    selectedFilterBaseHash = computeJamlHash(filter.filterJaml);
    isFilterDirty = false;
    
    // Update current search ID if available
    if (filter.searchId) {
        currentSearchId = filter.searchId;
        updateUrlWithSearchId(filter.searchId);
    }
    
    showStatus(`Loaded: ${filter.name}`);
    
    // Update UI
    refreshSettingsModalUI();
}

// ================================================
// Filter Builder Functions
// ================================================
function updateBuilderValues2() {
    // Initialize filter builder UI values
    // This would populate dropdowns and set up the filter builder interface
    console.log('Filter builder initialized');
}

function quickAnalyze(seed) {
    // Quick analysis of a seed - could open a modal or navigate to analysis
    console.log('Quick analyze seed:', seed);
    alert(`Analysis for seed ${seed} - feature not yet implemented`);
}

async function checkExistingSearchStatus(searchId) {
    try {
        const response = await fetch(`/search?id=${encodeURIComponent(searchId)}`);
        if (!response.ok) return;
        
        const data = await response.json();
        
        // Load the filter JAML into the editor if available
        if (data.filterJaml) {
            isProgrammaticEdit = true;
            setJamlValue(data.filterJaml);
            isProgrammaticEdit = false;
            selectedFilterBaseHash = computeJamlHash(data.filterJaml);
            isFilterDirty = false;
        }

        // Load results if available
        if (data.results && Array.isArray(data.results) && data.results.length > 0) {
            searchResults = data.results;
            searchColumns = data.columns || ['seed', 'score'];
            displayResults({ results: searchResults, columns: searchColumns });
            document.getElementById('shareBtn').disabled = false;
        }

        // Find and select the filter in the dropdown by searchId
        const dropdown = document.getElementById('savedSearches');
        if (dropdown) {
            const idx = savedFilters.findIndex(f => f.searchId === searchId);
            if (idx >= 0) {
                dropdown.value = idx.toString();
                selectedFilterFilePath = savedFilters[idx].filePath;
            }
        }

        currentSearchId = searchId;
        
        if (data.isBackgroundRunning || data.status === 'running' || data.searchStatus === 'running') {
            if (!runningSearchIds.includes(searchId)) runningSearchIds.push(searchId);
            updateSearchButton('RUNNING', 0);
            showStatus(`Resuming search ${searchId}`);
            ensureWebSocket();
            subscribeToSearch(searchId);
        } else {
            // Search exists but not running - show Continue
            const progress = (data.progressPercent || 0) / 100;
            updateSearchButton('CONTINUE', progress);
            showStatus(`Loaded search ${searchId} - ${searchResults.length} results`);
            ensureWebSocket();
            subscribeToSearch(searchId);
        }
    } catch (e) {
        console.error('Failed to check search status:', e);
    }
}

// ================================================
// Initialization
// ================================================
document.addEventListener('DOMContentLoaded', async function() {
    // Load filters FIRST (so dropdown is populated)
    await loadFilters();
    startTaglineRotation();
    ensureWebSocket();
    initPanelSplitter();

    // Load search ID from URL if present and check its status
    const urlParams = new URLSearchParams(window.location.search);
    const searchId = urlParams.get('search');
    if (searchId) {
        currentSearchId = searchId;
        await checkExistingSearchStatus(searchId);
    }
});

// ================================================
// JAML Branding Functions
// ================================================
function startTaglineRotation() {
    const taglineElement = document.getElementById('jaml-tagline');
    let currentIndex = 0;
    
    // Rotate tagline every 5 seconds
    setInterval(() => {
        currentIndex = (currentIndex + 1) % jamlTaglines.length;
        taglineElement.style.opacity = '0';
        
        setTimeout(() => {
            taglineElement.textContent = jamlTaglines[currentIndex];
            taglineElement.style.opacity = '1';
        }, 300);
    }, 5000);
    
    // Click to manually cycle
    taglineElement.addEventListener('click', () => {
        currentIndex = (currentIndex + 1) % jamlTaglines.length;
        taglineElement.textContent = jamlTaglines[currentIndex];
    });
}

// ================================================
// Tab Management
// ================================================
function switchTab(tabName, tabButton) {
    // Hide all tab contents
    const tabs = document.querySelectorAll('.tab-content');
    tabs.forEach(tab => tab.classList.remove('active'));
    
    // Remove active class from all tab buttons
    const tabButtons = document.querySelectorAll('.tab');
    tabButtons.forEach(btn => btn.classList.remove('active'));
    
    // Show selected tab and mark button as active
    document.getElementById(tabName + '-tab').classList.add('active');
    if (tabButton) tabButton.classList.add('active');
}

// ================================================
// JSON to JAML Conversion
// ================================================
async function convertJsonToJaml() {
    const filterContent = getJamlValue().trim();

    if (!filterContent) {
        showStatus('Paste JSON filter content first');
        return;
    }

    // Check if it looks like JSON (starts with { or has "type":)
    if (!filterContent.startsWith('{') && !filterContent.includes('"type"')) {
        showStatus('Content doesn\'t look like JSON - already JAML?');
        return;
    }

    showStatus(' Converting JSON to JAML...');

    try {
        const response = await fetch('/convert', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ jsonContent: filterContent })
        });

        const data = await response.json();

        if (!response.ok) {
            showStatus(` Convert failed: ${data.error || 'Unknown error'}`);
            return;
        }

        setJamlValue(data.jaml);
        showStatus(' Converted to JAML! Review and start search.');

    } catch (error) {
        showStatus(` Convert error: ${error.message}`);
    }
}

// ================================================
// Genie Functions
// ================================================
async function generateJAML() {
    const prompt = document.getElementById('geniePrompt').value.trim();
    const statusDiv = document.getElementById('genieStatus');

    if (!prompt) {
        statusDiv.innerHTML = '<div class="status-message error">Please enter a description!</div>';
        return;
    }

    statusDiv.innerHTML = '<div class="status-message loading"> Genie is thinking...</div>';

    try {
        const response = await fetch('/genie', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt })
        });

        const data = await response.json();

        if (!response.ok) {
            statusDiv.innerHTML = `<div class="status-message error">Genie error: ${data.error || 'Failed to generate JAML'}</div>`;
            return;
        }

        const jaml = data.jaml;
        setJamlValue(jaml);

        // Switch to JAML tab
        document.querySelector('.tab:nth-child(2)').click();
        
        statusDiv.innerHTML = '<div class="status-message success"> JAML generated! Switched to editor.</div>';
    } catch (error) {
        statusDiv.innerHTML = `<div class="status-message error">Genie error: ${error.message}</div>`;
    }
}

// ================================================
// Search Functions
// ================================================
// Track button state explicitly - NEVER rely on button text for control flow!
let searchButtonState = 'START'; // 'START' | 'RUNNING' | 'CONTINUE' | 'STOPPING'

function updateSearchButton(state, progress = 0) {
    if (typeof state !== 'string' || !state) {
        state = searchButtonState;
    }

    searchButtonState = state;

    const btn = document.getElementById('searchBtn');
    if (!btn) return;

    const pct = (typeof progress === 'number' && isFinite(progress)) ? Math.max(0, Math.min(1, progress)) : 0;

    if (searchButtonState === 'RUNNING') {
        btn.textContent = 'Stop Search';
        btn.className = 'button-primary';
        btn.disabled = false;
        isSearching = true;
        return;
    }

    if (searchButtonState === 'CONTINUE') {
        const p = Math.round(pct * 100);
        btn.textContent = p > 0 ? `Continue (${p}%)` : 'Continue';
        btn.className = 'button-primary';
        btn.disabled = false;
        isSearching = false;
        return;
    }

    if (searchButtonState === 'STOPPING') {
        btn.textContent = 'Starting...';
        btn.className = 'button-blue';
        btn.disabled = true;
        return;
    }

    btn.textContent = 'Start Search';
    btn.className = 'button-primary';
    btn.disabled = false;
    isSearching = false;
}

function toggleSearch() {
    if ((searchButtonState === 'START' || searchButtonState === 'CONTINUE') && isFilterDirty && selectedFilterFilePath) {
        void saveDirtyFilter();
        return;
    }

    // Use explicit state machine - NEVER rely on button text!
    switch (searchButtonState) {
        case 'RUNNING':
            stopSearch();
            break;
        case 'STOPPING':
            // Ignore clicks while stop is in progress - prevents race condition
            console.log('Stop in progress, ignoring click');
            break;
        case 'CONTINUE':
            continueSearch();
            break;
        case 'START':
        default:
            runSearch();
            break;
    }
}

async function continueSearch() {
    // Just call runSearch - the server's POST /search already handles resume
    // via bgState.StartBatch which was saved when we stopped
    return runSearch();
}

async function runSearch() {
    let filterJaml = getJamlValue();
    const resultsContainer = document.getElementById('resultsGrid');

    if (!filterJaml.trim()) {
        resultsContainer.innerHTML = '<div class="status-message error">Please enter a filter!</div>';
        return;
    }

    if (isSearching) {
        showStatus('Search already running...');
        return;
    }

    isSearching = true;
    searchAborted = false;

    // Only clear results if filter changed! Same filter = keep accumulating via fertilizer!
    const filterChanged = currentSearchJaml && currentSearchJaml !== filterJaml.trim();
    if (filterChanged || !currentSearchJaml) {
        searchResults = []; // Clear only if filter changed or first search
        showStatus('Filter changed - clearing old results...');
    } else {
        showStatus('Same filter - keeping existing results...');
    }

    // CRITICAL: Set state to STOPPING to prevent race conditions during POST
    // This also disables the button via updateSearchButton
    searchButtonState = 'STOPPING';  // Temporarily block clicks
    const searchBtn = document.getElementById('searchBtn');
    searchBtn.textContent = 'Starting...';
    searchBtn.className = 'button-blue';
    searchBtn.disabled = true;

    let timeoutId = null;
    try {
        // ONE POST to start search
        showStatus('Starting search...');

        // Check for batch override
        const batchOverrideInput = document.getElementById('batchOverride');
        const batchOverride = batchOverrideInput && batchOverrideInput.value ? parseInt(batchOverrideInput.value) : null;

        // Check for cutoff override
        const cutoffOverrideInput = document.getElementById('cutoffOverride');
        const cutoffOverride = cutoffOverrideInput && cutoffOverrideInput.value !== '' ? parseInt(cutoffOverrideInput.value) : null;

        const requestBody = { filterJaml, seedCount: 100000000 };
        if (currentSeedSource && currentSeedSource !== 'all') {
            requestBody.seedSource = currentSeedSource;
        }
        if (batchOverride !== null && !isNaN(batchOverride)) {
            requestBody.startBatch = batchOverride;
        }
        if (cutoffOverride !== null && !isNaN(cutoffOverride)) {
            requestBody.cutoff = cutoffOverride;
        }

        const controller = new AbortController();
        const timeoutMs = 15000;
        timeoutId = setTimeout(() => controller.abort(), timeoutMs);

        const response = await fetch('/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestBody),
            signal: controller.signal
        });

        if (!response.ok) {
            const error = await response.json();
            showStatus(` Error: ${error.error}`);
            isSearching = false;
            updateSearchButton('START', 0);
            return;
        }

        const data = await response.json();
        currentSearchId = data.searchId;
        currentSearchJaml = filterJaml.trim(); // Save JAML that started this search
        updateUrlWithSearchId(currentSearchId); // Sync URL so refresh/bookmark works

        if (currentSearchId && !runningSearchIds.includes(currentSearchId)) {
            runningSearchIds.push(currentSearchId);
        }

        // NOW we can show Stop button - searchId is set!
        // Use updateSearchButton to keep state machine in sync!
        updateSearchButton('RUNNING', 0);

        // Handle fertilizer results based on whether filter changed
        if (filterChanged || !currentSearchJaml) {
            // Filter changed - replace with new fertilizer results (or empty if none)
            searchResults = data.results || [];
        } else {
            // Same filter - merge new fertilizer results with existing ones
            if (data.results && data.results.length > 0) {
                mergeResults(data.results);
            }
            // Keep existing searchResults if fertilizer returned empty
        }

        // ALWAYS update display
        if (searchResults.length > 0) {
            document.getElementById('shareBtn').disabled = false;
        }
        displayResults({ results: searchResults, columns: data.columns || ['seed', 'score'] });

        // POST /search returns isBackgroundRunning=true, so we KNOW it's running
        // Update filter dots to show this search is now running
        loadFilters();

        // Start WebSocket connection
        ensureWebSocket();
        subscribeToSearch(currentSearchId);

        showStatus('Search started...');

    } catch (error) {
        if (error && error.name === 'AbortError') {
            showStatus(' Network error: /search timed out');
        } else {
            showStatus(` Network error: ${error.message}`);
        }
        isSearching = false;
        updateSearchButton('START', 0);  // This also re-enables the button
    } finally {
        if (timeoutId !== null) {
            clearTimeout(timeoutId);
        }
    }
}

async function stopSearch() {
    // CRITICAL: Set STOPPING state FIRST to prevent race conditions
    // This must happen BEFORE we set isSearching = false!
    updateSearchButton('STOPPING', 0);

    // Now set flags to stop polling loop
    isSearching = false;
    searchAborted = true;

    if (!currentSearchId) {
        updateSearchButton('START', 0);
        showStatus('No search to stop');
        return;
    }

    try {
        const response = await fetch('/search/stop', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ searchId: currentSearchId })
        });

        if (!response.ok) {
            const error = await response.json();
            showStatus(`Error stopping: ${error.error}`);
            updateSearchButton('START', 0);
            return;
        }

        const data = await response.json();
        showStatus(`${data.message} - ${searchResults.length} results`);

        if (currentSearchId) {
            runningSearchIds = runningSearchIds.filter(id => id !== currentSearchId);
        }

        // Get current progress from the API to show accurate state
        const statusResponse = await fetch(`/search?id=${currentSearchId}`);
        if (statusResponse.ok) {
            const statusData = await statusResponse.json();
            const progress = statusData.progressPercent || 0;
            updateSearchButton('CONTINUE', progress / 100);

            // Sync batch input with final position
            const batchInput = document.getElementById('batchOverride');
            if (batchInput && statusData.currentBatch !== undefined) {
                batchInput.value = statusData.currentBatch;
                batchInput.placeholder = `Current: ${statusData.currentBatch}`;
            }
        } else {
            updateSearchButton('START', 0);
        }

    } catch (error) {
        showStatus(`❌ Network error: ${error.message}`);
        updateSearchButton('START', 0);
    }
}

async function stopAllSearches() {
    if (!confirm('Stop ALL running searches?')) return;
    
    showStatus('Stopping all searches...');
    try {
        const response = await fetch('/search/stop-all', { method: 'POST' });
        if (response.ok) {
            runningSearchIds = [];
            updateSearchButton('START', 0);
            showStatus('All searches stopped');
            await loadFilters();
        } else {
            // Handle empty or invalid JSON response
            try {
                const err = await response.json();
                showStatus(`Error: ${err.error || 'Failed to stop'}`);
            } catch {
                showStatus(`Error: HTTP ${response.status}`);
            }
        }
    } catch (e) {
        showStatus(`Error: ${e.message}`);
    }
}

async function saveDirtyFilter() {
    // Save the current JAML to the selected filter (or create new if name changed)
    const jaml = getJamlValue();
    if (!jaml.trim()) {
        showStatus('Cannot save empty filter');
        return;
    }

    // Extract filter name from JAML (look for "name:" line)
    const nameMatch = jaml.match(/^name:\s*(.+)$/m);
    const filterName = nameMatch ? nameMatch[1].trim() : null;

    if (!filterName) {
        showStatus('Filter must have a name: field');
        return;
    }

    // Generate filename from name (sanitize for filesystem)
    const safeName = filterName.replace(/[^a-zA-Z0-9_-]/g, '_') + '.jaml';

    try {
        showStatus('Saving filter...');

        const response = await fetch('/filters/update', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ filterId: safeName, filterJaml: jaml })
        });

        if (!response.ok) {
            const err = await response.json();
            showStatus(`Save failed: ${err.error}`);
            return;
        }

        const data = await response.json();

        // Update state to reflect saved filter - use the filename returned by API
        selectedFilterFilePath = data.filePath;
        selectedFilterBaseHash = computeJamlHash(jaml);
        isFilterDirty = false;

        showStatus(`Saved: ${filterName}`);

        // Reload filters to show updated list
        await loadFilters();

    } catch (e) {
        showStatus(`Save error: ${e.message}`);
    }
}

function shareSearch() {
    // Share current filter - works even if search isn't running!
    let searchIdToShare = currentSearchId;

    // If no currentSearchId, try to get from selected dropdown item
    if (!searchIdToShare) {
        const dropdown = document.getElementById('savedSearches');
        const idx = dropdown.value;
        if (idx !== '' && savedFilters[parseInt(idx)]) {
            searchIdToShare = savedFilters[parseInt(idx)].searchId;
        }
    }

    if (!searchIdToShare) {
        alert('Please select a filter first');
        return;
    }

    const url = new URL(window.location);
    url.searchParams.set('search', searchIdToShare);

    navigator.clipboard.writeText(url.toString()).then(() => {
        const btn = document.getElementById('shareBtn');
        const originalText = btn.textContent;
        btn.textContent = '✅ Copied!';
        setTimeout(() => btn.textContent = originalText, 2000);
    });
}

function mergeResults(newResults) {
    const existingSeeds = new Set(searchResults.map(r => r.seed));
    for (const result of newResults) {
        if (!existingSeeds.has(result.seed)) {
            searchResults.push(result);
            existingSeeds.add(result.seed);
        }
    }
    
    // Silently re-apply current sort when new results flow in
    applySortToResults();
}

function applySortToResults() {
    searchResults.sort((a, b) => {
        let valueA, valueB;
        
        if (sortColumn === 'seed') {
            valueA = a.seed;
            valueB = b.seed;
        } else if (sortColumn === 'score') {
            valueA = a.score;
            valueB = b.score;
        } else {
            // Tally column
            const colIndex = searchColumns.indexOf(sortColumn);
            if (colIndex >= 2) {
                const tallyIndex = colIndex - 2;
                valueA = a.tallies?.[tallyIndex] || 0;
                valueB = b.tallies?.[tallyIndex] || 0;
            } else {
                return 0;
            }
        }
        
        if (valueA < valueB) return sortDirection === 'asc' ? -1 : 1;
        if (valueA > valueB) return sortDirection === 'asc' ? 1 : -1;
        return 0;
    });
}

// ================================================
// Sorting Functions
// ================================================
function sortResults(column) {
    // Toggle direction if clicking same column
    if (sortColumn === column) {
        sortDirection = sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
        sortColumn = column;
        sortDirection = column === 'seed' ? 'asc' : 'desc'; // Seeds A-Z by default, scores high-low
    }
    
    // Apply sort and re-display
    applySortToResults();
    displayResults({ results: searchResults, columns: searchColumns });
}

// ================================================
// Results Display
// ================================================
function displayResults(data) {
    const container = document.getElementById('resultsGrid');
    
    if (!data.results || data.results.length === 0) {
        container.innerHTML = `
            <div class="no-results">
                <p>🎰 No results yet</p>
            </div>
        `;
        return;
    }

    // Store columns for sorting
    searchColumns = data.columns || ['seed', 'score'];
    
    let html = `
        <table class="results-table">
            <thead>
                <tr>
    `;
    
    // Add clickable headers with sort indicators
    // Use ' - ' placeholder replaced by arrow to prevent column width jumping
    searchColumns.forEach(column => {
        const isCurrentSort = sortColumn === column;
        const arrow = isCurrentSort ? (sortDirection === 'asc' ? '↑' : '↓') : '-';
        const displayName = column === 'seed' ? 'Seed' : 
                           column === 'score' ? 'Score' : column;
        html += `<th onclick="sortResults('${column}')" style="cursor: pointer; user-select: none; text-align: left;">${arrow} ${displayName}</th>`;
    });
    
    html += `
                </tr>
            </thead>
            <tbody>
    `;

    // Add result rows (show all 1000 from API)
    const displayResults = data.results;
    
    displayResults.forEach(result => {
        html += `
            <tr onclick="quickAnalyze('${result.seed}')" style="cursor: pointer;" title="Click to analyze this seed">
                <td><code>${result.seed}</code></td>
                <td>${result.score}</td>
        `;
        
        if (result.tallies && result.tallies.length > 0) {
            result.tallies.forEach(tally => {
                html += `<td>${tally}</td>`;
            });
        }
        
        html += '</tr>';
    });

    html += `
            </tbody>
        </table>
    `;

    if (data.results.length > maxRows) {
        html += `<div class="info-text">Showing top ${maxRows} of ${data.results.length} results</div>`;
    }

    container.innerHTML = html;
}

// ================================================
// Filter Management
// ================================================
async function loadFilters() {
    try {
        const response = await fetch('/filters');
        if (response.ok) {
            const data = await response.json();
            // Handle both old (array) and new (object with filters array) response formats
            savedFilters = data.filters || data;

            runningSearchIds = data.runningSearchIds || (data.runningSearchId ? [data.runningSearchId] : []);

            const desiredFilePath = selectedFilterFilePath;
            const desiredSearchId = currentSearchId;

            const dropdown = document.getElementById('savedSearches');
            dropdown.innerHTML = '<option value="">Select a filter...</option>';
            
            // Sort filters: running (green) first, then stopped (red), alphabetically within each group
            const sortedIndices = savedFilters.map((filter, i) => {
                const isRunning = !!(filter.searchId && runningSearchIds.includes(filter.searchId));
                return { index: i, isRunning, name: filter.name };
            }).sort((a, b) => {
                if (a.isRunning !== b.isRunning) return b.isRunning - a.isRunning; // Running first
                return a.name.localeCompare(b.name); // Then alphabetically
            });

            sortedIndices.forEach(({ index: i }) => {
                const filter = savedFilters[i];
                // Show GREEN dot if this filter is running, RED dot if not
                const isRunning = !!(filter.searchId && runningSearchIds.includes(filter.searchId));
                const isDirtySelected = !!(isFilterDirty && desiredFilePath && filter.filePath && filter.filePath === desiredFilePath);
                const statusDot = isDirtySelected ? '🟡' : (isRunning ? '🟢' : '🔴');
                dropdown.innerHTML += `<option value="${i}">${statusDot} ${filter.name}</option>`;
            });

            if (desiredFilePath) {
                const idx = savedFilters.findIndex(f => f.filePath && f.filePath === desiredFilePath);
                if (idx >= 0) dropdown.value = idx.toString();
            } else if (desiredSearchId) {
                const idx = savedFilters.findIndex(f => f.searchId === desiredSearchId);
                if (idx >= 0) {
                    dropdown.value = idx.toString();
                }
            }
        }
    } catch (e) {
        console.error('Failed to load filters', e);
    }
}

function openSettingsModal() {
    const modal = document.getElementById('settingsModal');
    
    if (!modal) return;
    modal.style.display = 'flex';
    refreshSettingsModalUI();
    loadSeedSourcesForSettings(); // Load seeds when modal opens
}

function closeSettingsModal() {
    const modal = document.getElementById('settingsModal');
    if (!modal) return;
    modal.style.display = 'none';
    
    // Restore main dropdown selection after closing settings
    const dropdown = document.getElementById('savedSearches');
    if (dropdown && selectedFilterFilePath) {
        const idx = savedFilters.findIndex(f => f.filePath && f.filePath === selectedFilterFilePath);
        if (idx >= 0) {
            dropdown.value = idx.toString();
        }
    }
}

function refreshSettingsModalUI() {
    // Update the settings modal UI with current values
    const seedSourceSelect = document.getElementById('settingsSeedSource');
    if (seedSourceSelect && currentSeedSource) {
        seedSourceSelect.value = currentSeedSource;
    }
    
    // Update word list editor visibility
    const isTxt = currentSeedSource && currentSeedSource.startsWith('txt:');
    const editBtn = document.getElementById('settingsEditWordListBtn');
    if (editBtn) {
        editBtn.disabled = !isTxt;
    }
    
    // Populate the settings modal filter dropdown
    const settingsDropdown = document.getElementById('settingsSavedSearches');
    if (settingsDropdown) {
        settingsDropdown.innerHTML = '<option value="">Select a filter...</option>';
        savedFilters.forEach((filter, i) => {
            const isRunning = !!(filter.searchId && runningSearchIds.includes(filter.searchId));
            const isDirtySelected = !!(isFilterDirty && selectedFilterFilePath && filter.filePath && filter.filePath === selectedFilterFilePath);
            const statusDot = isDirtySelected ? '🟡' : (isRunning ? '🟢' : '🔴');
            settingsDropdown.innerHTML += `<option value="${i}">${statusDot} ${filter.name}</option>`;
        });
        
        // Set selected filter if one is currently selected
        if (selectedFilterFilePath) {
            const idx = savedFilters.findIndex(f => f.filePath && f.filePath === selectedFilterFilePath);
            if (idx >= 0) settingsDropdown.value = idx.toString();
        }
    }
}

function deleteSelectedFilterFromSettings() {
    const dropdown = document.getElementById('settingsSavedSearches');
    const idx = dropdown.value;
    
    if (idx === '' || !savedFilters[parseInt(idx)]) {
        alert('Please select a filter to delete');
        return;
    }
    
    const filter = savedFilters[parseInt(idx)];
    
    if (!confirm(`Are you sure you want to delete "${filter.name}"?`)) {
        return;
    }
    
    // Don't close the modal - just delete and refresh
    deleteFilter(filter.filePath, false);
}

async function deleteFilter(filterId, closeAfterDelete = true) {
    try {
        const response = await fetch(`/filters/${encodeURIComponent(filterId)}`, {
            method: 'DELETE'
        });
        
        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || 'Delete failed');
        }
        
        showStatus(`Deleted: ${filterId}`);
        
        // Clear current selection if it was the deleted filter
        if (selectedFilterFilePath === filterId) {
            selectedFilterFilePath = null;
            selectedFilterBaseHash = null;
            isFilterDirty = false;
            currentSearchId = null;
            updateUrlWithSearchId(null);
        }
        
        // Reload filters to refresh list
        await loadFilters();
        
        // Refresh settings modal if it's open
        const modal = document.getElementById('settingsModal');
        if (modal && modal.style.display !== 'none') {
            refreshSettingsModalUI();
        }
        
        // Only close modal if requested (default behavior for non-settings delete)
        if (closeAfterDelete) {
            closeSettingsModal();
        }
        
    } catch (e) {
        alert(`Error deleting filter: ${e.message}`);
    }
}

// ================================================
// Seed Sources & Word Lists
// ================================================
async function loadSeedSourcesForSettings() {
    const select = document.getElementById('settingsSeedSource');
    if (!select) return;

    try {
        const response = await fetch('/seed-sources');
        if (!response.ok) return;
        const data = await response.json();
        seedSources = data.sources || [];

        // Preserve selection
        const currentVal = select.value;
        
        select.innerHTML = '';
        seedSources.forEach(src => {
            const opt = document.createElement('option');
            opt.value = src.key;
            opt.textContent = src.label;
            select.appendChild(opt);
        });

        // Restore or default
        if (currentSeedSource && Array.from(select.options).some(o => o.value === currentSeedSource)) {
            select.value = currentSeedSource;
        } else {
            select.value = 'all';
        }
        
        settingsSeedSourceChanged(); // Update visibility of Edit button
    } catch (e) {
        console.error('Failed to load seed sources', e);
    }
}

function settingsSeedSourceChanged() {
    const select = document.getElementById('settingsSeedSource');
    const val = select.value;
    currentSeedSource = val;
    
    // Update global state/UI for hydration
    isHydrationMode = val !== 'all' && val !== 'random:1000000';
    
    // Update main Search button text to reflect mode
    updateSearchButton(searchButtonState);

    // Show/Hide Edit/New buttons
    const actionsDiv = document.getElementById('settingsWordListActions');
    const editBtn = document.getElementById('settingsEditWordListBtn');
    
    if (actionsDiv) {
        actionsDiv.style.display = 'flex'; // Always show actions row in modal
        // But only enable Edit if it's a text file
        const isTxt = val.startsWith('txt:');
        if (editBtn) editBtn.disabled = !isTxt;
    }
}

function openNewWordListEditor() {
    document.getElementById('settingsWordListEditor').style.display = 'flex';
    document.getElementById('settingsWordListName').value = '';
    document.getElementById('settingsWordListName').disabled = false; // Editable for new
    document.getElementById('settingsWordListText').value = '';
    document.getElementById('settingsWordListActions').style.display = 'none'; // Hide buttons while editing
}

async function openEditWordListEditor() {
    const key = currentSeedSource;
    if (!key.startsWith('txt:')) return;
    
    const fileName = key.substring(4); // Remove txt: prefix
    
    try {
        const response = await fetch(`/wordlists/${encodeURIComponent(fileName)}`);
        if (!response.ok) throw new Error('Failed to load');
        
        const data = await response.json();
        
        document.getElementById('settingsWordListEditor').style.display = 'flex';
        document.getElementById('settingsWordListName').value = data.name;
        document.getElementById('settingsWordListName').disabled = true; // Cannot rename existing yet
        document.getElementById('settingsWordListText').value = data.text;
        document.getElementById('settingsWordListActions').style.display = 'none';
        
    } catch (e) {
        alert("Could not load word list: " + e.message);
    }
}

function closeWordListEditor() {
    document.getElementById('settingsWordListEditor').style.display = 'none';
    document.getElementById('settingsWordListActions').style.display = 'flex';
}

async function saveWordListFromEditor() {
    const nameInput = document.getElementById('settingsWordListName');
    const textInput = document.getElementById('settingsWordListText');
    const name = nameInput.value.trim();
    const text = textInput.value;
    
    if (!name) {
        alert("Please enter a name");
        return;
    }
    
    try {
        const response = await fetch(`/wordlists/${encodeURIComponent(name)}`, {
            method: 'PUT', // UPSERT
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text: text })
        });
        
        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || 'Save failed');
        }
        
        const data = await response.json();
        alert("Saved word list!");
        
        closeWordListEditor();
        await loadSeedSourcesForSettings(); // Refresh list
        
        // Select the new/updated one
        const select = document.getElementById('settingsSeedSource');
        if (select) {
            select.value = data.key;
            settingsSeedSourceChanged();
        }
        
    } catch (e) {
        alert("Error saving: " + e.message);
    }
}

function initPanelSplitter() {
    const splitter = document.getElementById('panelSplitter');
    const leftPanel = document.querySelector('.left-panel');
    const rightPanel = document.querySelector('.right-panel');
    const container = document.querySelector('.side-by-side');

    if (!splitter || !leftPanel || !rightPanel || !container) return;

    let isDragging = false;

    // Check if we're in stacked (vertical) mode
    function isStackedMode() {
        return window.innerWidth <= 768;
    }

    splitter.addEventListener('mousedown', (e) => {
        isDragging = true;
        document.body.style.cursor = isStackedMode() ? 'row-resize' : 'col-resize';
        splitter.classList.add('active');
        e.preventDefault();
    });

    document.addEventListener('mousemove', (e) => {
        if (!isDragging) return;

        if (isStackedMode()) {
            // Vertical resize for stacked mode
            const leftRect = leftPanel.getBoundingClientRect();
            let newHeight = e.clientY - leftRect.top;

            // Min/Max constraints
            const minHeight = 150;
            const maxHeight = window.innerHeight - 200;

            if (newHeight < minHeight) newHeight = minHeight;
            if (newHeight > maxHeight) newHeight = maxHeight;

            leftPanel.style.height = `${newHeight}px`;
            leftPanel.style.flex = 'none';
        } else {
            // Horizontal resize for side-by-side mode
            const containerRect = container.getBoundingClientRect();
            const containerWidth = containerRect.width;
            let newLeftWidth = e.clientX - containerRect.left;

            // Min/Max constraints
            const minWidth = 300;
            const maxWidth = containerWidth - 300;

            if (newLeftWidth < minWidth) newLeftWidth = minWidth;
            if (newLeftWidth > maxWidth) newLeftWidth = maxWidth;

            const widthPercent = (newLeftWidth / containerWidth) * 100;
            leftPanel.style.flex = `0 0 ${widthPercent}%`;
        }
    });

    document.addEventListener('mouseup', () => {
        if (isDragging) {
            isDragging = false;
            document.body.style.cursor = '';
            splitter.classList.remove('active');
            
            // Trigger Monaco layout refresh if it exists
            if (window.jamlEditor) {
                window.jamlEditor.layout();
            }
        }
    });

    // Also handle touch events for mobile
    splitter.addEventListener('touchstart', (e) => {
        isDragging = true;
        splitter.classList.add('active');
        e.preventDefault();
    });

    document.addEventListener('touchmove', (e) => {
        if (!isDragging) return;
        const touch = e.touches[0];

        if (isStackedMode()) {
            const leftRect = leftPanel.getBoundingClientRect();
            let newHeight = touch.clientY - leftRect.top;

            const minHeight = 150;
            const maxHeight = window.innerHeight - 200;

            if (newHeight < minHeight) newHeight = minHeight;
            if (newHeight > maxHeight) newHeight = maxHeight;

            leftPanel.style.height = `${newHeight}px`;
            leftPanel.style.flex = 'none';
        }
    });

    document.addEventListener('touchend', () => {
        if (isDragging) {
            isDragging = false;
            splitter.classList.remove('active');
            if (window.jamlEditor) {
                window.jamlEditor.layout();
            }
        }
    });
}
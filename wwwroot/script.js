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

function ensureWebSocket() {
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) {
        return;
    }

    const proto = location.protocol === 'https:' ? 'wss' : 'ws';
    ws = new WebSocket(`${proto}://${location.host}/ws`);

    ws.onmessage = (evt) => {
        try {
            const msg = JSON.parse(evt.data);
            if (!msg || !msg.type) return;

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
        } catch (e) {
            // ignore
        }
    };
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
    return `name: ${safe}\n${jaml}`;
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

// ================================================
// Initialization
// ================================================
document.addEventListener('DOMContentLoaded', async function() {
    // Load filters FIRST (so dropdown is populated)
    await loadFilters();
    startTaglineRotation();
    ensureWebSocket();

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

        const response = await fetch('/search', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(requestBody)
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

        showStatus('Search started...');

    } catch (error) {
        showStatus(` Network error: ${error.message}`);
        isSearching = false;
        updateSearchButton('START', 0);  // This also re-enables the button
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
                <p class="help-text">Search is running...</p>
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
    searchColumns.forEach(column => {
        const isCurrentSort = sortColumn === column;
        const arrow = isCurrentSort ? (sortDirection === 'asc' ? ' ↑' : ' ↓') : '';
        const displayName = column === 'seed' ? 'Seed' : 
                           column === 'score' ? 'Score' : column;
        html += `<th onclick="sortResults('${column}')" style="cursor: pointer; user-select: none;">${displayName}${arrow}</th>`;
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
            savedFilters.forEach((filter, i) => {
                // Show GREEN dot if this filter is running, RED dot if not
                const isRunning = data.isSearchRunning && filter.searchId && runningSearchIds.includes(filter.searchId);
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
}

// Initialize builder on page load
document.addEventListener('DOMContentLoaded', () => {
    updateBuilderValues2();

    // Add click handlers for ante buttons
    document.querySelectorAll('.ante-btn').forEach(btn => {
        // Add ante button functionality here if needed
        btn.addEventListener('click', () => {
            console.log('Ante button clicked');
        });
    });

    initPanelSplitter();
});

// ================================================
// Filter Builder - Item Data
// ================================================
const ITEM_DATA = {
    joker: [
            // Rare
            'DNA', 'Vagabond', 'Baron', 'Obelisk', 'BaseballCard', 'AncientJoker', 'Campfire', 'Blueprint',
            'WeeJoker', 'HitTheRoad', 'TheDuo', 'TheTrio', 'TheFamily', 'TheOrder', 'TheTribe', 'Stuntman',
            'InvisibleJoker', 'Brainstorm', 'DriversLicense', 'BurntJoker',
            // Uncommon
            'JokerStencil', 'FourFingers', 'Mime', 'CeremonialDagger', 'MarbleJoker', 'LoyaltyCard', 'Dusk',
            'Fibonacci', 'SteelJoker', 'Hack', 'Pareidolia', 'SpaceJoker', 'Burglar', 'Blackboard', 'SixthSense',
            'Constellation', 'Hiker', 'CardSharp', 'Madness', 'Seance', 'Vampire', 'Shortcut', 'Hologram',
            'Cloud9', 'Rocket', 'MidasMask', 'Luchador', 'GiftCard', 'TurtleBean', 'Erosion', 'ToTheMoon',
            'StoneJoker', 'LuckyCat', 'Bull', 'DietCola', 'TradingCard', 'FlashCard', 'SpareTrousers', 'Ramen',
            'Seltzer', 'Castle', 'MrBones', 'Acrobat', 'SockAndBuskin', 'Troubadour', 'Certificate', 'SmearedJoker',
            'Throwback', 'RoughGem', 'Bloodstone', 'Arrowhead', 'OnyxAgate', 'GlassJoker', 'Showman', 'FlowerPot',
            'MerryAndy', 'OopsAll6s', 'TheIdol', 'SeeingDouble', 'Matador', 'Satellite', 'Cartomancer', 'Astronomer', 'Bootstraps',
            // Common
            'Joker', 'GreedyJoker', 'LustyJoker', 'WrathfulJoker', 'GluttonousJoker', 'JollyJoker', 'ZanyJoker',
            'MadJoker', 'CrazyJoker', 'DrollJoker', 'SlyJoker', 'WilyJoker', 'CleverJoker', 'DeviousJoker',
            'CraftyJoker', 'HalfJoker', 'CreditCard', 'Banner', 'MysticSummit', 'EightBall', 'Misprint',
            'RaisedFist', 'ChaostheClown', 'ScaryFace', 'AbstractJoker', 'DelayedGratification', 'GrosMichel',
            'EvenSteven', 'OddTodd', 'Scholar', 'BusinessCard', 'Supernova', 'RideTheBus', 'Egg', 'Runner',
            'IceCream', 'Splash', 'BlueJoker', 'FacelessJoker', 'GreenJoker', 'Superposition', 'ToDoList',
            'Cavendish', 'RedCard', 'SquareJoker', 'RiffRaff', 'Photograph', 'ReservedParking', 'MailInRebate',
            'Hallucination', 'FortuneTeller', 'Juggler', 'Drunkard', 'GoldenJoker', 'Popcorn', 'WalkieTalkie',
            'SmileyFace', 'GoldenTicket', 'Swashbuckler', 'HangingChad', 'ShootTheMoon',
            'NewJoker', 'AnotherJoker',
            'NewJoker2', 'AnotherJoker2'
        ],
    soulJoker: ['Canio', 'Triboulet', 'Yorick', 'Chicot', 'Perkeo'],
    voucher: [
            'Overstock', 'OverstockPlus', 'ClearanceSale', 'Liquidation', 'Hone', 'GlowUp', 'RerollSurplus',
            'RerollGlut', 'CrystalBall', 'OmenGlobe', 'Telescope', 'Observatory', 'Grabber', 'NachoTong',
            'Wasteful', 'Recyclomancy', 'TarotMerchant', 'TarotTycoon', 'PlanetMerchant', 'PlanetTycoon',
            'SeedMoney', 'MoneyTree', 'Blank', 'Antimatter', 'MagicTrick', 'Illusion', 'Hieroglyph',
            'Petroglyph', 'DirectorsCut', 'Retcon', 'PaintBrush', 'Palette'
        ],
        tag: [
            'UncommonTag', 'RareTag', 'NegativeTag', 'FoilTag', 'HolographicTag', 'PolychromeTag',
            'InvestmentTag', 'VoucherTag', 'BossTag', 'StandardTag', 'CharmTag', 'MeteorTag', 'BuffoonTag',
            'HandyTag', 'GarbageTag', 'EtherealTag', 'CouponTag', 'DoubleTag', 'JuggleTag', 'D6Tag',
            'TopupTag', 'SpeedTag', 'OrbitalTag', 'EconomyTag',
            'NewTag', 'AnotherTag'
        ],
        tarot: [
            'TheFool', 'TheMagician', 'TheHighPriestess', 'TheEmpress', 'TheEmperor', 'TheHierophant',
            'TheLovers', 'TheChariot', 'Justice', 'TheHermit', 'TheWheelOfFortune', 'Strength',
            'TheHangedMan', 'Death', 'Temperance', 'TheDevil', 'TheTower', 'TheStar', 'TheMoon',
            'TheSun', 'Judgement', 'TheWorld'
        ],
        spectral: [
            'Familiar', 'Grim', 'Incantation', 'Talisman', 'Aura', 'Wraith', 'Sigil', 'Ouija',
            'Ectoplasm', 'Immolate', 'Ankh', 'DejaVu', 'Hex', 'Trance', 'Medium', 'Cryptid', 'Soul', 'BlackHole'
        ],
        planet: ['Mercury', 'Venus', 'Earth', 'Mars', 'Jupiter', 'Saturn', 'Uranus', 'Neptune', 'Pluto', 'PlanetX', 'Ceres', 'Eris'],
        boss: [
            'AmberAcorn', 'CeruleanBell', 'CrimsonHeart', 'VerdantLeaf', 'VioletVessel', 'TheArm', 'TheClub',
            'TheEye', 'TheFish', 'TheFlint', 'TheGoad', 'TheHead', 'TheHook', 'TheHouse', 'TheManacle',
            'TheMark', 'TheMouth', 'TheNeedle', 'TheOx', 'ThePillar', 'ThePlant', 'ThePsychic', 'TheSerpent',
            'TheTooth', 'TheWall', 'TheWater', 'TheWheel', 'TheWindow',
        ]
    };

    // ================================================
    // Filter Builder Functions
    // ================================================
    async function saveDirtyFilter() {
    // We can use a custom modal later, for now use confirm/prompt flow
    // But since this is triggered by a button labeled "Save Changes?", we can assume they want to save.
    // The ambiguity is: Overwrite existing file? Or Save as Copy?

    // Check if we can overwrite (is it a file we loaded?)
    const canOverwrite = !!selectedFilterFilePath;

    let action = 'cancel'; // 'overwrite', 'clone', 'cancel'

    if (canOverwrite) {
        // Simple mechanic: confirm to overwrite, cancel to see more options?
        // Or better: prompt "Click OK to overwrite [file]. Click Cancel to save as a new file."
        // This is a bit janky standard alert UX but robust for now.
        if (confirm(`Overwrite existing filter?\n\nFile: ${selectedFilterFilePath}`)) {
            action = 'overwrite';
        } else {
            if (confirm("Save as a new copy instead?")) {
                action = 'clone';
            }
        }
    } else {
        action = 'clone';
    }

    if (action === 'cancel') return;

    const currentJaml = getJamlValue();

    try {
        if (action === 'overwrite') {
            const response = await fetch('/filters/update', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    filterId: selectedFilterFilePath,
                    filterJaml: currentJaml
                })
            });

            if (!response.ok) {
                const err = await response.json();
                throw new Error(err.error || 'Update failed');
            }

            const data = await response.json();
            showStatus(`Saved: ${data.name}`);
            
            // Update local state hash so it's not dirty anymore
            selectedFilterBaseHash = computeJamlHash(currentJaml);
            isFilterDirty = false;
            
            // Reload filters to refresh list but keep selection
            await loadFilters();
            
            // Update button state immediately
            updateSearchButton(searchButtonState); // Refreshes label
            
        } else if (action === 'clone') {
            let defaultName = 'Copy of ' + (extractFromJaml(currentJaml, 'name') || 'Filter');
            const newName = prompt("Enter name for new filter:", defaultName);
            if (!newName) return;

            const response = await fetch('/filters/clone', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    filterId: selectedFilterFilePath || 'new', // If new, this might need handling, but logic above implies we have a path. For brand new, we need a create endpoint or treat as clone of template? 
                    // Actually API /filters/clone requires a source filterId. 
                    // If we are "brand new" (no filePath), we can't use /clone easily unless we have a "create" endpoint.
                    // Fallback: If no filePath, we probably shouldn't have reached here via "saveDirtyFilter" logic usually implies editing EXISTING.
                    // But if we did:
                    filterId: selectedFilterFilePath,
                    newName: newName
                })
            });

            if (!response.ok) {
                const err = await response.json();
                throw new Error(err.error || 'Clone failed');
            }

            const data = await response.json();
            showStatus(`Saved copy: ${data.name}`);

            // Update editor to the NEW file (implicit switch)
            selectedFilterFilePath = data.filePath;
            selectedFilterBaseHash = computeJamlHash(data.filterJaml); // The server might have modified it (name update)
            setJamlValue(data.filterJaml); // Update editor with server version
            isFilterDirty = false;

            await loadFilters();
        }
    } catch (e) {
        alert(`Error saving: ${e.message}`);
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

    splitter.addEventListener('mousedown', (e) => {
        isDragging = true;
        document.body.style.cursor = 'col-resize';
        splitter.classList.add('active');
    });

    document.addEventListener('mousemove', (e) => {
        if (!isDragging) return;

        const containerRect = container.getBoundingClientRect();
        const containerWidth = containerRect.width;
        let newLeftWidth = e.clientX - containerRect.left;

        // Min/Max constraints (percentage or pixels)
        const minWidth = 300;
        const maxWidth = containerWidth - 300;

        if (newLeftWidth < minWidth) newLeftWidth = minWidth;
        if (newLeftWidth > maxWidth) newLeftWidth = maxWidth;

        // Use percentage for responsiveness
        const widthPercent = (newLeftWidth / containerWidth) * 100;
        
        leftPanel.style.flex = `0 0 ${widthPercent}%`;
        // rightPanel automatically takes remaining space due to flex: 1
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
}
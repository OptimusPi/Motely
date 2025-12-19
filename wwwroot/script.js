// ================================================
// JAML Web UI - JavaScript Functions
// ================================================

// Constants - Remove magic numbers
const SEED_COUNT_DEFAULT = 100000000;
const BATCH_SIZE_DEFAULT = 1000000;
const MAX_DISPLAY_ROWS = 1000;
const TAGLINE_ROTATION_INTERVAL = 5000;
const TAGLINE_FADE_DURATION = 300;
const COPY_BUTTON_RESET_DELAY = 2000;
const PROGRESS_PERCENT_MULTIPLIER = 100;
const SPEED_MILLION_THRESHOLD = 1000000;
const SPEED_THOUSAND_THRESHOLD = 1000;
const HASH_DJB2_SEED = 5381;
const MOBILE_BREAKPOINT = 768;
const PANEL_MIN_HEIGHT = 150;
const PANEL_MARGIN = 200;
const PANEL_MIN_WIDTH = 300;
const BLUEPRINT_SCALE = 0.85;
const BLUEPRINT_HEIGHT = 600;

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
let searchAborted = false;
let currentSearchId = null;
let currentSearchJaml = null; // The JAML content that started the current search
let currentBatchSize = BATCH_SIZE_DEFAULT;
let isProgrammaticEdit = false; // Flag to ignore programmatic setJamlValue calls
let totalSeedsSearched = 0;
let searchResults = [];
let searchColumns = ['seed', 'score'];
let savedFilters = [];
let sortColumn = 'score';
let sortDirection = 'desc'; // 'asc' or 'desc'
const maxRows = MAX_DISPLAY_ROWS; // Display limit message

let runningSearchIds = [];

let selectedFilterFilePath = null;
let selectedFilterBaseHash = null;
let isFilterDirty = false;

let seedSources = [];
let currentSeedSource = 'all';

let ws = null;
let wsDesiredSearchId = null;

// Centralized status message management
const StatusTypes = {
    INFO: 'info',
    SUCCESS: 'success', 
    ERROR: 'error',
    WARNING: 'warning',
    PROGRESS: 'progress'
};

class StatusManager {
    constructor() {
        this.statusElement = null;
        this.lastMessage = '';
        this.messageQueue = [];
        this.isProcessing = false;
    }

    init() {
        this.statusElement = document.getElementById('status');
    }

    show(message, type = StatusTypes.INFO) {
        if (!this.statusElement) {
            console.warn('Status element not found');
            return;
        }

        // Avoid duplicate messages
        if (message === this.lastMessage) {
            return;
        }

        this.lastMessage = message;
        
        // Add timestamp for errors and warnings
        const timestamp = (type === StatusTypes.ERROR || type === StatusTypes.WARNING) 
            ? `[${new Date().toLocaleTimeString()}] ` 
            : '';
        
        this.statusElement.textContent = timestamp + message;
        
        // Add visual feedback based on type
        this.statusElement.className = `status-${type}`;
    }

    // Convenience methods for common message types
    info(message) { this.show(message, StatusTypes.INFO); }
    success(message) { this.show(message, StatusTypes.SUCCESS); }
    error(message) { this.show(message, StatusTypes.ERROR); }
    warning(message) { this.show(message, StatusTypes.WARNING); }
    progress(message) { this.show(message, StatusTypes.PROGRESS); }

    // Batch status updates
    showProgress(searchId, threads, batch, speed, searched, found) {
        const sid = searchId || '';
        const threadStr = threads > 0 ? `${threads}T` : '';
        const speedStr = speed >= SPEED_MILLION_THRESHOLD
            ? `${(speed / SPEED_MILLION_THRESHOLD).toFixed(1)}M/s`
            : speed >= SPEED_THOUSAND_THRESHOLD
                ? `${(speed / SPEED_THOUSAND_THRESHOLD).toFixed(0)}K/s`
                : `${speed.toFixed(0)}/s`;
        
        this.progress(`${sid} | ${threadStr} | Batch ${batch} | ${speedStr} | ${(searched / SPEED_MILLION_THRESHOLD).toFixed(1)}M searched | ${found} found`);
    }

    showLoaded(filterName, isRunning = false) {
        if (isRunning) {
            this.info(`Loaded: ${filterName} (running)`);
        } else {
            this.info(`Loaded: ${filterName}`);
        }
    }

    showFilterChanged(hasSavedSelection = false, isDirty = false) {
        if (hasSavedSelection && isDirty) {
            this.warning('Filter changed - save or start new search');
        } else {
            this.info('Filter changed - ready to start new search');
        }
    }

    showSearchError(error) {
        this.error(`Search failed: ${error || 'unknown error'}`);
    }

    showSearchProgress(progress) {
        this.info(`Search progress: ${progress}%`);
    }

    showHalted(reason) {
        this.warning(`Search halted: ${reason || 'unknown'}`);
    }
}

// Global status manager instance
const statusManager = new StatusManager();

// Centralized event listener management to prevent memory leaks
class EventManager {
    constructor() {
        this.listeners = new Map();
        this.timers = new Set();
        this.intervals = new Set();
    }

    addListener(element, event, handler, options = {}) {
        if (!element) return null;
        
        const key = `${element.id || 'anonymous'}-${event}`;
        
        // Remove existing listener if present
        if (this.listeners.has(key)) {
            const existing = this.listeners.get(key);
            element.removeEventListener(event, existing.handler, existing.options);
        }
        
        // Add new listener
        element.addEventListener(event, handler, options);
        this.listeners.set(key, { element, event, handler, options });
        
        return key;
    }

    removeListener(key) {
        if (!this.listeners.has(key)) return false;
        
        const listener = this.listeners.get(key);
        listener.element.removeEventListener(listener.event, listener.handler, listener.options);
        this.listeners.delete(key);
        return true;
    }

    addTimer(callback, delay) {
        const timerId = setTimeout(callback, delay);
        this.timers.add(timerId);
        return timerId;
    }

    removeTimer(timerId) {
        if (this.timers.has(timerId)) {
            clearTimeout(timerId);
            this.timers.delete(timerId);
            return true;
        }
        return false;
    }

    addInterval(callback, delay) {
        const intervalId = setInterval(callback, delay);
        this.intervals.add(intervalId);
        return intervalId;
    }

    removeInterval(intervalId) {
        if (this.intervals.has(intervalId)) {
            clearInterval(intervalId);
            this.intervals.delete(intervalId);
            return true;
        }
        return false;
    }

    cleanup() {
        // Remove all event listeners
        for (const [key, listener] of this.listeners) {
            listener.element.removeEventListener(listener.event, listener.handler, listener.options);
        }
        this.listeners.clear();
        
        // Clear all timers
        for (const timerId of this.timers) {
            clearTimeout(timerId);
        }
        this.timers.clear();
        
        // Clear all intervals
        for (const intervalId of this.intervals) {
            clearInterval(intervalId);
        }
        this.intervals.clear();
    }
}

// Global event manager instance
const eventManager = new EventManager();

// Legacy showStatus function for backward compatibility
function showStatus(message) {
    statusManager.info(message);
}

function formatJaml() {
    const jaml = getJamlValue();
    if (!jaml.trim()) {
        statusManager.info('Nothing to format');
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
    // Prevent duplicate connections - check if we already have a healthy connection
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) {
        return;
    }

    // Close any existing dead connection before creating a new one
    if (ws && ws.readyState !== WebSocket.OPEN && ws.readyState !== WebSocket.CONNECTING) {
        ws.close();
        ws = null;
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
        // Clear desired search ID when connection closes to prevent stale subscriptions
        wsDesiredSearchId = null;
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
                        updateSearchButton('CONTINUE', (msg.progressPercent || 0) / PROGRESS_PERCENT_MULTIPLIER);
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
                searchButtonState = 'START';
                updateSearchButton('START', 0);
                statusManager.showSearchError(msg.error || 'unknown error');
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

                const speedStr = seedsPerSecond >= SPEED_MILLION_THRESHOLD
                    ? `${(seedsPerSecond / SPEED_MILLION_THRESHOLD).toFixed(1)}M/s`
                    : seedsPerSecond >= SPEED_THOUSAND_THRESHOLD
                        ? `${(seedsPerSecond / SPEED_THOUSAND_THRESHOLD).toFixed(0)}K/s`
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
                statusManager.showProgress(sid, threadsInUse, msg.currentBatch, seedsPerSecond, seedsSearched, foundCount);
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
                searchButtonState = 'CONTINUE';
                updateSearchButton('CONTINUE', 0);
                statusManager.showHalted(msg.reason || 'unknown');
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
    const filter = loadFilterByIdx(dropdown.value);
    
    if (!filter) return;
    
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
            const progress = (data.progressPercent || 0) / PROGRESS_PERCENT_MULTIPLIER;
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
async function loadUrlParametersAfterMonaco() {
    // Load URL parameters AFTER Monaco is initialized
    const urlParams = new URLSearchParams(window.location.search);
    const searchId = urlParams.get('search');
    const seedSource = urlParams.get('seedSource');
    
    // Load seed source from URL
    if (seedSource) {
        currentSeedSource = seedSource;
        // Update both dropdowns to reflect loaded seed source
        const settingsDropdown = document.getElementById('settingsSeedSource');
        const mainDropdown = document.getElementById('seedSource');
        if (settingsDropdown) settingsDropdown.value = seedSource;
        if (mainDropdown) mainDropdown.value = seedSource;
        showStatus(`Loaded seed source: ${seedSource}`);
    }
    
    // Load search ID and filter JAML from URL
    if (searchId) {
        currentSearchId = searchId;
        await checkExistingSearchStatus(searchId);
    }
}

// Monaco ready callback
window.onMonacoReady = async function() {
    await loadUrlParametersAfterMonaco();
};

// Initialize unified JAML editor functions after all functions are defined
function updateJamlEditorFunctions() {
    const textarea = document.getElementById('filterJaml');
    window.getJamlValue = () => {
        if (window.jamlEditor) return window.jamlEditor.getValue();
        return textarea ? textarea.value : '';
    };
    window.setJamlValue = (val) => {
        if (window.jamlEditor) {
            isProgrammaticEdit = true;
            window.jamlEditor.setValue(val || '');
            isProgrammaticEdit = false;
        } else if (textarea) {
            textarea.value = val || '';
        }
    };
}
updateJamlEditorFunctions();

function setEditorMode(mode) {
    const mono = document.getElementById('monacoEditor');
    const plain = document.getElementById('filterJaml');
    if (mode === 'monaco') {
        if (mono) mono.style.display = 'block';
        if (plain) plain.style.display = 'none';
        // If Monaco not yet created, it will be initialized by index.html loader; nothing else to do here
    } else {
        if (mono) mono.style.display = 'none';
        if (plain) plain.style.display = 'block';
        // Ensure get/set refer to textarea when in plain mode
        updateJamlEditorFunctions();
    }
}

document.addEventListener('DOMContentLoaded', async function() {
    // Initialize status manager first
    statusManager.init();
    
    // Load filters FIRST (so dropdown is populated)
    await loadFilters();
    await loadSeedSourcesForSettings(); // Load seed sources in settings
    syncMainSeedSourceDropdown(); // Sync main dropdown with settings
    
    // Initialize remaining UI components
    startTaglineRotation();
    ensureWebSocket();
    initPanelSplitter();
    
    // Load URL parameters immediately if Monaco is ready, otherwise callback will handle it
    if (window.jamlEditor) {
        await loadUrlParametersAfterMonaco();
    }
    
    // Add cleanup on page unload to prevent memory leaks
    eventManager.addListener(window, 'beforeunload', () => {
        eventManager.cleanup();
    });
    
    // Handle orientation changes for mobile
    eventManager.addListener(window, 'orientationchange', () => {
        // Force layout recalculation after orientation change
        setTimeout(() => {
            if (window.jamlEditor) {
                window.jamlEditor.layout();
            }
        }, 100);
    });
});

function startTaglineRotation() {
    const taglineElement = document.getElementById('jaml-tagline');
    if (!taglineElement) return;
    
    let currentIndex = 0;
    
    // Rotate tagline every TAGLINE_ROTATION_INTERVAL seconds
    const rotationInterval = eventManager.addInterval(() => {
        currentIndex = (currentIndex + 1) % jamlTaglines.length;
        taglineElement.style.opacity = '0';
        
        eventManager.addTimer(() => {
            taglineElement.textContent = jamlTaglines[currentIndex];
            taglineElement.style.opacity = '1';
        }, TAGLINE_FADE_DURATION);
    }, TAGLINE_ROTATION_INTERVAL);
    
    // Click to manually cycle
    eventManager.addListener(taglineElement, 'click', () => {
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
        return;
    }

    if (searchButtonState === 'CONTINUE') {
        const p = Math.round(pct * PROGRESS_PERCENT_MULTIPLIER);
        btn.textContent = p > 0 ? `Continue (${p}%)` : 'Continue';
        btn.className = 'button-primary';
        btn.disabled = false;
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
}

function toggleSearch() {
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

    if (searchButtonState === 'RUNNING') {
        showStatus('Search already running...');
        return;
    }

    searchButtonState = 'RUNNING';
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

        const requestBody = { filterJaml, seedCount: SEED_COUNT_DEFAULT };
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
            const errorMsg = ` Error: ${error.error}`;
            showStatus(errorMsg);
            console.error('Search failed:', error);
            alert(errorMsg); // Keep error visible
            searchButtonState = 'START';
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
        showStatus(` Network error: ${error.message}`);
        searchButtonState = 'START';
        updateSearchButton('START', 0);  // This also re-enables the button
    }
}

async function stopSearch() {
    // CRITICAL: Set STOPPING state FIRST to prevent race conditions
    // This must happen BEFORE we set searchButtonState = 'START'!
    updateSearchButton('STOPPING', 0);

    // Now set flags to stop polling loop
    searchButtonState = 'START';
    searchAborted = true;

    // DEBUG: Log current state
    console.log('stopSearch called - currentSearchId:', currentSearchId, 'runningSearchIds:', runningSearchIds);

    // Try to get searchId from running searches if currentSearchId is missing
    let searchIdToStop = currentSearchId;
    if (!searchIdToStop && runningSearchIds.length > 0) {
        searchIdToStop = runningSearchIds[0];
        console.log('Using fallback searchId from runningSearchIds:', searchIdToStop);
    }

    if (!searchIdToStop) {
        updateSearchButton('START', 0);
        showStatus('No search to stop');
        return;
    }

    try {
        const response = await fetch('/search/stop', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ searchId: searchIdToStop })
        });

        if (!response.ok) {
            const error = await response.json();
            showStatus(`Error stopping: ${error.error}`);
            updateSearchButton('START', 0);
            return;
        }

        const data = await response.json();
        showStatus(`${data.message} - ${searchResults.length} results`);

        // Remove from running searches
        runningSearchIds = runningSearchIds.filter(id => id !== searchIdToStop);
        
        // Clear currentSearchId if it was the one we stopped
        if (currentSearchId === searchIdToStop) {
            currentSearchId = null;
        }

        // Get current progress from the API to show accurate state
        const statusResponse = await fetch(`/search?id=${searchIdToStop}`);
        if (statusResponse.ok) {
            const statusData = await statusResponse.json();
            const progress = statusData.progressPercent || 0;
            updateSearchButton('CONTINUE', progress / PROGRESS_PERCENT_MULTIPLIER);

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
        console.error('Failed to stop all searches:', e);
        showStatus(`Error: ${e?.message || 'Failed to stop all searches'}`);
        updateSearchButton('START', 0);
    }
}

// Sync main seed source dropdown from settings on initial load
function syncMainSeedSourceDropdown() {
    const settingsDropdown = document.getElementById('settingsSeedSource');
    const mainDropdown = document.getElementById('seedSource');
    if (settingsDropdown && mainDropdown) {
        mainDropdown.innerHTML = settingsDropdown.innerHTML;
        mainDropdown.value = settingsDropdown.value;
        currentSeedSource = mainDropdown.value;
    }
}

function onSeedSourceChange() {
    const mainDropdown = document.getElementById('seedSource');
    const settingsDropdown = document.getElementById('settingsSeedSource');
    syncSeedSourceDropdowns(mainDropdown, settingsDropdown);

    // Update global state/UI for hydration
    isHydrationMode = currentSeedSource !== 'all' && currentSeedSource !== 'random:' + BATCH_SIZE_DEFAULT;
    updateSearchButton(searchButtonState);
    
    // Show/Hide Edit/New buttons
    const actionsDiv = document.getElementById('settingsWordListActions');
    const editBtn = document.getElementById('settingsEditWordListBtn');
    
    if (actionsDiv) {
        actionsDiv.style.display = 'flex'; // Always show actions row in modal
        // But only enable Edit if it's a text file
        const isTxt = currentSeedSource.startsWith('txt:');
        if (editBtn) editBtn.disabled = !isTxt;
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
    
    // Include seed source in share URL if set
    if (currentSeedSource && currentSeedSource !== 'all') {
        url.searchParams.set('seedSource', currentSeedSource);
    }

    navigator.clipboard.writeText(url.toString()).then(() => {
        const btn = document.getElementById('shareBtn');
        const originalText = btn.textContent;
        btn.textContent = '✅ Copied!';
        setTimeout(() => btn.textContent = originalText, COPY_BUTTON_RESET_DELAY);
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

    // Add result rows (show all MAX_DISPLAY_ROWS from API)
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

    if (data.results.length > MAX_DISPLAY_ROWS) {
        html += `<div class="info-text">Showing top ${MAX_DISPLAY_ROWS} of ${data.results.length} results</div>`;
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
        showStatus('Failed to load filters - server may not be running');
        // Try to populate dropdown with error message
        const dropdown = document.getElementById('savedSearches');
        if (dropdown) {
            dropdown.innerHTML = '<option value="">Server not running - check connection</option>';
        }
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
        showStatus('Failed to load seed sources - server may not be running');
        // Try to populate dropdown with error message
        if (select) {
            select.innerHTML = '<option value="">Server not running - check connection</option>';
        }
    }
}

function settingsSeedSourceChanged() {
    const settingsDropdown = document.getElementById('settingsSeedSource');
    const mainDropdown = document.getElementById('seedSource');
    syncSeedSourceDropdowns(settingsDropdown, mainDropdown);
    
    // Update global state/UI for hydration
    isHydrationMode = currentSeedSource !== 'all' && currentSeedSource !== 'random:' + BATCH_SIZE_DEFAULT;
    updateSearchButton(searchButtonState);
    
    // Show/Hide Edit/New buttons
    const actionsDiv = document.getElementById('settingsWordListActions');
    const editBtn = document.getElementById('settingsEditWordListBtn');
    
    if (actionsDiv) {
        actionsDiv.style.display = 'flex'; // Always show actions row in modal
        // But only enable Edit if it's a text file
        const isTxt = currentSeedSource.startsWith('txt:');
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

    // Check if we're in stacked (vertical) mode - only stack on mobile vertical
    function isStackedMode() {
        return window.innerWidth <= MOBILE_BREAKPOINT && window.innerHeight > window.innerWidth;
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
            const minHeight = PANEL_MIN_HEIGHT;
            const maxHeight = window.innerHeight - PANEL_MIN_HEIGHT;

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
            const minWidth = PANEL_MIN_WIDTH;
            const maxWidth = containerWidth - PANEL_MIN_WIDTH;

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

// ================================================
// Settings Modal Functions
// ================================================
function handleSettingsModalBackdrop(event) {
    // Close modal when clicking backdrop
    if (event.target.id === 'settingsModal') {
        closeSettingsModal();
    }
}

function renameSelectedFilterFromSettings() {
    showStatus('Rename filter not yet implemented');
}

function cloneSelectedFilterFromSettings() {
    showStatus('Clone filter not yet implemented');
}

// ================================================
// Fast Seed Analyzer with iframe
// ================================================
function quickAnalyze(seed) {
    // Switch to Analyze tab
    document.querySelector('.tab:nth-child(2)').click();
    
    const analyzeResult = document.getElementById('analyzeResult');
    
    // Show loading immediately with seed info
    analyzeResult.innerHTML = `
        <div style="padding: 20px; color: #fff;">
            <h3>Seed: ${seed}</h3>
            <div style="margin-top: 15px;">
                <p><strong>Loading Blueprint analyzer...</strong></p>
                <p style="font-size: 14px; color: #888; margin-top: 5px;">Score: ${searchResults.find(r => r.seed === seed)?.score || 'N/A'}</p>
            </div>
            <div style="margin-top: 20px; border: 1px solid #565b5c; border-radius: 8px; overflow: hidden;">
                <iframe 
                    src="https://miaklwalker.github.io/Blueprint/?seed=${seed}" 
                    style="width: 100%; height: 600px; border: none; transform: scale(0.85); transform-origin: top left;"
                    onload="this.style.opacity='1'"
                    onerror="this.parentElement.innerHTML='<div style=\\'padding: 40px; text-align: center; color: #888;\\'>Failed to load Blueprint. <a href=\\\"https://miaklwalker.github.io/Blueprint/?seed=${seed}\\\" target=\\\"_blank\\\" style=\\\"color: #4a9eff;\\\">Open in new tab ↗</a></div>'">
                </iframe>
            </div>
        </div>
    `;
}

// ================================================
// Export Results
// ================================================
function exportResults() {
    if (!searchResults || searchResults.length === 0) {
        showStatus('No results to export');
        return;
    }
    
    // Build CSV from results
    const headers = Object.keys(searchResults[0]);
    const csv = [
        headers.join(','),
        ...searchResults.map(r => headers.map(h => r[h] ?? '').join(','))
    ].join('\n');
    
    // Download
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `motely_results_${Date.now()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    
    showStatus(`Exported ${searchResults.length} results`);
}

// ================================================
// Missing Functions Polyfill & UI Fixes
// ================================================

function syncSeedSourceDropdowns(source, target) {
    if (!source || !target) return;
    
    // If options don't match, copy them
    if (source.innerHTML !== target.innerHTML) {
        target.innerHTML = source.innerHTML;
    }
    
    // Sync value
    if (source.value !== target.value) {
        target.value = source.value;
    }
    
    currentSeedSource = source.value;
}

function loadFilterByIdx(idx) {
    if (idx === '' || idx === null || idx === undefined) return null;
    const i = parseInt(idx);
    if (isNaN(i) || i < 0 || i >= savedFilters.length) return null;
    
    const filter = savedFilters[i];
    selectedFilterFilePath = filter.filePath;
    
    // Load JAML content
    if (filter.jaml) {
        isProgrammaticEdit = true;
        setJamlValue(filter.jaml);
        isProgrammaticEdit = false;
        selectedFilterBaseHash = computeJamlHash(filter.jaml);
        isFilterDirty = false;
    } else {
        // Fetch if not inline
        fetch(`/filters/${encodeURIComponent(filter.filePath)}`)
            .then(r => r.json())
            .then(data => {
                if (data.jaml) {
                    isProgrammaticEdit = true;
                    setJamlValue(data.jaml);
                    isProgrammaticEdit = false;
                    selectedFilterBaseHash = computeJamlHash(data.jaml);
                    isFilterDirty = false;
                }
            })
            .catch(e => console.error('Failed to load filter content', e));
    }
    
    return filter;
}

function loadSavedSearch() {
    const dropdown = document.getElementById('savedSearches');
    if (!dropdown) return;
    
    const idx = dropdown.value;
    if (idx === '') return;
    
    const filter = loadFilterByIdx(idx);
    if (!filter) return;
    
    if (filter.searchId) {
        currentSearchId = filter.searchId;
        updateUrlWithSearchId(filter.searchId);
        
        // Check status if it has a search ID
        checkExistingSearchStatus(filter.searchId);
    }
    
    showStatus(`Loaded: ${filter.name}`);
}

function updateUrlWithSearchId(searchId) {
    const url = new URL(window.location);
    if (searchId) {
        url.searchParams.set('search', searchId);
    } else {
        url.searchParams.delete('search');
    }
    
    if (currentSeedSource && currentSeedSource !== 'all') {
        url.searchParams.set('seedSource', currentSeedSource);
    } else {
        url.searchParams.delete('seedSource');
    }
    
    window.history.replaceState({}, '', url);
}

function computeJamlHash(jaml) {
    if (!jaml) return 0;
    let hash = 5381; // DJB2 seed
    for (let i = 0; i < jaml.length; i++) {
        hash = ((hash << 5) + hash) + jaml.charCodeAt(i);
    }
    return hash;
}

function subscribeToSearch(searchId) {
    if (!ws) {
        wsDesiredSearchId = searchId;
        ensureWebSocket();
        return;
    }
    
    if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type: 'subscribe', searchId: searchId }));
    } else {
        wsDesiredSearchId = searchId;
    }
}

function initSplitter() {
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
        e.preventDefault();
    });
    
    document.addEventListener('mousemove', (e) => {
        if (!isDragging) return;
        
        const containerRect = container.getBoundingClientRect();
        let newLeftWidth = e.clientX - containerRect.left;
        
        // Constraints (min 200px, max container width - 200px)
        const minWidth = 300;
        const maxWidth = containerRect.width - 300;
        
        if (newLeftWidth < minWidth) newLeftWidth = minWidth;
        if (newLeftWidth > maxWidth) newLeftWidth = maxWidth;
        
        const percentage = (newLeftWidth / containerRect.width) * 100;
        
        leftPanel.style.flex = `0 0 ${percentage}%`;
        leftPanel.style.width = `${percentage}%`;
        rightPanel.style.flex = `1 1 ${100 - percentage}%`;
        rightPanel.style.width = `${100 - percentage}%`;
    });
    
    document.addEventListener('mouseup', () => {
        if (isDragging) {
            isDragging = false;
            document.body.style.cursor = '';
            splitter.classList.remove('active');
        }
    });
}

// Initialize components
document.addEventListener('DOMContentLoaded', () => {
    initSplitter();
    
    // Also try to sync sources on load if they exist
    setTimeout(() => {
        const settingsDropdown = document.getElementById('settingsSeedSource');
        const mainDropdown = document.getElementById('seedSource');
        if (settingsDropdown && mainDropdown) {
             syncSeedSourceDropdowns(mainDropdown, settingsDropdown);
        }
    }, 1000);
});

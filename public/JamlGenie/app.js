// JamlGenie - Using SignalR Hub (like JAML WebUI does)
// Can be hosted separately (e.g., Cloudflare Pages) - just change apiBaseUrl to point to API server
// For Cloudflare Pages: Set API_BASE_URL environment variable, or use meta tag, or it will use window.location.origin
const apiBaseUrl = (() => {
    // Check for meta tag first (easiest to configure)
    const metaTag = document.querySelector('meta[name="api-base-url"]');
    if (metaTag && metaTag.content) {
        return metaTag.content;
    }
    // Check for global variable (set by Cloudflare Pages environment variable via build script)
    if (typeof window !== 'undefined' && window.API_BASE_URL) {
        return window.API_BASE_URL;
    }
    // Default: use same origin (works when frontend and backend are on same domain)
    return window.location.origin;
})();
let signalRConnection = null;
let currentSearchId = null;
let currentJaml = '';
let currentPrompt = ''; // Store original prompt for seed verification
let currentFilterName = ''; // Store current filter name
let originalFilterName = ''; // Store original filter name for comparison

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    console.log('JamlGenie: DOM loaded, setting up event listeners');
    
    // Check URL for shared search ID on page load
    const urlParams = new URLSearchParams(window.location.search);
    const sharedSearchId = urlParams.get('search');
    if (sharedSearchId) {
        loadSharedSearch(sharedSearchId);
    }
    
    // Set up event listeners
    const grantBtn = document.getElementById('grantBtn');
    const retryBtn = document.getElementById('retryBtn');
    const wishInput = document.getElementById('wishInput');
    
    if (!grantBtn) {
        console.error('JamlGenie: grantBtn not found!');
        return;
    }
    
    if (!wishInput) {
        console.error('JamlGenie: wishInput not found!');
        return;
    }
    
    grantBtn.addEventListener('click', () => {
        console.log('JamlGenie: Grant button clicked');
        grantWish();
    });
    
    if (retryBtn) {
        retryBtn.addEventListener('click', () => {
            console.log('JamlGenie: Retry button clicked');
            grantWish(); // Retry uses same prompt
        });
    }
    
    wishInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            console.log('JamlGenie: Enter key pressed');
            grantWish();
        }
    });
    
    // Filter management event listeners (only set up if elements exist)
    try {
        const filterNameInput = document.getElementById('filterNameInput');
        const saveFilterBtn = document.getElementById('saveFilterBtn');
        const saveAsNewBtn = document.getElementById('saveAsNewBtn');
        
        if (filterNameInput && saveFilterBtn && saveAsNewBtn) {
            filterNameInput.addEventListener('input', () => {
                const name = filterNameInput.value.trim();
                // Enable save buttons when name is entered
                saveFilterBtn.disabled = !name;
                saveAsNewBtn.disabled = !name;
                
                // If name changed from original, suggest "Save As New"
                if (name && name !== originalFilterName && originalFilterName) {
                    saveAsNewBtn.style.display = 'inline-block';
                    saveAsNewBtn.textContent = '≡ƒô¥ Save As New Filter';
                } else {
                    saveAsNewBtn.style.display = 'none';
                }
            });
            
            filterNameInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !saveFilterBtn.disabled) {
                    e.preventDefault();
                    saveFilterBtn.click();
                }
            });
            
            saveFilterBtn.addEventListener('click', () => saveFilter(false));
            saveAsNewBtn.addEventListener('click', () => saveFilter(true));
        }
    } catch (error) {
        console.warn('Failed to set up filter management:', error);
        // Don't break the app if filter management fails
    }
    
});

// Connect to SignalR Hub (simple pattern like JAML UI)
async function ensureSignalR() {
    if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
        return;
    }

    if (!signalRConnection) {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl(`${apiBaseUrl}/searchHub`)
            .withAutomaticReconnect()
            .build();
        
        signalRConnection.on('Result', (message) => {
            let resultData = typeof message === 'string' ? JSON.parse(message) : message;
            
            if (resultData.type === 'result' && resultData.searchId === currentSearchId) {
                setTrackerStep('searching', 'completed');
                setTrackerStep('found', 'active');
                const rawResult = resultData.result || {};
                const normalizedResult = {
                    seed: rawResult.seed || rawResult.Seed || '???',
                    score: rawResult.score || rawResult.Score || 0,
                    tallies: rawResult.tallies || rawResult.Tallies || []
                };
                setTimeout(() => showResult(normalizedResult, currentJaml), 500);
                } else if (resultData.type === 'progress' && resultData.searchId === currentSearchId) {
                    const detail = resultData.seedsPerSecond > 0
                        ? `${resultData.seedsSearched.toLocaleString()} seeds | ${resultData.seedsPerSecond.toFixed(0)}/sec | Found: ${resultData.seedsFound || 0}`
                        : `Searched ${resultData.seedsSearched.toLocaleString()} seeds...`;
                    updateTrackerStatus('The Genie is searching...', detail);
                } else if (resultData.type === 'search_completed' && resultData.searchId === currentSearchId) {
                    if (resultData.seedsFound === 0) {
                        setTrackerStep('searching', 'completed');
                        updateTrackerStatus('No seeds found', 'Try refining your wish');
                        document.getElementById('seedResult').textContent = 'No seeds found';
                        document.getElementById('jamlCode').textContent = currentJaml || 'No JAML';
                        document.getElementById('resultArea').classList.remove('hidden');
                        document.getElementById('retryBtn').classList.remove('hidden');
                        document.getElementById('analyzeLink').classList.add('hidden');
                    } else {
                        setTrackerStep('found', 'completed');
                        updateTrackerStatus('Search complete!', `Found ${resultData.seedsFound || 0} from ${resultData.seedsSearched.toLocaleString()} seeds`);
                    }
            } else if (resultData.type === 'search_failed' && resultData.searchId === currentSearchId) {
                updateTrackerStatus('Search failed', resultData.error || 'Unknown error');
            }
        });
        
        signalRConnection.onreconnected(() => {
            if (currentSearchId) {
                signalRConnection.invoke('JoinSearchGroup', currentSearchId).catch(() => {});
            }
        });
    }
    
    if (signalRConnection.state === signalR.HubConnectionState.Disconnected) {
        await signalRConnection.start();
        if (currentSearchId) {
            await signalRConnection.invoke('JoinSearchGroup', currentSearchId);
        }
    }
}

// Pizza Tracker Functions
function showTracker() {
    // Tracker is always visible now, just reset it
    document.getElementById('resultArea').classList.add('hidden');
    resetTracker();
}

function resetTracker() {
    document.querySelectorAll('.tracker-step').forEach(step => {
        step.classList.remove('active', 'completed', 'failed');
    });
    const statusText = document.getElementById('statusText');
    if (statusText) {
        statusText.classList.remove('seed-found');
    }
    const genieImg = document.querySelector('.genie-illustration');
    if (genieImg) {
        genieImg.classList.remove('genie-thinking', 'genie-plan', 'genie-searching', 'genie-found');
    }
    updateTrackerStatus('Make your wish...', '');
}

// Status messages for each step
const stepStatusMessages = {
    thinking: { text: 'The Genie is struggling...', detail: 'Crafting your wish into JAML' },
    plan: { text: 'The Genie has a plan!', detail: 'JAML filter generated' },
    searching: { text: 'The Genie is searching...', detail: 'Scanning seeds...' },
    found: { text: 'Your wish is granted!', detail: 'Seed found!' }
};

function updateTrackerStatus(text, detail) {
    const statusText = document.getElementById('statusText');
    const statusDetail = document.getElementById('statusDetail');
    if (statusText) statusText.textContent = text;
    if (statusDetail) statusDetail.textContent = detail;
}

function showPromptPipeline(steps) {
    const debugDiv = document.getElementById('promptDebug');
    if (!debugDiv || !steps) return;
    
    // C# uses PascalCase, but JSON might be camelCase - handle both
    const original = steps.Original || steps.original || '';
    const step1 = steps.AfterStep1 || steps.afterStep1 || '';
    const step2 = steps.AfterStep2 || steps.afterStep2 || '';
    const step3 = steps.AfterStep3 || steps.afterStep3 || '';
    const final = steps.Final || steps.final || '';
    
    document.getElementById('debugOriginal').textContent = original;
    document.getElementById('debugStep1').textContent = step1;
    document.getElementById('debugStep2').textContent = step2;
    document.getElementById('debugStep3').textContent = step3;
    document.getElementById('debugFinal').textContent = final;
    
    // JAML transformation steps
    const rawJaml = steps.RawJamlFromAI || steps.rawJamlFromAI || '';
    const cleanedJaml = steps.CleanedJaml || steps.cleanedJaml || '';
    const finalJaml = steps.FinalJaml || steps.finalJaml || '';
    const validationError = steps.ValidationError || steps.validationError || '';
    
    if (rawJaml || cleanedJaml || finalJaml) {
        document.getElementById('debugJamlRaw').textContent = rawJaml || '(not available)';
        document.getElementById('debugJamlCleaned').textContent = cleanedJaml || '(not available)';
        document.getElementById('debugJamlFinal').textContent = finalJaml || '(not available)';
    }
    
    if (validationError) {
        const errorDiv = document.getElementById('debugJamlError');
        const errorSpan = document.getElementById('debugValidationError');
        if (errorDiv && errorSpan) {
            errorSpan.textContent = validationError;
            errorDiv.style.display = 'block';
        }
    } else {
        const errorDiv = document.getElementById('debugJamlError');
        if (errorDiv) errorDiv.style.display = 'none';
    }
    
    debugDiv.style.display = 'block';
    console.log('Prompt Pipeline:', { original, step1, step2, step3, final, rawJaml, cleanedJaml, finalJaml, validationError });
}

function setTrackerStep(stepName, status) {
    const step = document.querySelector(`[data-step="${stepName}"]`);
    if (!step) {
        console.warn(`Tracker step not found: ${stepName}`);
        return;
    }
    
    const genieImg = document.querySelector('.genie-illustration');
    
    console.log(`Setting step ${stepName} to ${status}`);
    
    // Remove all states first
    step.classList.remove('active', 'completed', 'failed');
    
    if (status === 'active') {
        step.classList.add('active');
        // Update status bar text
        const msg = stepStatusMessages[stepName];
        if (msg) updateTrackerStatus(msg.text, msg.detail);
        
        // Morph genie based on step
        if (genieImg) {
            genieImg.classList.remove('genie-thinking', 'genie-plan', 'genie-searching', 'genie-found');
            genieImg.classList.add(`genie-${stepName}`);
        }
        
        // Force visibility
        step.style.opacity = '1';
        step.style.visibility = 'visible';
    } else if (status === 'completed') {
        step.classList.add('completed');
        // Keep it visible
        step.style.opacity = '1';
        step.style.visibility = 'visible';
    }
    
    // Force a repaint to ensure styles are applied
    step.offsetHeight; // Trigger reflow
}

async function grantWish() {
    console.log('JamlGenie: grantWish() called');
    const wishInput = document.getElementById('wishInput');
    if (!wishInput) {
        console.error('JamlGenie: wishInput not found in grantWish()');
        return;
    }
    
    const wish = wishInput.value.trim();
    console.log('JamlGenie: Wish text:', wish);
    if (!wish) {
        console.warn('JamlGenie: Empty wish, returning');
        return;
    }

    // Store prompt for seed verification
    currentPrompt = wish;
    
    wishInput.disabled = true;
    const grantBtn = document.getElementById('grantBtn');
    if (grantBtn) grantBtn.disabled = true;
    const retryBtn = document.getElementById('retryBtn');
    if (retryBtn) retryBtn.classList.add('hidden');
    
    console.log('JamlGenie: Showing tracker and making API call');
    showTracker();
    setTrackerStep('thinking', 'active');

    try {
        const response = await fetch(apiBaseUrl + '/mcp/prompt', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt: wish })
        });

        let data;
        const contentType = response.headers.get('content-type');
        if (contentType && contentType.includes('application/json')) {
            const text = await response.text();
            if (!text) {
                throw new Error('Empty response from server');
            }
            try {
                data = JSON.parse(text);
            } catch (e) {
                throw new Error(`Invalid JSON response: ${text.substring(0, 100)}`);
            }
        } else {
            const text = await response.text();
            throw new Error(`Server error: ${response.status} ${response.statusText} - ${text.substring(0, 200)}`);
        }

        if (!response.ok || !data.success) {
            // Show prompt pipeline and JAML transformation steps even on error
            if (data.refinementSteps) {
                showPromptPipeline(data.refinementSteps);
            }
            throw new Error(data.error || data.message || 'Genie failed to process your wish');
        }
        
        // Show prompt pipeline if available
        if (data.refinementSteps) {
            showPromptPipeline(data.refinementSteps);
        }
        
        // Mark thinking as completed and show plan step
        setTrackerStep('thinking', 'completed');
        await new Promise(resolve => setTimeout(resolve, 300)); // Small delay to show transition
        setTrackerStep('plan', 'active');
        
        currentSearchId = data.searchId;
        currentJaml = data.jamlFilter || '';
        
        // Extract filter name from JAML and populate filter name input
        extractAndSetFilterName(currentJaml);
        
        // Store search URL for linking to full JAML UI (available globally for showResult)
        // This URL shows the full search results table in JAML UI, not just the seed
        window.currentSearchUrl = data.searchUrl || (currentSearchId ? `${apiBaseUrl}/JAML/?search=${encodeURIComponent(currentSearchId)}` : null);
        
        // Add searchId to URL so it can be shared and works when hosted separately
        if (currentSearchId) {
            const url = new URL(window.location.href);
            url.searchParams.set('search', currentSearchId);
            window.history.pushState({}, '', url);
        }
        
        // Connect SignalR ONLY after we have a searchId
        await ensureSignalR();
        if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
            await signalRConnection.invoke('JoinSearchGroup', currentSearchId);
        }
        
        // Show plan completed and start searching
        await new Promise(resolve => setTimeout(resolve, 500));
        setTrackerStep('plan', 'completed');
        await new Promise(resolve => setTimeout(resolve, 300));
        setTrackerStep('searching', 'active');
        
        if (data.results && data.results.length > 0) {
            setTrackerStep('searching', 'completed');
            setTrackerStep('found', 'active');
            // Normalize result format - handle both camelCase and PascalCase
            const firstResult = data.results[0];
            if (firstResult) {
                const normalizedResult = {
                    seed: firstResult.seed || firstResult.Seed || firstResult.seedNumber || '',
                    score: firstResult.score || firstResult.Score || 0,
                    tallies: firstResult.tallies || firstResult.Tallies || []
                };
                // Ensure seed is never empty/undefined
                if (!normalizedResult.seed || normalizedResult.seed === 'undefined') {
                    normalizedResult.seed = '???';
                }
                setTimeout(async () => {
                    await showResult(normalizedResult, currentJaml);
                    // Verify seed matches original prompt
                    await verifySeed(normalizedResult.seed, currentPrompt);
                }, 500);
            }
        }
        
    } catch (error) {
        console.error('Genie error:', error);
        // Show error in tracker, but KEEP the step states so we know where it failed!
        // Don't remove active/completed - just mark the current active step as failed
        const activeStep = document.querySelector('.tracker-step.active');
        if (activeStep) {
            activeStep.classList.remove('active');
            activeStep.classList.add('failed');
        }
        updateTrackerStatus('The Genie failed', error.message || 'Genie is confused. Try again.');
        document.getElementById('resultArea').classList.add('hidden');
    } finally {
        document.getElementById('wishInput').disabled = false;
        document.getElementById('grantBtn').disabled = false;
    }
}

async function showResult(result, jaml) {
    if (!result) {
        // Show "no result" in tracker instead of hiding it
        document.querySelectorAll('.tracker-step').forEach(step => {
            step.classList.remove('active', 'completed');
        });
        updateTrackerStatus('No seed found', 'Try refining your wish');
        document.getElementById('resultArea').classList.add('hidden');
        document.getElementById('wishInput').focus();
        return;
    }
    
    // Normalize result to ensure consistent property access
    const normalizedResult = {
        seed: result.seed || result.Seed || result.SeedNumber || '',
        score: result.score || result.Score || 0,
        tallies: result.tallies || result.Tallies || []
    };
    
    // Build display text - ensure seed is never undefined
    const seed = normalizedResult.seed || '???';
    const score = normalizedResult.score || 0;
    const scoreText = score > 0 ? ` (Score: ${score})` : '';
    
    // Make seed clickable to copy
    const seedResult = document.getElementById('seedResult');
    seedResult.textContent = seed;
    seedResult.classList.add('clickable-seed');
    seedResult.title = 'Click to copy seed';
    seedResult.onclick = () => {
        navigator.clipboard.writeText(seed).then(() => {
            const original = seedResult.textContent;
            seedResult.textContent = 'Copied!';
            setTimeout(() => {
                seedResult.textContent = original;
            }, 1000);
        });
    };
    document.getElementById('jamlCode').textContent = jaml || 'No JAML generated';
    
    // Show analyzer link - prefer search URL (full results table) over just seed
    const analyzeLink = document.getElementById('analyzeLink');
    if (window.currentSearchUrl) {
        // Link to full search results in JAML UI (shows table with all results, filter, etc.)
        // This is better than just the seed - shows the full search context
        analyzeLink.href = window.currentSearchUrl;
        analyzeLink.textContent = '≡ƒöì View Full Search Results';
        analyzeLink.target = '_blank';
        analyzeLink.classList.remove('hidden');
    } else if (seed && seed !== '???' && seed !== 'undefined' && seed !== 'null') {
        // Fallback: Link to JAML analyzer with just the seed (opens in new tab)
        analyzeLink.href = `${apiBaseUrl}/JAML/?seed=${seed}`;
        analyzeLink.textContent = '≡ƒöì Analyze Seed';
        analyzeLink.target = '_blank';
        analyzeLink.classList.remove('hidden');
    } else {
        analyzeLink.classList.add('hidden');
    }
    
    // Show result - just the seed, no redundant text
    setTrackerStep('found', 'completed');
    const statusText = document.getElementById('statusText');
    if (statusText) {
        statusText.classList.add('seed-found');
        // Just show the seed, not redundant "Your wish is granted!" text
        statusText.textContent = seed + scoreText;
    }
    document.getElementById('resultArea').classList.remove('hidden');
    document.getElementById('retryBtn').classList.add('hidden'); // Hide retry when seed found
    
    // Enable filter management UI
    enableFilterManagement();
    
    document.getElementById('wishInput').focus();
}

// Extract filter name from JAML and set it in the input field
function extractAndSetFilterName(jaml) {
    if (!jaml) return;
    
    try {
        // Try to extract name from JAML (look for "name: ..." pattern)
        const nameMatch = jaml.match(/^name:\s*(.+)$/m);
        if (nameMatch && nameMatch[1]) {
            const name = nameMatch[1].trim().replace(/^["']|["']$/g, '');
            const filterNameInput = document.getElementById('filterNameInput');
            if (filterNameInput) {
                filterNameInput.value = name;
                currentFilterName = name;
                originalFilterName = name;
                
                // Enable save button if name exists
                const saveFilterBtn = document.getElementById('saveFilterBtn');
                if (saveFilterBtn) saveFilterBtn.disabled = !name;
            }
        }
    } catch (e) {
        console.warn('Failed to extract filter name:', e);
    }
}

// Enable filter management UI
function enableFilterManagement() {
    const filterNameInput = document.getElementById('filterNameInput');
    const saveFilterBtn = document.getElementById('saveFilterBtn');
    const saveAsNewBtn = document.getElementById('saveAsNewBtn');
    
    if (filterNameInput && currentJaml) {
        // Extract name if not already set
        if (!filterNameInput.value) {
            extractAndSetFilterName(currentJaml);
        }
        
        // Enable buttons if name exists
        const name = filterNameInput.value.trim();
        if (saveFilterBtn) saveFilterBtn.disabled = !name;
        if (saveAsNewBtn) saveAsNewBtn.disabled = !name;
    }
}

// Save filter (create new if name changed, or update existing)
// Toast notification system
function showToast(message, type = 'success', duration = 3000) {
    // Remove existing toast if any
    const existingToast = document.querySelector('.toast');
    if (existingToast) {
        existingToast.remove();
    }
    
    // Create toast element
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    
    // Set icon based on type
    const icon = type === 'success' ? 'Γ£à' : type === 'error' ? 'Γ¥î' : 'ΓÜá∩╕Å';
    toast.innerHTML = `
        <span class="toast-icon">${icon}</span>
        <span class="toast-message">${message}</span>
    `;
    
    // Add to page
    document.body.appendChild(toast);
    
    // Auto-remove after duration
    setTimeout(() => {
        toast.classList.add('hiding');
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    }, duration);
    
    // Allow clicking to dismiss
    toast.addEventListener('click', () => {
        toast.classList.add('hiding');
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    });
}

async function saveFilter(forceNew = false) {
    const filterNameInput = document.getElementById('filterNameInput');
    if (!filterNameInput || !currentJaml) {
        showToast('No filter to save', 'warning');
        return;
    }
    
    const newName = filterNameInput.value.trim();
    if (!newName) {
        showToast('Please enter a filter name', 'warning');
        return;
    }
    
    // Update JAML with new name
    let updatedJaml = currentJaml;
    const namePattern = /^name:\s*.+$/m;
    if (namePattern.test(updatedJaml)) {
        updatedJaml = updatedJaml.replace(namePattern, `name: ${newName}`);
    } else {
        // Add name at the beginning if it doesn't exist
        updatedJaml = `name: ${newName}\n${updatedJaml}`;
    }
    
    // Determine if we should create a new filter
    const nameChanged = newName !== originalFilterName;
    const shouldCreateNew = forceNew || (nameChanged && originalFilterName);
    
    try {
        // Disable buttons during save
        const saveFilterBtn = document.getElementById('saveFilterBtn');
        const saveAsNewBtn = document.getElementById('saveAsNewBtn');
        if (saveFilterBtn) saveFilterBtn.disabled = true;
        if (saveAsNewBtn) saveAsNewBtn.disabled = true;
        
        // Save filter via API
        const response = await fetch(`${apiBaseUrl}/filters/save`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                filterJaml: updatedJaml,
                filterId: shouldCreateNew ? null : currentSearchId, // null = create new
                createNew: shouldCreateNew
            })
        });
        
        if (!response.ok) {
            const error = await response.text();
            throw new Error(error || 'Failed to save filter');
        }
        
        const result = await response.json();
        
        // Update state
        currentJaml = updatedJaml;
        currentFilterName = newName;
        if (!shouldCreateNew) {
            originalFilterName = newName; // Update original if we're updating existing
        }
        
        // Show success message
        const saveBtn = shouldCreateNew ? saveAsNewBtn : saveFilterBtn;
        if (saveBtn) {
            const originalText = saveBtn.textContent;
            saveBtn.textContent = 'Γ£à Saved!';
            saveBtn.style.backgroundColor = 'rgba(6, 214, 160, 0.2)';
            saveBtn.style.borderColor = '#06d6a0';
            setTimeout(() => {
                saveBtn.textContent = originalText;
                saveBtn.style.backgroundColor = '';
                saveBtn.style.borderColor = '';
            }, 2000);
        }
        
        // Show toast notification instead of alert
        showToast(
            shouldCreateNew ? `Filter "${newName}" saved as new filter!` : `Filter "${newName}" saved!`,
            'success'
        );
        
    } catch (error) {
        console.error('Failed to save filter:', error);
        showToast(`Failed to save filter: ${error.message}`, 'error');
    } finally {
        // Re-enable buttons
        const saveFilterBtn = document.getElementById('saveFilterBtn');
        const saveAsNewBtn = document.getElementById('saveAsNewBtn');
        const filterNameInput = document.getElementById('filterNameInput');
        const name = filterNameInput ? filterNameInput.value.trim() : '';
        if (saveFilterBtn && name) saveFilterBtn.disabled = false;
        if (saveAsNewBtn && name) saveAsNewBtn.disabled = false;
    }
}

// Verify seed matches original prompt by analyzing it
async function verifySeed(seed, originalPrompt) {
    if (!seed || seed === '???' || seed === 'undefined' || seed === 'null' || !originalPrompt) {
        return; // Skip verification if no valid seed or prompt
    }
    
    try {
        // Call analyzer endpoint to get seed details
        const response = await fetch(`${apiBaseUrl}/analyze?seed=${encodeURIComponent(seed)}&deck=Red&stake=White`);
        if (!response.ok) {
            console.warn('Seed verification failed: Could not analyze seed');
            return;
        }
        
        const analysis = await response.text();
        
        // Simple verification: check if key items from prompt appear in analysis
        // Extract key terms from prompt (joker names, tarot names, etc.)
        const promptLower = originalPrompt.toLowerCase();
        const analysisLower = analysis.toLowerCase();
        
        // Check for common joker names mentioned in prompt
        const jokerNames = ['blueprint', 'brainstorm', 'perkeo', 'lucky cat', 'luckycat', 'wee', 'wee joker', 'weeJoker'];
        const foundItems = [];
        const missingItems = [];
        
        for (const jokerName of jokerNames) {
            if (promptLower.includes(jokerName)) {
                if (analysisLower.includes(jokerName.replace(/\s+/g, ''))) {
                    foundItems.push(jokerName);
                } else {
                    missingItems.push(jokerName);
                }
            }
        }
        
        // Log verification result (could be enhanced to show warning to user)
        if (missingItems.length > 0) {
            console.warn(`Seed verification: Missing items from prompt: ${missingItems.join(', ')}`);
            // Could show a warning message to user here if needed
        } else if (foundItems.length > 0) {
            console.log(`Seed verification: Found expected items: ${foundItems.join(', ')}`);
        }
    } catch (error) {
        console.warn('Seed verification error:', error);
        // Don't fail the UI if verification fails
    }
}

// Load a shared search by ID (for when searchId is in URL)
async function loadSharedSearch(searchId) {
    try {
        setTrackerStep('thinking', 'active');
        document.getElementById('resultArea').classList.add('hidden');
        
        const response = await fetch(`${apiBaseUrl}/search?id=${encodeURIComponent(searchId)}`);
        if (!response.ok) {
            throw new Error('Search not found');
        }
        
        const data = await response.json();
        currentSearchId = searchId;
        currentJaml = data.filterJaml || '';
        
        // Store search URL for linking to full JAML UI
        window.currentSearchUrl = `${apiBaseUrl}/JAML/?search=${encodeURIComponent(searchId)}`;
        
        // Show results if available
        if (data.results && data.results.length > 0) {
            const firstResult = data.results[0];
            const normalizedResult = {
                seed: firstResult.seed || firstResult.Seed || firstResult.seedNumber || '',
                score: firstResult.score || firstResult.Score || 0,
                tallies: firstResult.tallies || firstResult.Tallies || []
            };
            if (!normalizedResult.seed || normalizedResult.seed === 'undefined') {
                normalizedResult.seed = '???';
            }
            
            setTrackerStep('thinking', 'completed');
            setTrackerStep('plan', 'completed');
            setTrackerStep('searching', 'completed');
            setTrackerStep('found', 'active');
            setTimeout(() => showResult(normalizedResult, currentJaml), 500);
        } else {
            // Search is running or no results yet
            if (data.status === 'running') {
                setTrackerStep('thinking', 'completed');
                setTrackerStep('plan', 'completed');
                setTrackerStep('searching', 'active');
                await ensureSignalR();
                if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
                    await signalRConnection.invoke('JoinSearchGroup', searchId);
                }
            } else {
                // Search completed but no results
                setTrackerStep('thinking', 'completed');
                setTrackerStep('plan', 'completed');
                setTrackerStep('searching', 'completed');
                updateTrackerStatus('No seeds found', 'Try refining your wish');
                document.getElementById('seedResult').textContent = 'No seeds found';
                document.getElementById('jamlCode').textContent = currentJaml || 'No JAML';
                document.getElementById('resultArea').classList.remove('hidden');
                document.getElementById('retryBtn').classList.remove('hidden');
                document.getElementById('analyzeLink').classList.add('hidden');
            }
        }
    } catch (error) {
        console.error('Failed to load shared search:', error);
        document.querySelectorAll('.tracker-step').forEach(step => {
            step.classList.remove('active', 'completed');
        });
        updateTrackerStatus('Failed to load search', error.message);
        document.getElementById('seedResult').textContent = 'Error';
        document.getElementById('jamlCode').textContent = `Failed to load search: ${error.message}`;
        document.getElementById('resultArea').classList.remove('hidden');
    }
}


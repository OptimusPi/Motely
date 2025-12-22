// JamlGenie - Using SignalR Hub (like JAML WebUI does)
const apiBaseUrl = window.location.origin;
let signalRConnection = null;
let currentSearchId = null;
let currentJaml = '';

// Initialize
document.getElementById('grantBtn').addEventListener('click', grantWish);
document.getElementById('wishInput').addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
        e.preventDefault();
        grantWish();
    }
});

// Connect to SignalR Hub (same as JAML WebUI)
async function ensureSignalR() {
    // Don't reconnect if already connected
    if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) return;
    
    // Wait if connection is in progress
    if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connecting) {
        await new Promise(resolve => {
            const checkState = setInterval(() => {
                if (signalRConnection.state === signalR.HubConnectionState.Connected) {
                    clearInterval(checkState);
                    resolve();
                } else if (signalRConnection.state === signalR.HubConnectionState.Disconnected) {
                    clearInterval(checkState);
                    signalRConnection = null; // Reset on disconnect
                    resolve();
                }
            }, 100);
            setTimeout(() => clearInterval(checkState), 5000); // Timeout after 5s
        });
        if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) return;
    }
    
    if (!signalRConnection) {
        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/search')
            .build();
        
        signalRConnection.on('Result', (result, resultColumns) => {
            if (result.searchId === currentSearchId) {
                setTrackerStep('searching', 'completed');
                setTrackerStep('found', 'active');
                setTimeout(() => showResult(result.result || result, currentJaml), 500);
            }
        });
        
        signalRConnection.on('Snapshot', (snapshotResults, snapshotColumns) => {
            // Handle snapshot if needed
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

// Pizza Tracker Functions
function showTracker() {
    document.getElementById('tracker').classList.remove('hidden');
    document.getElementById('resultArea').classList.add('hidden');
    resetTracker();
}

function resetTracker() {
    document.querySelectorAll('.tracker-step').forEach(step => {
        step.classList.remove('active', 'completed');
    });
}

function setTrackerStep(stepName, status) {
    const step = document.querySelector(`[data-step="${stepName}"]`);
    if (!step) return;
    
    step.classList.remove('active', 'completed');
    if (status === 'active') {
        step.classList.add('active');
    } else if (status === 'completed') {
        step.classList.add('completed');
    }
}

async function grantWish() {
    const wish = document.getElementById('wishInput').value.trim();
    if (!wish) return;

    document.getElementById('wishInput').disabled = true;
    document.getElementById('grantBtn').disabled = true;
    
    showTracker();
    setTrackerStep('thinking', 'active');

    try {
        const response = await fetch(apiBaseUrl + '/mcp/prompt', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt: wish })
        });

        const data = await response.json();

        if (!response.ok || !data.success) {
            throw new Error(data.error || data.message || 'Genie failed to process your wish');
        }
        
        setTrackerStep('thinking', 'completed');
        setTrackerStep('plan', 'active');
        
        currentSearchId = data.searchId;
        currentJaml = data.jamlFilter || '';
        
        // Connect SignalR ONLY after we have a searchId
        await ensureSignalR();
        if (signalRConnection && signalRConnection.state === signalR.HubConnectionState.Connected) {
            await signalRConnection.invoke('JoinSearchGroup', currentSearchId);
        }
        
        setTimeout(() => {
            setTrackerStep('plan', 'completed');
            setTrackerStep('searching', 'active');
        }, 500);
        
        if (data.results && data.results.length > 0) {
            setTrackerStep('searching', 'completed');
            setTrackerStep('found', 'active');
            setTimeout(() => showResult(data.results[0], currentJaml), 500);
        }
        
    } catch (error) {
        console.error('Genie error:', error);
        document.getElementById('tracker').classList.add('hidden');
        document.getElementById('seedResult').textContent = 'Error';
        document.getElementById('jamlCode').textContent = error.message || 'Genie is confused. Try again.';
        document.getElementById('resultArea').classList.remove('hidden');
    } finally {
        document.getElementById('wishInput').disabled = false;
        document.getElementById('grantBtn').disabled = false;
    }
}

function showResult(result, jaml) {
    currentSearchId = null;
    const seed = result?.Seed || result?.seed || result?.SeedNumber || '???';
    document.getElementById('seedResult').textContent = seed;
    document.getElementById('jamlCode').textContent = jaml || 'No JAML generated';
    document.getElementById('tracker').classList.add('hidden');
    document.getElementById('resultArea').classList.remove('hidden');
    document.getElementById('wishInput').focus();
}


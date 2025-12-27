// JamlGenie - Local Test Version
// Change this to test different endpoints:

// Local Wrangler dev server (run: wrangler dev in worker/ folder)
const GENIE_API = 'http://localhost:8787';

// Or use existing balatrogenie.app
// const GENIE_API = 'https://balatrogenie.app/generate';

// Or your deployed worker
// const GENIE_API = 'https://jamlgenie.YOUR-SUBDOMAIN.workers.dev';

const promptInput = document.getElementById('promptInput');
const generateBtn = document.getElementById('generateBtn');
const result = document.getElementById('result');
const jsonOutput = document.getElementById('jsonOutput');
const status = document.getElementById('status');
const copyBtn = document.getElementById('copyBtn');
const downloadBtn = document.getElementById('downloadBtn');
const newBtn = document.getElementById('newBtn');
const consentModal = document.getElementById('consentModal');
const consentAllow = document.getElementById('consentAllow');
const consentDeny = document.getElementById('consentDeny');
const apiUrl = document.getElementById('apiUrl');

// Show current API URL
apiUrl.textContent = GENIE_API;

let recognition = null;
let isListening = false;

function initSpeech() {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) return;
    
    recognition = new SpeechRecognition();
    recognition.continuous = false;
    recognition.interimResults = false;
    recognition.lang = 'en-US';
    
    recognition.onresult = (e) => {
        promptInput.value = e.results[0][0].transcript;
        generate();
    };
    
    recognition.onerror = (e) => {
        if (e.error === 'not-allowed') {
            showStatus('⚠️ Mic blocked', 'error');
        }
        isListening = false;
        generateBtn.textContent = '✨';
    };
    
    recognition.onend = () => {
        isListening = false;
        generateBtn.textContent = '✨';
    };
}

generateBtn.addEventListener('click', (e) => {
    e.preventDefault();
    if (isListening) {
        recognition?.stop();
        return;
    }
    
    if (!recognition) {
        showStatus('🎤 Voice not supported', 'info');
        return;
    }
    
    const consent = localStorage.getItem('jamlgenie_mic_consent');
    if (!consent) {
        consentModal.classList.remove('hidden');
        return;
    }
    
    startVoice();
});

function startVoice() {
    try {
        recognition.start();
        isListening = true;
        generateBtn.textContent = '🎤';
    } catch (e) {
        showStatus('⚠️ Mic error', 'error');
    }
}

consentAllow.addEventListener('click', () => {
    localStorage.setItem('jamlgenie_mic_consent', 'true');
    consentModal.classList.add('hidden');
    startVoice();
});

consentDeny.addEventListener('click', () => {
    consentModal.classList.add('hidden');
});

async function generate() {
    const prompt = promptInput.value.trim();
    if (!prompt) return;
    
    generateBtn.disabled = true;
    generateBtn.textContent = '⏳';
    result.classList.add('hidden');
    showStatus('✨', 'info');
    
    try {
        const res = await fetch(GENIE_API, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ prompt })
        });
        
        const data = await res.json();
        
        if (data.success && data.config) {
            jsonOutput.textContent = JSON.stringify(data.config, null, 2);
            result.classList.remove('hidden');
            showStatus('✅', 'success');
        } else {
            throw new Error(data.error || 'Failed');
        }
    } catch (e) {
        showStatus(`❌ ${e.message}`, 'error');
        console.error('Error:', e);
    } finally {
        generateBtn.disabled = false;
        generateBtn.textContent = '✨';
    }
}

promptInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        generate();
    }
});

copyBtn.addEventListener('click', () => {
    navigator.clipboard.writeText(jsonOutput.textContent);
    showStatus('📋 Copied', 'success');
});

downloadBtn.addEventListener('click', () => {
    const blob = new Blob([jsonOutput.textContent], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'filter.json';
    a.click();
    URL.revokeObjectURL(url);
    showStatus('⬇️ Downloaded', 'success');
});

newBtn.addEventListener('click', () => {
    promptInput.value = '';
    result.classList.add('hidden');
    promptInput.focus();
});

function showStatus(msg, type) {
    status.textContent = msg;
    status.className = `status ${type}`;
    status.classList.remove('hidden');
    if (type !== 'error') {
        setTimeout(() => status.classList.add('hidden'), 2000);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    initSpeech();
    promptInput.focus();
});

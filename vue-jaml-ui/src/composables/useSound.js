import { ref } from 'vue'

// Sound system using Web Audio API for click/clack sounds
let audioContext = null
let soundEnabled = ref(false)

// Initialize audio context (must be triggered by user interaction)
function initAudioContext() {
  if (!audioContext) {
    try {
      audioContext = new (window.AudioContext || window.webkitAudioContext)()
    } catch (e) {
      console.warn('Web Audio API not supported:', e)
      return false
    }
  }
  return true
}

// Check if sound is enabled from localStorage
function checkSoundEnabled() {
  try {
    return localStorage.getItem('jaml-ui-sound-enabled') === 'true'
  } catch {
    return false
  }
}

// Initialize sound system
soundEnabled.value = checkSoundEnabled()

// Watch for changes in localStorage (from index.html toggle)
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (e) => {
    if (e.key === 'jaml-ui-sound-enabled') {
      soundEnabled.value = e.newValue === 'true'
    }
  })
  
  // Also check periodically in case same-window changes
  setInterval(() => {
    const current = checkSoundEnabled()
    if (current !== soundEnabled.value) {
      soundEnabled.value = current
    }
  }, 500)
}

// Generate a click/clack sound
function playClickSound(type = 'click') {
  if (!soundEnabled.value) return
  
  if (!initAudioContext()) return
  
  try {
    const now = audioContext.currentTime
    const oscillator = audioContext.createOscillator()
    const gainNode = audioContext.createGain()
    
    oscillator.connect(gainNode)
    gainNode.connect(audioContext.destination)
    
    // Different frequencies for different sound types
    const frequencies = {
      click: 1200,      // High click
      clack: 800,       // Medium clack
      snap: 1500,       // Sharp snap
      pop: 600,         // Low pop
      tick: 1000        // Medium tick
    }
    
    oscillator.frequency.value = frequencies[type] || frequencies.click
    oscillator.type = 'sine'
    
    // Quick attack and decay for click/clack sound
    gainNode.gain.setValueAtTime(0, now)
    gainNode.gain.linearRampToValueAtTime(0.15, now + 0.001) // Quick attack
    gainNode.gain.exponentialRampToValueAtTime(0.01, now + 0.05) // Quick decay
    
    oscillator.start(now)
    oscillator.stop(now + 0.05)
  } catch (e) {
    // Silently fail if audio context is not available
    console.debug('Could not play sound:', e)
  }
}

// Play a double click (for panel creation)
function playDoubleClick() {
  playClickSound('click')
  setTimeout(() => playClickSound('clack'), 30)
}

// Play a snap (for panel destruction)
function playSnap() {
  playClickSound('snap')
}

// Play a tick (for resize)
function playTick() {
  playClickSound('tick')
}

// Play a pop (for collapse/expand)
function playPop() {
  playClickSound('pop')
}

export function useSound() {
  return {
    soundEnabled,
    playClickSound,
    playDoubleClick,
    playSnap,
    playTick,
    playPop
  }
}

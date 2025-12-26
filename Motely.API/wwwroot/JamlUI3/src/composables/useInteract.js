import { ref, onUnmounted } from 'vue'

// Singleton: Load interact.js once, reuse everywhere
let interactPromise = null
let interactInstance = null

const loadInteract = async () => {
  if (interactInstance) return interactInstance
  
  if (!interactPromise) {
    interactPromise = import('interactjs').then(module => {
      const interact = module.default || module
      if (typeof interact !== 'function') {
        throw new Error('interact.js not loaded correctly')
      }
      interactInstance = interact
      return interactInstance
    })
  }
  
  return interactPromise
}

/**
 * Composable for drag functionality using interact.js
 * Loads interact.js once and provides reusable drag handlers
 */
export function useInteract() {
  const isReady = ref(false)
  const error = ref(null)
  
  // Initialize interact.js
  const init = async () => {
    try {
      await loadInteract()
      isReady.value = true
      error.value = null
    } catch (err) {
      console.error('Failed to load interact.js:', err)
      error.value = err
      isReady.value = false
    }
  }
  
  /**
   * Create a draggable element
   * @param {HTMLElement} element - Element to make draggable
   * @param {Object} options - Drag options
   * @returns {Function} Cleanup function
   */
  const makeDraggable = async (element, options = {}) => {
    if (!isReady.value) {
      await init()
    }
    
    if (!interactInstance || !element) {
      console.warn('interact.js not ready or element missing')
      return () => {}
    }
    
    const {
      axis = 'y',
      onStart = () => {},
      onMove = () => {},
      onEnd = () => {},
      ...restOptions
    } = options
    
    const instance = interactInstance(element).draggable({
      axis,
      listeners: {
        start(event) {
          if (document.body) {
            document.body.style.cursor = axis === 'x' ? 'col-resize' : 'ns-resize'
            document.body.style.userSelect = 'none'
          }
          onStart(event)
        },
        move(event) {
          onMove(event)
        },
        end(event) {
          if (document.body) {
            document.body.style.cursor = ''
            document.body.style.userSelect = ''
          }
          onEnd(event)
        }
      },
      ...restOptions
    })
    
    // Return cleanup function
    return () => {
      if (instance) {
        instance.unset()
      }
    }
  }
  
  return {
    isReady,
    error,
    init,
    makeDraggable,
    interact: () => interactInstance
  }
}


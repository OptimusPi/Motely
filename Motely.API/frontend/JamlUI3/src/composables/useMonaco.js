import { ref, onUnmounted } from 'vue'

// Singleton: Load Monaco once, reuse everywhere
let monacoPromise = null
let monacoInstance = null

const loadMonaco = async () => {
  if (monacoInstance) return monacoInstance
  
  if (!monacoPromise) {
    monacoPromise = import('monaco-editor').then(module => {
      monacoInstance = module
      return monacoInstance
    })
  }
  
  return monacoPromise
}

/**
 * Composable for Monaco Editor
 * Loads Monaco once and provides reusable editor creation
 */
export function useMonaco() {
  const isReady = ref(false)
  const error = ref(null)
  
  // Initialize Monaco
  const init = async () => {
    try {
      await loadMonaco()
      isReady.value = true
      error.value = null
    } catch (err) {
      console.error('Failed to load Monaco Editor:', err)
      error.value = err
      isReady.value = false
    }
  }
  
  /**
   * Create a Monaco editor instance
   * @param {HTMLElement} container - Container element
   * @param {Object} options - Editor options
   * @returns {Object} Editor instance and cleanup function
   */
  const createEditor = async (container, options = {}) => {
    if (!isReady.value) {
      await init()
    }
    
    if (!monacoInstance || !container) {
      throw new Error('Monaco not ready or container missing')
    }
    
    const defaultOptions = {
      value: '',
      language: 'yaml',
      theme: 'vs-dark',
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 16,
      fontFamily: "'Lucida Console', 'Consolas', monospace",
      lineHeight: 24,
      tabSize: 2,
      wordWrap: 'on',
      scrollBeyondLastLine: false
    }
    
    const editor = monacoInstance.editor.create(container, {
      ...defaultOptions,
      ...options
    })
    
    // Handle resize
    const resizeObserver = new ResizeObserver(() => {
      editor.layout()
    })
    resizeObserver.observe(container)
    
    // Cleanup function
    const cleanup = () => {
      resizeObserver.disconnect()
      editor.dispose()
    }
    
    return {
      editor,
      cleanup,
      monaco: monacoInstance
    }
  }
  
  return {
    isReady,
    error,
    init,
    createEditor,
    monaco: () => monacoInstance
  }
}


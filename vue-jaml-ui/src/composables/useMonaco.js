import { ref } from 'vue'
import {
  clauseTypeOptions,
  deckOptions,
  stakeOptions
} from '../constants/jamlOptions.js'

// Singleton: Load Monaco once, reuse everywhere
let monacoPromise = null
let monacoInstance = null
let completionRegistered = false

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

const registerYamlCompletions = (monaco) => {
  if (completionRegistered || !monaco) return

  const topLevelKeys = [
    'name',
    'description',
    'author',
    'deck',
    'stake',
    'defaults',
    'must',
    'should',
    'mustNot'
  ]

  const valueSuggestions = [
    ...deckOptions,
    ...stakeOptions,
    ...clauseTypeOptions
  ]

  monaco.languages.registerCompletionItemProvider('yaml', {
    provideCompletionItems: (model, position) => {
      const word = model.getWordUntilPosition(position)
      const range = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        endColumn: word.endColumn
      }

      const keywordSuggestions = topLevelKeys.map((label) => ({
        label,
        kind: monaco.languages.CompletionItemKind.Keyword,
        insertText: `${label}: `,
        range
      }))

      const valueItems = valueSuggestions.map((label) => ({
        label,
        kind: monaco.languages.CompletionItemKind.Value,
        insertText: label,
        range
      }))

      return {
        suggestions: [...keywordSuggestions, ...valueItems]
      }
    }
  })

  completionRegistered = true
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
      const monaco = await loadMonaco()
      registerYamlCompletions(monaco)
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


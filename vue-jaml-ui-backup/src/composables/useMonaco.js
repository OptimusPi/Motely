import { ref } from 'vue'
import {
  clauseTypeOptions,
  deckOptions,
  stakeOptions
} from '../constants/jamlOptions.js'

// Singleton: Load Monaco once, reuse everywhere
let monacoPromise = null
let monacoInstance = null
let providersRegistered = false
let jamlSchema = null

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

const fetchSchema = async () => {
  if (jamlSchema) return jamlSchema
  try {
    // Respect Vite base (prod deploy is under /JAML/)
    const schemaUrl = `${import.meta.env.BASE_URL}jaml.schema.json`
    const response = await fetch(schemaUrl)
    jamlSchema = await response.json()
    return jamlSchema
  } catch (e) {
    console.warn('Failed to fetch JAML schema for Monaco:', e)
    return null
  }
}

const registerYamlProviders = (monaco, schema) => {
  if (providersRegistered || !monaco) return

  const topLevelKeys = schema?.properties ? Object.keys(schema.properties) : [
    'name', 'description', 'author', 'deck', 'stake', 'defaults', 'must', 'should', 'mustNot'
  ]

  const clauseKeys = schema?.definitions?.clause?.properties ? Object.keys(schema.definitions.clause.properties) : [
    'type', 'value', 'antes', 'score', 'label', 'edition', 'seal', 'enhancement', 'rank', 'suit', 'sources'
  ]

  const valueSuggestions = [
    ...deckOptions,
    ...stakeOptions,
    ...clauseTypeOptions
  ]

  // Completion Provider
  monaco.languages.registerCompletionItemProvider('yaml', {
    provideCompletionItems: (model, position) => {
      const word = model.getWordUntilPosition(position)
      const range = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        endColumn: word.endColumn
      }

      // Context-aware suggestions (very basic check for indentation)
      const lineContent = model.getLineContent(position.lineNumber)
      const isIndented = lineContent.startsWith('  ')

      const keys = isIndented ? clauseKeys : topLevelKeys
      
      const keywordSuggestions = keys.map((label) => {
        const prop = (isIndented ? schema?.definitions?.clause?.properties?.[label] : schema?.properties?.[label]) || {}
        return {
          label,
          kind: monaco.languages.CompletionItemKind.Keyword,
          insertText: `${label}: `,
          detail: prop.description || 'JAML Property',
          documentation: prop.enum ? `Options: ${prop.enum.join(', ')}` : undefined,
          range
        }
      })

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

  // Hover Provider
  monaco.languages.registerHoverProvider('yaml', {
    provideHover: (model, position) => {
      const word = model.getWordAtPosition(position)
      if (!word || !schema) return null

      const prop = schema.properties?.[word.word] || 
                   schema.definitions?.clause?.properties?.[word.word]
      
      if (prop && prop.description) {
        return {
          range: new monaco.Range(position.lineNumber, word.startColumn, position.lineNumber, word.endColumn),
          contents: [
            { value: `**${word.word}**` },
            { value: prop.description },
            prop.enum ? { value: `*Options:* ${prop.enum.join(', ')}` } : null
          ].filter(Boolean)
        }
      }
      return null
    }
  })

  providersRegistered = true
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
      const [monaco, schema] = await Promise.all([loadMonaco(), fetchSchema()])
      registerYamlProviders(monaco, schema)
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


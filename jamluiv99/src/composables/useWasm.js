import { ref, shallowRef } from 'vue'

/**
 * Composable for Motely WASM in-browser search engine.
 * Uses the published motely-wasm@1.1.0 npm package.
 * Gracefully degrades when WASM is unavailable (falls back to API mode).
 */

const isLoaded = ref(false)
const isLoading = ref(false)
const loadError = ref(null)
const capabilities = shallowRef(null)
const wasmApi = shallowRef(null)

// Active search polling intervals
const activePollers = new Map()

export function useWasm() {

  /**
   * Load the WASM module using the motely-wasm npm package.
   * The vite-plugin-motely-wasm handles serving _framework and COOP/COEP headers.
   */
  async function loadWasmModule() {
    if (isLoaded.value || isLoading.value) return isLoaded.value

    isLoading.value = true
    loadError.value = null

    try {
      // Dynamic import to avoid bundler issues if motely-wasm isn't installed
      const { loadMotely } = await import('motely-wasm')
      const api = await loadMotely()

      wasmApi.value = api
      capabilities.value = api.getCapabilities()

      isLoaded.value = true
      console.log('[useWasm] Motely WASM loaded:', capabilities.value)
      return true
    } catch (err) {
      loadError.value = err.message || 'Failed to load WASM module'
      console.warn('[useWasm] WASM load failed (will use API fallback):', err.message)
      return false
    } finally {
      isLoading.value = false
    }
  }

  /**
   * Validate JAML content using the WASM engine.
   */
  function validateJaml(jamlContent) {
    if (!wasmApi.value) return { valid: false, error: 'WASM not loaded' }
    try {
      return wasmApi.value.validateJaml(jamlContent)
    } catch (err) {
      return { valid: false, error: err.message }
    }
  }

  /**
   * Analyze a single seed.
   */
  function analyzeSeed(seed, deck = 'Red', stake = 'White') {
    if (!wasmApi.value) return { error: 'WASM not loaded' }
    try {
      return wasmApi.value.analyzeSeed(seed, deck, stake)
    } catch (err) {
      return { error: err.message }
    }
  }

  /**
   * Start a JAML search in the browser via WASM.
   * Returns { searchId } or throws.
   */
  function startSearch(jamlContent, options = {}) {
    if (!wasmApi.value) return { error: 'WASM not loaded' }
    try {
      const searchId = wasmApi.value.startJamlSearch(jamlContent, options)
      return { searchId }
    } catch (err) {
      return { error: err.message }
    }
  }

  /**
   * Get current status and results for an active search.
   */
  function getSearchStatus(searchId, resultLimit = 50) {
    if (!wasmApi.value) return { error: 'WASM not loaded' }
    try {
      return wasmApi.value.getSearchStatus(searchId, resultLimit)
    } catch (err) {
      return { error: err.message }
    }
  }

  /**
   * Stop an active search.
   */
  function stopSearch(searchId) {
    if (activePollers.has(searchId)) {
      clearInterval(activePollers.get(searchId))
      activePollers.delete(searchId)
    }
    if (!wasmApi.value) return
    try {
      wasmApi.value.stopSearch(searchId)
    } catch (err) {
      console.error('[useWasm] Failed to stop search:', err)
    }
  }

  /**
   * Dispose a completed/stopped search to free memory.
   */
  function disposeSearch(searchId) {
    stopSearch(searchId)
    if (!wasmApi.value) return
    try {
      wasmApi.value.disposeSearch(searchId)
    } catch (err) {
      console.error('[useWasm] Failed to dispose search:', err)
    }
  }

  /**
   * Start polling a search for progress updates.
   */
  function pollSearch(searchId, onUpdate, intervalMs = 500) {
    if (activePollers.has(searchId)) {
      clearInterval(activePollers.get(searchId))
    }

    const poller = setInterval(() => {
      const status = getSearchStatus(searchId)
      if (status.error) {
        clearInterval(poller)
        activePollers.delete(searchId)
        onUpdate({ ...status, isRunning: false })
        return
      }

      onUpdate(status)

      if (!status.isRunning) {
        clearInterval(poller)
        activePollers.delete(searchId)
      }
    }, intervalMs)

    activePollers.set(searchId, poller)
  }

  return {
    isLoaded,
    isLoading,
    loadError,
    capabilities,
    loadWasmModule,
    validateJaml,
    analyzeSeed,
    startSearch,
    getSearchStatus,
    stopSearch,
    disposeSearch,
    pollSearch,
  }
}

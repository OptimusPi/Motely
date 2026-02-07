import { ref } from 'vue'
import { useApi } from './useApi'
import { useWasm } from './useWasm'

export function useSearch() {
  const results = ref([])
  const columns = ref(['seed', 'score'])
  const searchStatus = ref('Ready')
  const isSearching = ref(false)
  const activeSearches = ref([])
  const currentSearchId = ref(null)
  const searchMode = ref('auto') // 'auto' | 'api' | 'wasm'
  const loading = ref(false)
  const error = ref(null)
  const { get, post, delete: del } = useApi()
  const { isLoaded: wasmLoaded, startSearch: wasmStartSearch, getSearchStatus: wasmGetStatus, stopSearch: wasmStopSearch, disposeSearch: wasmDispose, pollSearch: wasmPoll } = useWasm()

  const loadActiveSearches = async () => {
    loading.value = true
    error.value = null
    try {
      const data = await get('/searches')
      if (data?._fallback) {
        activeSearches.value = []
      } else {
        activeSearches.value = data.searches || data || []
      }
    } catch (e) {
      console.error('Failed to load active searches:', e)
      error.value = 'Failed to load searches'
      activeSearches.value = []
    } finally {
      loading.value = false
    }
  }

  /**
   * Start search via WASM (in-browser).
   */
  const startWasmSearch = (jaml) => {
    const result = wasmStartSearch(jaml, {})
    if (result.error) {
      searchStatus.value = `WASM Error: ${result.error}`
      return null
    }

    const searchId = result.searchId
    currentSearchId.value = searchId
    isSearching.value = true
    searchStatus.value = 'Running (browser WASM)...'

    // Add to active searches
    activeSearches.value.push({
      searchId,
      status: 'running',
      progress: 0,
      searched: 0,
      found: 0,
      speed: 0,
      mode: 'wasm'
    })

    // Poll for results
    let lastSeedsSearched = 0
    let lastPollTime = Date.now()

    wasmPoll(searchId, (status) => {
      if (status.error) {
        searchStatus.value = `Error: ${status.error}`
        isSearching.value = false
        return
      }

      // Calculate speed
      const now = Date.now()
      const elapsed = (now - lastPollTime) / 1000
      const speed = elapsed > 0 ? Math.round((status.totalSeedsSearched - lastSeedsSearched) / elapsed) : 0
      lastSeedsSearched = status.totalSeedsSearched
      lastPollTime = now

      // Update active search entry
      const idx = activeSearches.value.findIndex(s => s.searchId === searchId)
      if (idx >= 0) {
        activeSearches.value[idx] = {
          ...activeSearches.value[idx],
          searched: status.totalSeedsSearched,
          found: status.matchingSeeds,
          speed,
          status: status.isRunning ? 'running' : 'completed',
          progress: status.isRunning ? Math.min(99, Math.round(status.totalSeedsSearched / 42_949_672_96 * 100)) : 100
        }
      }

      // Merge new results
      if (status.results?.length) {
        const existingSeeds = new Set(results.value.map(r => r.seed))
        const newResults = status.results
          .filter(r => !existingSeeds.has(r.seed))
          .map(r => ({
            seed: r.seed,
            score: r.score,
            tallies: r.tallies || []
          }))
        if (newResults.length > 0) {
          results.value = [...results.value, ...newResults].sort((a, b) => b.score - a.score)
        }
      }

      searchStatus.value = status.isRunning
        ? `Searching (WASM): ${status.totalSeedsSearched.toLocaleString()} seeds, ${status.matchingSeeds} found, ${speed.toLocaleString()}/s`
        : `Complete: ${status.totalSeedsSearched.toLocaleString()} seeds searched, ${status.matchingSeeds} found`

      if (!status.isRunning) {
        isSearching.value = false
      }
    }, 500)

    return searchId
  }

  /**
   * Start search via API.
   */
  const startApiSearch = async (jaml) => {
    try {
      const data = await post('/search', {
        filterJaml: jaml,
        seedCount: 0,
        seedSource: 'all'
      })

      currentSearchId.value = data.searchId
      results.value = data.results || []
      columns.value = data.columns || columns.value
      isSearching.value = true
      searchStatus.value = 'Running (API)...'
      
      await loadActiveSearches()
      return data.searchId
    } catch (e) {
      // If API fails and WASM is available, fall back to WASM
      if (wasmLoaded.value) {
        console.warn('API search failed, falling back to WASM:', e.message)
        searchStatus.value = 'API unavailable, using browser WASM...'
        return startWasmSearch(jaml)
      }
      searchStatus.value = `Failed: ${e.message}`
      return null
    }
  }

  const startSearch = async (jaml) => {
    if (!jaml.trim()) {
      searchStatus.value = 'Enter a filter'
      return
    }

    // Route based on search mode
    if (searchMode.value === 'wasm' && wasmLoaded.value) {
      return startWasmSearch(jaml)
    }
    if (searchMode.value === 'api') {
      return startApiSearch(jaml)
    }
    // Auto: try API first, fall back to WASM
    return startApiSearch(jaml)
  }

  const stopAll = async () => {
    if (!currentSearchId.value) {
      isSearching.value = false
      searchStatus.value = 'No active search'
      return
    }

    // Check if it's a WASM search
    const activeSearch = activeSearches.value.find(s => s.searchId === currentSearchId.value)
    if (activeSearch?.mode === 'wasm') {
      wasmStopSearch(currentSearchId.value)
      isSearching.value = false
      searchStatus.value = 'Stopped'
      return
    }

    try {
      await post(`/search/${encodeURIComponent(currentSearchId.value)}/stop`)
      isSearching.value = false
      searchStatus.value = 'Stopped'
      await loadActiveSearches()
    } catch (e) {
      searchStatus.value = `Error: ${e.message}`
    }
  }

  const stopSearch = async (searchId) => {
    const activeSearch = activeSearches.value.find(s => s.searchId === searchId)
    if (activeSearch?.mode === 'wasm') {
      wasmStopSearch(searchId)
      activeSearch.status = 'stopped'
      return
    }

    try {
      await post(`/search/${encodeURIComponent(searchId)}/stop`)
      await loadActiveSearches()
    } catch (e) {
      console.error('Failed to stop search:', e)
    }
  }

  const clearResults = async () => {
    results.value = []
    searchStatus.value = 'Cleared'
  }

  const exportResults = () => {
    if (results.value.length === 0) return
    
    const headers = columns.value
    const csv = [
      headers.join(','),
      ...results.value.map(r => {
        const row = [r.seed, r.score]
        if (r.tallies) {
          r.tallies.forEach(t => row.push(t))
        }
        return row.join(',')
      })
    ].join('\n')
    
    const blob = new Blob([csv], { type: 'text/csv' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `results_${Date.now()}.csv`
    a.click()
    URL.revokeObjectURL(url)
  }

  return {
    results,
    columns,
    searchStatus,
    isSearching,
    activeSearches,
    currentSearchId,
    searchMode,
    loadActiveSearches,
    startSearch,
    stopAll,
    stopSearch,
    clearResults,
    exportResults
  }
}


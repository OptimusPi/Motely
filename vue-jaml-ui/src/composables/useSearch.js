import { ref } from 'vue'
import { useApi } from './useApi'

export function useSearch() {
  const results = ref([])
  const columns = ref(['seed', 'score'])
  const searchStatus = ref('Ready')
  const isSearching = ref(false)
  const activeSearches = ref([])
  const currentSearchId = ref(null)
  const loading = ref(false)
  const error = ref(null)
  const { get, post, delete: del } = useApi()

  const loadActiveSearches = async () => {
    loading.value = true
    error.value = null
    try {
      const data = await get('/searches')
      if (data?._fallback) {
        // Dev fallback when API is down
        activeSearches.value = []
        console.warn('Using fallback active searches (API down)')
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

  const startSearch = async (jaml) => {
    if (!jaml.trim()) {
      searchStatus.value = 'Enter a filter'
      return
    }

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
      searchStatus.value = 'Running...'
      
      // Reload active searches
      await loadActiveSearches()
      
      return data.searchId
    } catch (e) {
      searchStatus.value = `Failed: ${e.message}`
      return null
    }
  }

  const stopAll = async () => {
    if (!currentSearchId.value) {
      isSearching.value = false
      searchStatus.value = 'No active search'
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
    try {
      await post(`/search/${encodeURIComponent(searchId)}/stop`)
      await loadActiveSearches()
    } catch (e) {
      console.error('Failed to stop search:', e)
    }
  }

  const clearResults = async () => {
    // Just clear locally - no API endpoint needed
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
    loadActiveSearches,
    startSearch,
    stopAll,
    stopSearch,
    clearResults,
    exportResults
  }
}


import { ref } from 'vue'
import { useApi } from './useApi'

export function useSearch() {
  const results = ref([])
  const columns = ref(['seed', 'score'])
  const searchStatus = ref('Ready')
  const isSearching = ref(false)
  const activeSearches = ref([])
  const currentSearchId = ref(null)
  const { post, delete: del } = useApi()

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
      return data.searchId
    } catch (e) {
      searchStatus.value = `Failed: ${e.message}`
      return null
    }
  }

  const stopAll = async () => {
    try {
      await post('/search/stop-all')
      isSearching.value = false
      searchStatus.value = 'Stopped'
    } catch (e) {
      searchStatus.value = `Error: ${e.message}`
    }
  }

  const clearResults = async () => {
    if (currentSearchId.value) {
      try {
        await del(`/search/${encodeURIComponent(currentSearchId.value)}/results`)
      } catch (e) {
        console.error('Failed to clear results:', e)
      }
    }
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
    startSearch,
    stopAll,
    clearResults,
    exportResults
  }
}


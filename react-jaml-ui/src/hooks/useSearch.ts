import { useState, useCallback } from 'react'

interface SearchResult {
  seed: string
  score: number
  tallies?: any[]
}

interface ActiveSearch {
  searchId: string
  status: string
  progress?: number
  searched?: number
  found?: number
}

export function useSearch() {
  const [results, setResults] = useState<SearchResult[]>([])
  const [columns, setColumns] = useState<string[]>(['seed', 'score'])
  const [searchStatus, setSearchStatus] = useState('Ready')
  const [isSearching, setIsSearching] = useState(false)
  const [activeSearches, setActiveSearches] = useState<ActiveSearch[]>([])
  const [currentSearchId, setCurrentSearchId] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const apiCall = async (url: string, options?: RequestInit) => {
    const response = await fetch(url, {
      headers: {
        'Content-Type': 'application/json',
        ...options?.headers
      },
      ...options
    })
    
    if (!response.ok) {
      throw new Error(`API error: ${response.status}`)
    }
    
    return response.json()
  }

  const loadActiveSearches = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await apiCall('/searches')
      if (data?._fallback) {
        setActiveSearches([])
        console.warn('Using fallback active searches (API down)')
      } else {
        setActiveSearches(data.searches || data || [])
      }
    } catch (e) {
      console.error('Failed to load active searches:', e)
      setError('Failed to load searches')
      setActiveSearches([])
    } finally {
      setLoading(false)
    }
  }, [])

  const startSearch = useCallback(async (jaml: string) => {
    if (!jaml.trim()) {
      setSearchStatus('Enter a filter')
      return null
    }

    setIsSearching(true)
    setSearchStatus('Starting search...')

    try {
      const data = await apiCall('/search', {
        method: 'POST',
        body: JSON.stringify({
          filterJaml: jaml,
          seedCount: 0,
          seedSource: 'all'
        })
      })

      const searchId = data.searchId || data.id
      setCurrentSearchId(searchId)
      setResults(data.results || [])
      setColumns(data.columns || columns)
      setSearchStatus('Search started - connecting to real-time updates')
      
      await loadActiveSearches()
      
      return searchId
    } catch (e: any) {
      console.error('Failed to start search:', e)
      setIsSearching(false)
      setSearchStatus(`Failed to start search: ${e.message}`)
      return null
    }
  }, [columns, loadActiveSearches])

  const stopAll = useCallback(async () => {
    if (!currentSearchId) {
      setIsSearching(false)
      setSearchStatus('No active search')
      return
    }
    try {
      await apiCall(`/search/${encodeURIComponent(currentSearchId)}/stop`, {
        method: 'POST'
      })
      setIsSearching(false)
      setSearchStatus('Stopped')
      await loadActiveSearches()
    } catch (e: any) {
      setSearchStatus(`Error: ${e.message}`)
    }
  }, [currentSearchId, loadActiveSearches])

  const stopSearch = useCallback(async (searchId: string) => {
    try {
      await apiCall(`/search/${encodeURIComponent(searchId)}/stop`, {
        method: 'POST'
      })
      await loadActiveSearches()
    } catch (e) {
      console.error('Failed to stop search:', e)
    }
  }, [loadActiveSearches])

  const clearResults = useCallback(() => {
    setResults([])
    setSearchStatus('Cleared')
  }, [])

  const exportResults = useCallback(() => {
    if (results.length === 0) return
    
    const headers = columns
    const csv = [
      headers.join(','),
      ...results.map(r => {
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
  }, [results, columns])

  return {
    results,
    columns,
    searchStatus,
    isSearching,
    activeSearches,
    currentSearchId,
    loading,
    error,
    loadActiveSearches,
    startSearch,
    stopAll,
    stopSearch,
    clearResults,
    exportResults,
    setResults: setResults,
    setSearchStatus: setSearchStatus,
    setIsSearching: setIsSearching
  }
}

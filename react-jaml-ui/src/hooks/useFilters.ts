import { useState, useCallback } from 'react'

interface Filter {
  id: string
  name: string
  filterJaml?: string
  author?: string
  created: string
}

export function useFilters() {
  const [filters, setFilters] = useState<Filter[]>([])
  const [currentFilter, setCurrentFilter] = useState<Filter | null>(null)
  const [currentFilterName, setCurrentFilterName] = useState('Untitled')
  const [jamlContent, setJamlContent] = useState('')
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

  const loadFilters = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const data = await apiCall('/filters')
      if (data?._fallback) {
        setFilters([])
        console.warn('API unavailable: no filters loaded')
      } else {
        setFilters(data.filters || data || [])
      }
    } catch (e) {
      console.error('Failed to load filters:', e)
      setError('Failed to load filters')
      setFilters([])
    } finally {
      setLoading(false)
    }
  }, [])

  const selectFilter = useCallback(async (filter: Filter) => {
    setCurrentFilter(filter)
    setCurrentFilterName(filter.name || filter.id || 'Untitled')
    
    if (filter.filterJaml) {
      setJamlContent(filter.filterJaml)
    } else {
      try {
        const data = await apiCall(`/filters/${encodeURIComponent(filter.id)}`)
        setJamlContent(data.filterJaml || '')
      } catch (e) {
        console.error('Failed to load filter content:', e)
      }
    }
  }, [])

  const saveFilter = useCallback(async (jaml: string) => {
    if (!jaml.trim()) return false
    
    try {
      const filterId = currentFilter?.id
      await apiCall('/filters/update', {
        method: 'POST',
        body: JSON.stringify({
          filterId: filterId || 'new',
          filterJaml: jaml
        })
      })
      await loadFilters()
      return true
    } catch (e) {
      console.error('Failed to save filter:', e)
      return false
    }
  }, [currentFilter, loadFilters])

  const deleteFilter = useCallback(async (filter: Filter) => {
    if (!confirm(`Delete "${filter.name}"?`)) return false
    
    try {
      await apiCall(`/filters/${encodeURIComponent(filter.id)}`, {
        method: 'DELETE'
      })
      await loadFilters()
      return true
    } catch (e) {
      console.error('Failed to delete filter:', e)
      return false
    }
  }, [loadFilters])

  return {
    filters,
    currentFilter,
    currentFilterName,
    jamlContent,
    setJamlContent,
    loading,
    error,
    loadFilters,
    selectFilter,
    saveFilter,
    deleteFilter
  }
}

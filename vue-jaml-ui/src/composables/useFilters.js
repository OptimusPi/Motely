import { ref } from 'vue'
import { useApi } from './useApi'

const STORAGE_KEY = 'jaml-filters'

/**
 * Load filters from localStorage (standalone fallback)
 */
function loadLocalFilters() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

/**
 * Save filters to localStorage
 */
function saveLocalFilters(filters) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(filters))
  } catch (e) {
    console.warn('Failed to save filters to localStorage:', e)
  }
}

/**
 * Extract a filter name from JAML content
 */
function extractFilterName(jaml) {
  const match = jaml.match(/^name:\s*(.+)$/m)
  return match ? match[1].trim() : 'Untitled'
}

export function useFilters() {
  const filters = ref([])
  const currentFilter = ref(null)
  const currentFilterName = ref('Untitled')
  const jamlContent = ref('')
  const loading = ref(false)
  const error = ref(null)
  const isOffline = ref(false) // true when API is unreachable, use localStorage
  const { get, post, delete: del } = useApi()

  const loadFilters = async () => {
    loading.value = true
    error.value = null
    try {
      const data = await get('/filters')
      if (data?._fallback) {
        // API down - use localStorage
        isOffline.value = true
        filters.value = loadLocalFilters()
        console.warn('API unavailable: loaded filters from localStorage')
      } else {
        isOffline.value = false
        filters.value = data.filters || data || []
        // Sync API filters to localStorage as backup
        saveLocalFilters(filters.value)
      }
    } catch (e) {
      console.error('Failed to load filters from API, falling back to localStorage:', e)
      isOffline.value = true
      error.value = null // Don't show error when we have a working fallback
      filters.value = loadLocalFilters()
    } finally {
      loading.value = false
    }
  }

  const selectFilter = async (filter) => {
    currentFilter.value = filter
    currentFilterName.value = filter.name || filter.id || 'Untitled'
    
    if (filter.filterJaml) {
      jamlContent.value = filter.filterJaml
    } else if (isOffline.value) {
      // In offline mode, filterJaml should always be present from localStorage
      jamlContent.value = filter.filterJaml || ''
    } else {
      // Load full filter content from API
      try {
        const data = await get(`/filters/${encodeURIComponent(filter.id)}`)
        jamlContent.value = data.filterJaml || ''
      } catch (e) {
        console.error('Failed to load filter content:', e)
      }
    }
  }

  const saveFilter = async (jaml) => {
    if (!jaml.trim()) return false
    
    if (isOffline.value) {
      // Save to localStorage directly
      const name = extractFilterName(jaml)
      const filterId = currentFilter.value?.id || `local-${Date.now()}`
      const localFilters = loadLocalFilters()
      
      const existingIdx = localFilters.findIndex(f => f.id === filterId)
      const filterObj = {
        id: filterId,
        name,
        filterJaml: jaml,
        updatedAt: new Date().toISOString()
      }
      
      if (existingIdx >= 0) {
        localFilters[existingIdx] = filterObj
      } else {
        localFilters.push(filterObj)
      }
      
      saveLocalFilters(localFilters)
      filters.value = localFilters
      currentFilter.value = filterObj
      currentFilterName.value = name
      return true
    }
    
    try {
      const filterId = currentFilter.value?.id
      await post('/filters/update', {
        filterId: filterId || 'new',
        filterJaml: jaml
      })
      await loadFilters()
      return true
    } catch (e) {
      console.error('Failed to save filter to API, saving to localStorage:', e)
      // Fallback: save locally even if API fails
      isOffline.value = true
      return saveFilter(jaml) // Recursive call will hit the offline branch
    }
  }

  const deleteFilter = async (filter) => {
    if (!confirm(`Delete "${filter.name}"?`)) return false
    
    if (isOffline.value) {
      const localFilters = loadLocalFilters().filter(f => f.id !== filter.id)
      saveLocalFilters(localFilters)
      filters.value = localFilters
      if (currentFilter.value?.id === filter.id) {
        currentFilter.value = null
        currentFilterName.value = 'Untitled'
        jamlContent.value = ''
      }
      return true
    }
    
    try {
      await del(`/filters/${encodeURIComponent(filter.id)}`)
      await loadFilters()
      return true
    } catch (e) {
      console.error('Failed to delete filter:', e)
      return false
    }
  }

  /**
   * Export current JAML as a downloadable .jaml file
   */
  const exportFilter = (jaml) => {
    if (!jaml?.trim()) return
    const name = extractFilterName(jaml) || 'filter'
    const safeName = name.replace(/[^a-zA-Z0-9_-]/g, '_')
    const blob = new Blob([jaml], { type: 'text/yaml' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${safeName}.jaml`
    a.click()
    URL.revokeObjectURL(url)
  }

  /**
   * Import a .jaml file from disk
   */
  const importFilter = () => {
    return new Promise((resolve) => {
      const input = document.createElement('input')
      input.type = 'file'
      input.accept = '.jaml,.yaml,.yml'
      input.onchange = async (e) => {
        const file = e.target.files?.[0]
        if (!file) return resolve(null)
        const text = await file.text()
        jamlContent.value = text
        const name = extractFilterName(text)
        currentFilterName.value = name
        currentFilter.value = { id: `imported-${Date.now()}`, name, filterJaml: text }
        resolve(text)
      }
      input.click()
    })
  }

  return {
    filters,
    currentFilter,
    currentFilterName,
    jamlContent,
    loading,
    error,
    isOffline,
    loadFilters,
    selectFilter,
    saveFilter,
    deleteFilter,
    exportFilter,
    importFilter
  }
}



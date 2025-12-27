import { ref } from 'vue'
import { useApi } from './useApi'

export function useFilters() {
  const filters = ref([])
  const currentFilter = ref(null)
  const currentFilterName = ref('Untitled.jaml')
  const jamlContent = ref('')
  const { get, post, delete: del } = useApi()

  const loadFilters = async () => {
    try {
      const data = await get('/filters')
      filters.value = data.filters || data || []
    } catch (e) {
      console.error('Failed to load filters:', e)
    }
  }

  const selectFilter = async (filter) => {
    currentFilter.value = filter
    currentFilterName.value = filter.name || filter.id || 'Untitled.jaml'
    
    if (filter.filterJaml) {
      jamlContent.value = filter.filterJaml
    } else {
      // Load full filter content
      try {
        const data = await get(`/filters/${encodeURIComponent(filter.id)}`)
        jamlContent.value = data.filterJaml || ''
      } catch (e) {
        console.error('Failed to load filter content:', e)
      }
    }
  }

  const saveFilter = async (jaml) => {
    if (!jaml.trim()) return
    
    try {
      const filterId = currentFilter.value?.id
      await post('/filters/update', {
        filterId: filterId || 'new',
        filterJaml: jaml
      })
      await loadFilters()
      return true
    } catch (e) {
      console.error('Failed to save filter:', e)
      return false
    }
  }

  const deleteFilter = async (filter) => {
    if (!confirm(`Delete "${filter.name}"?`)) return
    
    try {
      await del(`/filters/${encodeURIComponent(filter.id)}`)
      await loadFilters()
      return true
    } catch (e) {
      console.error('Failed to delete filter:', e)
      return false
    }
  }

  return {
    filters,
    currentFilter,
    currentFilterName,
    jamlContent,
    loadFilters,
    selectFilter,
    saveFilter,
    deleteFilter
  }
}



import { ref } from 'vue'

/**
 * Composable for API calls with error handling
 * Provides consistent fetch wrapper with loading states
 */
export function useApi() {
  const loading = ref(false)
  const error = ref(null)
  
  /**
   * Make an API request
   * @param {string} url - Request URL
   * @param {Object} options - Fetch options
   * @returns {Promise<Object>} Response data
   */
  const request = async (url, options = {}) => {
    loading.value = true
    error.value = null
    
    try {
      const response = await fetch(url, {
        ...options,
        headers: {
          'Content-Type': 'application/json',
          ...options.headers
        }
      })
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`)
      }
      
      const contentType = response.headers.get('content-type')
      if (contentType && contentType.includes('application/json')) {
        return await response.json()
      }
      
      return await response.text()
    } catch (err) {
      error.value = err
      console.error('API request failed:', err)
      throw err
    } finally {
      loading.value = false
    }
  }
  
  /**
   * GET request
   */
  const get = (url, options = {}) => {
    return request(url, { ...options, method: 'GET' })
  }
  
  /**
   * POST request
   */
  const post = (url, data, options = {}) => {
    return request(url, {
      ...options,
      method: 'POST',
      body: JSON.stringify(data)
    })
  }
  
  /**
   * PUT request
   */
  const put = (url, data, options = {}) => {
    return request(url, {
      ...options,
      method: 'PUT',
      body: JSON.stringify(data)
    })
  }
  
  /**
   * DELETE request
   */
  const del = (url, options = {}) => {
    return request(url, { ...options, method: 'DELETE' })
  }
  
  return {
    loading,
    error,
    request,
    get,
    post,
    put,
    delete: del
  }
}


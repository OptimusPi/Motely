import { ref } from 'vue'
import { useRequests } from './useRequests'

/**
 * Get the API base URL for the current environment
 */
function getApiBaseUrl() {
  // In development, use relative URLs (Vite proxy handles them)
  if (import.meta.env.DEV) {
    return ''
  }
  
  // In production, check for API_URL environment variable or use same origin
  // If API_URL is set, use it; otherwise assume API is on same origin
  const apiUrl = import.meta.env.VITE_API_URL
  if (apiUrl) {
    return apiUrl
  }
  
  // Default: use same origin (API should be on same server)
  return window.location.origin
}

/**
 * Composable for API calls with error handling
 * Provides consistent fetch wrapper with loading states
 */
export function useApi() {
  const loading = ref(false)
  const error = ref(null)
  const { addRequest, updateRequest } = useRequests()
  
  /**
   * Make an API request
   * @param {string} url - Request URL (can be relative or absolute)
   * @param {Object} options - Fetch options
   * @returns {Promise<Object>} Response data
   */
  const request = async (url, options = {}) => {
    loading.value = true
    error.value = null
    
    // Resolve relative URLs to full API URLs
    let fullUrl = url
    if (url.startsWith('/')) {
      // Relative URL - prepend API base URL
      const apiBase = getApiBaseUrl()
      fullUrl = apiBase + url
    }
    
    // Extract method for logging
    const method = options.method || 'GET'
    
    // Log request start (addRequest returns the request object reference)
    const trackedRequest = addRequest(method, fullUrl, 'pending')
    
    try {
      const response = await fetch(fullUrl, {
        ...options,
        headers: {
          'Content-Type': 'application/json',
          ...options.headers
        }
      })
      
      // Update request status using object reference (safe with concurrent requests)
      if (response.ok) {
        updateRequest(trackedRequest, 'success')
      } else {
        updateRequest(trackedRequest, 'error', `HTTP ${response.status}: ${response.statusText}`)
      }
      
      if (!response.ok) {
        if (response.status === 0 || !navigator.onLine) {
          throw new Error('Server unavailable. Please check your connection.')
        }
        if ((response.status >= 500 || response.status === 0) && import.meta.env.DEV) {
          // In development, provide fallback data when API is down
          return { _fallback: true, status: response.status }
        }
        throw new Error(`HTTP ${response.status}: ${response.statusText}`)
      }
      
      const contentType = response.headers.get('content-type')
      if (contentType && contentType.includes('application/json')) {
        return await response.json()
      }
      
      return await response.text()
    } catch (err) {
      error.value = err
      // Update request status to error (using object reference)
      updateRequest(trackedRequest, 'error', err.message)
      
      // In dev, suppress HTTP errors and return fallback
      if (import.meta.env.DEV) {
        if (err.name === 'TypeError' || err.message.includes('fetch') || err.message.includes('500')) {
          // Silently use fallback in dev mode
          return { _fallback: true, status: 0, error: err.message }
        }
      }
      console.error('API request failed:', err)
      // Re-throw with user-friendly message
      if (err.name === 'TypeError' && err.message.includes('fetch')) {
        throw new Error('Server unavailable. Please try again later.')
      }
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


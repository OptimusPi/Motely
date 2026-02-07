import { ref } from 'vue'

const requests = ref([])

export function useRequests() {
  const addRequest = (method, url, status = 'pending', error = null) => {
    const request = {
      method: method.toUpperCase(),
      url,
      status,
      error,
      timestamp: Date.now()
    }
    
    requests.value.unshift(request) // Add to beginning
    
    // Keep only last 100 requests
    if (requests.value.length > 100) {
      requests.value = requests.value.slice(0, 100)
    }
    
    // Return the request object reference for direct mutation
    return request
  }

  /**
   * Update a request by object reference or by index.
   * Prefer passing the request object returned by addRequest.
   */
  const updateRequest = (requestOrIndex, status, error = null) => {
    let target
    if (typeof requestOrIndex === 'object' && requestOrIndex !== null) {
      // Direct reference - update the object in-place (it's already in the array)
      target = requestOrIndex
    } else {
      // Legacy index-based lookup
      target = requests.value[requestOrIndex]
    }
    if (target) {
      target.status = status
      if (error) {
        target.error = error
      }
    }
  }

  const clearRequests = () => {
    requests.value = []
  }

  return {
    requests,
    addRequest,
    updateRequest,
    clearRequests
  }
}

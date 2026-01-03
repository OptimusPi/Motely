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
    
    return request
  }

  const updateRequest = (index, status, error = null) => {
    if (requests.value[index]) {
      requests.value[index].status = status
      if (error) {
        requests.value[index].error = error
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

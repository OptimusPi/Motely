import { ref } from 'vue'

/**
 * Simple error state management
 */
export function useGlobalError() {
  const error = ref(null)
  const showError = ref(false)

  const captureError = (err) => {
    console.error('Error captured:', err)
    error.value = err
    showError.value = true
  }

  const dismissError = () => {
    error.value = null
    showError.value = false
  }

  return {
    error,
    showError,
    captureError,
    dismissError
  }
}

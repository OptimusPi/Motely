import { ref } from 'vue'

let toastIdCounter = 0

export function useToasts() {
  const toasts = ref([])

  const showToast = (message, type = 'info', duration = 3000) => {
    const id = ++toastIdCounter
    toasts.value.push({ id, message, type })
    if (duration > 0) setTimeout(() => removeToast(id), duration)
  }

  const removeToast = (id) => {
    const idx = toasts.value.findIndex(t => t.id === id)
    if (idx >= 0) toasts.value.splice(idx, 1)
  }

  return { toasts, showToast, removeToast }
}

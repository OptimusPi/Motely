<template>
  <slot v-if="!hasError" />
  <div v-else class="error-boundary">
    <div class="error-content">
      <h2>⚠️ Something went wrong</h2>
      <p>{{ errorMessage }}</p>
      <button @click="retry" class="btn btn-primary">Retry</button>
    </div>
  </div>
</template>

<script setup>
import { ref, onErrorCaptured } from 'vue'

const hasError = ref(false)
const errorMessage = ref('')

onErrorCaptured((err) => {
  console.error('Error boundary caught:', err)
  hasError.value = true
  errorMessage.value = err?.message || String(err)
  // Prevent error from propagating
  return false
})

const retry = () => {
  hasError.value = false
  errorMessage.value = ''
}
</script>

<style scoped>
.error-boundary {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: var(--bg);
  color: var(--text);
}

.error-content {
  text-align: center;
  padding: 2rem;
  background: var(--panel);
  border: 2px solid var(--border);
  border-radius: 8px;
  max-width: 500px;
}

.error-content h2 {
  margin-bottom: 1rem;
}

.error-content p {
  margin-bottom: 1.5rem;
  opacity: 0.8;
}

.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-family: inherit;
}

.btn-primary {
  background: var(--green);
  color: white;
}

.btn-primary:hover {
  background: var(--green-dark);
}
</style>

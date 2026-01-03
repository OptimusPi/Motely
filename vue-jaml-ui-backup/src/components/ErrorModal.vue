<template>
  <teleport to="body">
    <div v-if="showError" class="error-overlay">
      <div class="error-modal">
        <div class="error-header">
          <h3>⚠️ Error</h3>
          <button @click="dismissError" class="close-btn">&times;</button>
        </div>
        <div class="error-body">
          <p v-if="error?.message">{{ error.message }}</p>
          <p v-else-if="error">{{ error }}</p>
          <p v-else>Something went wrong</p>
        </div>
        <div class="error-footer">
          <button @click="dismissError" class="btn btn-primary">Dismiss</button>
        </div>
      </div>
    </div>
  </teleport>
</template>

<script setup>
defineProps({
  showError: Boolean,
  error: [Error, String, Object],
  dismissError: Function
})
</script>

<style scoped>
.error-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.8);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10000;
}

.error-modal {
  background: var(--panel);
  border: 2px solid var(--border);
  border-radius: 8px;
  padding: 20px;
  min-width: 300px;
  max-width: 500px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
}

.error-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.error-header h3 {
  margin: 0;
  color: var(--text);
}

.close-btn {
  background: none;
  border: none;
  color: var(--text);
  font-size: 24px;
  cursor: pointer;
  padding: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.close-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.error-body {
  margin-bottom: 20px;
  color: var(--text);
}

.error-footer {
  display: flex;
  justify-content: flex-end;
}

.btn {
  padding: 6px 12px;
  border: 1px solid var(--border);
  background: var(--dark);
  color: var(--text);
  border-radius: 4px;
  cursor: pointer;
}

.btn-primary {
  background: var(--green);
  border-color: var(--green);
}

.btn-primary:hover {
  background: var(--green-dark);
}
</style>

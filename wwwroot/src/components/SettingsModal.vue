<template>
  <div class="modal" @click.self="$emit('close')">
    <div class="modal-content">
      <div class="modal-header">
        <h3 class="modal-title">Settings</h3>
        <button @click="$emit('close')" class="modal-close">×</button>
      </div>
      <div class="modal-body">
        <div class="settings-section">
          <h4>Filters</h4>
          <div class="filter-list">
            <div
              v-for="filter in filters"
              :key="filter.id"
              class="filter-item"
            >
              <span>{{ filter.name }}</span>
              <button
                @click="$emit('delete-filter', filter)"
                class="btn btn-danger btn-sm"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
defineProps({
  filters: {
    type: Array,
    default: () => []
  }
})

defineEmits(['close', 'select-filter', 'delete-filter'])
</script>

<style scoped>
.modal-body {
  max-height: 70vh;
  overflow-y: auto;
}

.settings-section {
  margin-bottom: 24px;
}

.settings-section h4 {
  margin-bottom: 12px;
  font-size: 16px;
}

.filter-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.filter-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  background: var(--dark);
  border: 1px solid var(--border);
  border-radius: 4px;
}
</style>



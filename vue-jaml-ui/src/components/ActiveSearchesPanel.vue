<template>
  <div class="active-searches">
    <div v-if="searches.length === 0" class="no-searches">
      No active searches
    </div>
    
    <!-- Mobile Card Layout -->
    <div v-else-if="isMobile" class="searches-cards">
      <div v-for="search in searches" :key="search.searchId" class="search-card">
        <div class="card-header">
          <span class="card-id">{{ search.searchId?.substring(0, 8) }}...</span>
          <span class="card-status" :class="`status-${search.status}`">{{ search.status }}</span>
        </div>
        <div class="card-body">
          <div class="card-row">
            <span class="card-label">Progress:</span>
            <span class="card-value">{{ search.progress }}%</span>
          </div>
          <div class="card-row">
            <span class="card-label">Speed:</span>
            <span class="card-value">{{ formatSpeed(search.speed) }}</span>
          </div>
          <div class="card-row">
            <span class="card-label">Searched:</span>
            <span class="card-value">{{ formatNumber(search.searched) }}</span>
          </div>
          <div class="card-row">
            <span class="card-label">Found:</span>
            <span class="card-value">{{ search.found }}</span>
          </div>
        </div>
        <div class="card-footer">
          <button 
            @click="$emit('stop-search', search.searchId)" 
            class="btn btn-danger"
            style="width: 100%; min-height: 44px;"
          >
            Stop Search
          </button>
        </div>
      </div>
    </div>
    
    <!-- Desktop Table Layout -->
    <table v-else class="searches-table">
      <thead>
        <tr>
          <th>ID</th>
          <th>Status</th>
          <th>Progress</th>
          <th>Speed</th>
          <th>Searched</th>
          <th>Found</th>
          <th>Action</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="search in searches" :key="search.searchId">
          <td>{{ search.searchId?.substring(0, 8) }}...</td>
          <td>{{ search.status }}</td>
          <td>{{ search.progress }}%</td>
          <td>{{ formatSpeed(search.speed) }}</td>
          <td>{{ formatNumber(search.searched) }}</td>
          <td>{{ search.found }}</td>
          <td>
            <button @click="$emit('stop-search', search.searchId)" class="btn btn-danger btn-sm">
              Stop
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useLayout } from '../composables/useLayout'

defineProps({
  searches: {
    type: Array,
    default: () => []
  }
})

defineEmits(['stop-search'])

const { windowWidth } = useLayout()
const isMobile = computed(() => windowWidth.value < 768)

const formatSpeed = (speed) => {
  if (speed == null) return '0/s'
  return `${Number(speed).toLocaleString()}/s`
}

const formatNumber = (num) => {
  if (num == null) return '0'
  return Number(num).toLocaleString()
}
</script>

<style scoped>
.active-searches {
  padding: 12px;
  height: 100%;
  overflow: auto;
}

.no-searches {
  text-align: center;
  color: var(--muted);
  padding: 24px;
}

.searches-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 14px;
}

.searches-table th,
.searches-table td {
  padding: 8px;
  text-align: left;
  border-bottom: 1px solid var(--border);
}

.searches-table th {
  background: var(--panel);
  font-weight: normal;
}

.btn-sm {
  padding: 4px 8px;
  font-size: 12px;
}

/* Mobile Card Layout */
.searches-cards {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 8px;
}

.search-card {
  background: var(--panel);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border);
}

.card-id {
  font-family: monospace;
  font-size: 12px;
  color: var(--muted);
}

.card-status {
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: normal;
  text-transform: uppercase;
}

.status-running {
  background: var(--green);
  color: white;
}

.status-stopped {
  background: var(--muted);
  color: white;
}

.card-body {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.card-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-label {
  color: var(--muted);
  font-size: 13px;
}

.card-value {
  font-weight: normal;
  font-size: 14px;
}

.card-footer {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--border);
}

@media (max-width: 768px) {
  .active-searches {
    padding: 8px;
  }
}
</style>



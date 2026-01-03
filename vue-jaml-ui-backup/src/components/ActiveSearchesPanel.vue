<template>
  <div class="active-searches">
    <div v-if="searches.length === 0" class="no-searches">
      No active searches
    </div>
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
defineProps({
  searches: {
    type: Array,
    default: () => []
  }
})

defineEmits(['stop-search'])

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
  font-weight: bold;
}

.btn-sm {
  padding: 4px 8px;
  font-size: 12px;
}
</style>



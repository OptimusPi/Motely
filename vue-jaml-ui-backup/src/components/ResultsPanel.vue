<template>
  <div class="results-container">
    <div class="results-toolbar">
      <select v-model="seedSource" class="select">
        <option
          v-for="source in seedSources"
          :key="source.key"
          :value="source.key"
        >
          {{ source.displayName || source.label }}
        </option>
      </select>
      <button
        @click="isSearching ? $emit('stop') : $emit('start')"
        :class="['btn', isSearching ? 'btn-danger' : 'btn-primary']"
      >
        {{ isSearching ? '⏹ Stop' : '▶ Start' }}
      </button>
      <button @click="$emit('clear')" class="btn">🗑️ Clear</button>
      <button @click="$emit('export')" class="btn">📥 Export</button>
    </div>
    
    <div class="status-bar">{{ status }}</div>
    
    <div ref="tableContainer" class="results-table" />
  </div>
</template>

<script setup>
import { ref, watch, onMounted, onUnmounted } from 'vue'
import { TabulatorFull as Tabulator } from 'tabulator-tables'
import { useApi } from '../composables/useApi'

const props = defineProps({
  results: {
    type: Array,
    default: () => []
  },
  columns: {
    type: Array,
    default: () => ['seed', 'score']
  },
  status: {
    type: String,
    default: 'Ready'
  },
  isSearching: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['start', 'stop', 'clear', 'export'])

const tableContainer = ref(null)
const seedSource = ref('all')
const seedSources = ref([])
const loading = ref(false)
const error = ref(null)
const { get } = useApi()
let table = null

const initTable = () => {
  if (!tableContainer.value) return
  
  try {
    // Map results to table format
    const tableData = props.results.map(r => {
      const row = {
        seed: r.seed || '',
        score: r.score || 0
      }
      // Add tallies as columns
      if (r.tallies && Array.isArray(r.tallies)) {
        r.tallies.forEach((tally, idx) => {
          row[`tally_${idx}`] = tally
        })
      }
      return row
    })

    if (table) {
      table.destroy()
    }

    table = new Tabulator(tableContainer.value, {
      data: tableData,
      columns: props.columns.map((col, idx) => {
        if (idx === 0) {
          return {
            title: col,
            field: 'seed',
            sorter: 'string',
            formatter: 'plaintext'
          }
        } else if (idx === 1) {
          return {
            title: col,
            field: 'score',
            sorter: 'number',
            formatter: (cell) => {
              const val = cell.getValue()
              return typeof val === 'number' ? val.toLocaleString() : val
            }
          }
        } else {
          return {
            title: col,
            field: `tally_${idx - 2}`,
            sorter: 'number',
            formatter: 'plaintext'
          }
        }
      }),
      layout: 'fitColumns',
      height: '100%',
      placeholder: 'No results yet',
      initialSort: [{ column: 'score', dir: 'desc' }]
    })
  } catch (error) {
    console.error('Failed to init table:', error)
    if (tableContainer.value) {
      tableContainer.value.innerHTML = `<div style="padding: 20px; color: white;">Table error: ${error.message}</div>`
    }
  }
}

watch(() => props.results, (newResults) => {
  if (table) {
    const tableData = newResults.map(r => {
      const row = {
        seed: r.seed || '',
        score: r.score || 0
      }
      if (r.tallies && Array.isArray(r.tallies)) {
        r.tallies.forEach((tally, idx) => {
          row[`tally_${idx}`] = tally
        })
      }
      return row
    })
    table.replaceData(tableData)
  }
}, { deep: true })

watch(() => props.columns, () => {
  if (table) {
    initTable()
  }
})

const loadSeedSources = async () => {
  loading.value = true
  error.value = null
  try {
    const data = await get('/seed-sources')
    if (data?._fallback) {
      // Dev fallback when API is down
      seedSources.value = []
      console.warn('API unavailable: no seed sources loaded')
    } else {
      seedSources.value = data.sources || data || []
    }
  } catch (e) {
    console.error('Failed to load seed sources:', e)
    error.value = 'Failed to load seed sources'
    seedSources.value = []
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadSeedSources()
  initTable()
})

onUnmounted(() => {
  if (table) {
    table.destroy()
  }
})
</script>

<style scoped>
/* Tabulator styles */
.results-container {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.results-table {
  flex: 1;
  overflow: hidden;
}

:deep(.tabulator) {
  background: var(--dark-bg);
  border: 1px solid var(--border-color);
}

:deep(.tabulator-header) {
  background: var(--panel-bg);
  border-bottom: 1px solid var(--border-color);
}

:deep(.tabulator-header .tabulator-col) {
  background: var(--panel-bg);
  border-right: 1px solid var(--border-color);
  color: var(--text-color);
  font-weight: normal;
}

:deep(.tabulator-body) {
  background: var(--dark-bg);
}

:deep(.tabulator-row) {
  background: var(--dark-bg);
  border-bottom: 1px solid var(--border-color);
  color: var(--text-color);
}

:deep(.tabulator-row:hover) {
  background: var(--panel-bg);
}

:deep(.tabulator-row.tabulator-selected) {
  background: var(--balatro-gold);
  color: var(--dark-bg);
}
</style>


<template>
  <div id="app">
    <!-- Top Bar -->
    <div class="top-bar">
      <button @click="goHome" title="Home">🏠</button>
      <span class="top-bar-title">JAML</span>
      <button @click="toggleSettings" title="Settings">⚙️</button>
    </div>

    <!-- Main Layout -->
    <div class="main-layout">
      <SplitPane
        :split="isPortrait ? 'horizontal' : 'vertical'"
        :default-percent="50"
        :min-percent="20"
        :max-percent="80"
        @resize="onSplitResize"
      >
        <template #left>
          <div class="split-pane-left">
            <!-- JAML Editor -->
            <PanelSection
              :color="'red'"
              :label="currentFilterName || 'Untitled.jaml'"
              :min-height="200"
              @resize="onEditorResize"
            >
              <EditorPanel
                v-model:jaml="jamlContent"
                @save="handleSave"
              />
            </PanelSection>

            <!-- Blueprint Analyzer -->
            <PanelSection
              :color="'blue'"
              label="Blueprint Analyzer"
              :min-height="180"
              :default-height="300"
            >
              <BlueprintPanel v-model:seed="blueprintSeed" />
            </PanelSection>
          </div>
        </template>

        <template #right>
          <div class="split-pane-right">
            <!-- Active Searches -->
            <PanelSection
              :color="'green'"
              :label="`Running Searches ${activeSearches.length > 0 ? `(${activeSearches.length})` : ''}`"
              :min-height="100"
              :default-height="150"
            >
              <ActiveSearchesPanel :searches="activeSearches" @stop="handleStopSearch" />
            </PanelSection>

            <!-- Results -->
            <PanelSection
              :color="'purple'"
              :label="`Results ${results.length > 0 ? `(${results.length})` : ''}`"
              :min-height="200"
            >
              <ResultsPanel
                :results="results"
                :columns="columns"
                :status="searchStatus"
                :is-searching="isSearching"
                @start="handleStartSearch"
                @stop="handleStopAll"
                @clear="handleClearResults"
                @export="handleExport"
              />
            </PanelSection>
          </div>
        </template>
      </SplitPane>
    </div>

    <!-- Settings Modal -->
    <SettingsModal
      v-if="showSettings"
      @close="showSettings = false"
      :filters="filters"
      @select-filter="handleSelectFilter"
      @delete-filter="handleDeleteFilter"
    />
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import SplitPane from './components/SplitPane.vue'
import PanelSection from './components/PanelSection.vue'
import EditorPanel from './components/EditorPanel.vue'
import BlueprintPanel from './components/BlueprintPanel.vue'
import ActiveSearchesPanel from './components/ActiveSearchesPanel.vue'
import ResultsPanel from './components/ResultsPanel.vue'
import SettingsModal from './components/SettingsModal.vue'
import { useFilters } from './composables/useFilters'
import { useSearch } from './composables/useSearch'
import { useSignalR } from './composables/useSignalR'
import { useLayout } from './composables/useLayout'
import { useApi } from './composables/useApi'

// Layout
const { isPortrait } = useLayout()

// Filters
const {
  filters,
  currentFilter,
  currentFilterName,
  jamlContent,
  loadFilters,
  selectFilter,
  saveFilter,
  deleteFilter
} = useFilters()

// Search
const {
  results,
  columns,
  searchStatus,
  isSearching,
  activeSearches,
  startSearch,
  stopAll,
  clearResults,
  exportResults
} = useSearch()

// SignalR
const { connect, disconnect, joinSearchGroup } = useSignalR({
  onResult: (result) => {
    results.value.push(result)
  },
  onProgress: (progress) => {
    searchStatus.value = progress.message || progress.status || 'Running...'
  },
  onSearchUpdate: (search) => {
    const idx = activeSearches.value.findIndex(s => s.id === search.searchId)
    if (idx >= 0) {
      activeSearches.value[idx] = {
        id: search.searchId,
        status: search.status,
        progress: search.progressPercent || 0,
        speed: search.seedsPerSecond || 0,
        searched: search.seedsSearched || 0,
        found: search.seedsFound || 0
      }
    } else {
      activeSearches.value.push({
        id: search.searchId,
        status: search.status,
        progress: search.progressPercent || 0,
        speed: search.seedsPerSecond || 0,
        searched: search.seedsSearched || 0,
        found: search.seedsFound || 0
      })
    }
  }
})

// UI State
const showSettings = ref(false)
const blueprintSeed = ref('')
const { post } = useApi()

// Handlers
const goHome = () => {
  window.location.href = '/'
}

const toggleSettings = () => {
  showSettings.value = !showSettings.value
}

const handleSave = async () => {
  await saveFilter(jamlContent.value)
}

const handleStartSearch = async () => {
  const searchId = await startSearch(jamlContent.value)
  if (searchId) {
    await joinSearchGroup(searchId)
  }
}

const handleStopAll = async () => {
  await stopAll()
}

const handleClearResults = async () => {
  await clearResults()
}

const handleExport = () => {
  exportResults()
}

const handleSelectFilter = async (filter) => {
  await selectFilter(filter)
}

const handleDeleteFilter = async (filter) => {
  await deleteFilter(filter)
}

const handleStopSearch = async (searchId) => {
  await fetch(`/search/${encodeURIComponent(searchId)}/stop`, { method: 'POST' })
}

const onSplitResize = () => {
  // Save layout state
}

const onEditorResize = () => {
  // Notify Monaco to relayout
  if (window.monacoEditor) {
    window.monacoEditor.layout()
  }
}

// Lifecycle
onMounted(async () => {
  try {
    await loadFilters()
    connect()
    
    // Load filter from URL
    const params = new URLSearchParams(window.location.search)
    const filterId = params.get('filter')
    if (filterId) {
      const filter = filters.value.find(f => f.id === filterId)
      if (filter) {
        await selectFilter(filter)
      }
    }
  } catch (error) {
    console.error('Failed to initialize:', error)
  }
})

onUnmounted(() => {
  disconnect()
})
</script>

<template>
  <div class="jaml-ui">
    <!-- Top Bar -->
    <div class="top-bar">
      <div
        class="top-bar-center"
        :class="[edgeStateClass, { dragging: isDraggingLayout }]"
        :style="[topBarCenterStyles, { width: `${topPillWidth}px` }]"
      >
        <div class="layout-controls" @pointerdown="startLayoutDrag">
          <button class="slider-icon" title="Home" @pointerdown.stop="goHome">🏠</button>
          <span class="layout-indicator" :class="`layout-${layoutMode}`">
            {{ layoutIndicatorLabel }}
          </span>
          <span class="top-bar-title">JAML:</span>
          <button class="slider-icon" title="Settings" @pointerdown.stop="toggleSettings">⚙️</button>
        </div>
      </div>
      <router-link to="/genie" class="nav-link">
        <button title="Genie">🧞‍♂️</button>
      </router-link>
    </div>

    <!-- Panels -->
    <div class="main-layout" :class="`layout-${layoutMode}`">
      <div v-if="layoutMode === 'stack'" class="layout-stack">
        <PanelSection
          v-for="panel in panels"
          :key="panel.id"
          :color="panel.color"
          :label="panel.label"
          :badge="panel.badge"
          :min-height="panel.minHeight"
          :default-height="panel.defaultHeight"
          :layout-mode="layoutMode"
          @resize="onPanelResize(panel.id, $event)"
          @collapse="onPanelCollapse(panel.id, $event)"
        >
          <component
            :is="panel.component"
            v-bind="panel.props || {}"
            :jaml="panel.id === 'jaml-editor' ? jamlContent : undefined"
            :results="panel.id === 'results' ? results : undefined"
            :columns="panel.id === 'results' ? columns : undefined"
            :status="panel.id === 'results' ? searchStatus : undefined"
            :is-searching="panel.id === 'results' ? isSearching : undefined"
            :searches="panel.id === 'active-searches' ? activeSearches : undefined"
            @save="handleSaveFilter"
            @start="handleStartSearch"
            @stop="handleStopSearch"
            @clear="clearResults"
            @export="exportResults"
            @stop-search="handleStopSpecificSearch"
            @update:jaml="updateJamlContent"
          />
        </PanelSection>
      </div>

      <div v-else class="layout-split" ref="splitLayoutRef">
        <div class="split-column split-left" :style="{ flex: `0 0 ${splitPercent}%` }">
          <PanelSection
            v-for="panel in leftPanels"
            :key="panel.id"
            :color="panel.color"
            :label="panel.label"
            :badge="panel.badge"
            :min-height="panel.minHeight"
            :default-height="panel.defaultHeight"
            :layout-mode="layoutMode"
            @resize="onPanelResize(panel.id, $event)"
            @collapse="onPanelCollapse(panel.id, $event)"
          >
            <component
              :is="panel.component"
              v-bind="panel.props || {}"
              :jaml="panel.id === 'jaml-editor' ? jamlContent : undefined"
              @save="handleSaveFilter"
              @update:jaml="updateJamlContent"
            />
          </PanelSection>
        </div>

        <div class="split-divider" :style="{ left: `calc(${splitPercent}% - 4px)` }" @pointerdown="startSplitDrag">
          <div class="divider-handle"></div>
        </div>

        <div class="split-column split-right" :style="{ flex: `0 0 ${100 - splitPercent}%` }">
          <PanelSection
            v-for="panel in rightPanels"
            :key="panel.id"
            :color="panel.color"
            :label="panel.label"
            :badge="panel.badge"
            :min-height="panel.minHeight"
            :default-height="panel.defaultHeight"
            :layout-mode="layoutMode"
            @resize="onPanelResize(panel.id, $event)"
            @collapse="onPanelCollapse(panel.id, $event)"
          >
            <component
              :is="panel.component"
              v-bind="panel.props || {}"
              :results="panel.id === 'results' ? results : undefined"
              :columns="panel.id === 'results' ? columns : undefined"
              :status="panel.id === 'results' ? searchStatus : undefined"
              :is-searching="panel.id === 'results' ? isSearching : undefined"
              :searches="panel.id === 'active-searches' ? activeSearches : undefined"
              @start="handleStartSearch"
              @stop="handleStopSearch"
              @clear="clearResults"
              @export="exportResults"
              @stop-search="handleStopSpecificSearch"
            />
          </PanelSection>
        </div>
      </div>
    </div>

    <SettingsModal
      v-if="showSettings"
      :filters="filters"
      @close="showSettings = false"
      @select-filter="handleSelectFilter"
      @delete-filter="handleDeleteFilter"
    />
    
    <ErrorModal
      :show-error="showError"
      :error="globalError"
      :dismiss-error="dismissError"
    />
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, reactive } from 'vue'
import PanelSection from '../components/PanelSection.vue'
import EditorPanel from '../components/EditorPanel.vue'
import BlueprintPanel from '../components/BlueprintPanel.vue'
import ActiveSearchesPanel from '../components/ActiveSearchesPanel.vue'
import ResultsPanel from '../components/ResultsPanel.vue'
import SettingsModal from '../components/SettingsModal.vue'
import ErrorModal from '../components/ErrorModal.vue'
import { useFilters } from '../composables/useFilters'
import { useSearch } from '../composables/useSearch'
import { useSignalR } from '../composables/useSignalR'
import { useGlobalError } from '../composables/useGlobalError'

// Layout state
const panels = reactive([
  {
    id: 'jaml-editor',
    color: 'red',
    label: 'JAML Editor',
    minHeight: 220,
    defaultHeight: 320,
    component: EditorPanel
  },
  {
    id: 'blueprint',
    color: 'blue',
    label: 'Blueprint Analyzer',
    minHeight: 220,
    defaultHeight: 320,
    component: BlueprintPanel
  },
  {
    id: 'active-searches',
    color: 'green',
    label: 'Active Searches',
    minHeight: 200,
    defaultHeight: 240,
    component: ActiveSearchesPanel
  },
  {
    id: 'results',
    color: 'purple',
    label: 'Results',
    minHeight: 260,
    defaultHeight: 360,
    component: ResultsPanel
  }
])

const leftPanels = computed(() => panels.slice(0, 2))
const rightPanels = computed(() => panels.slice(2))

const showSettings = ref(false)

// Layout mode
const layoutMode = ref('stack')

const updateLayoutMode = () => {
  layoutMode.value = window.innerWidth > window.innerHeight ? 'split' : 'stack'
}

// Dragging state
const isDraggingLayout = ref(false)
const isDraggingSplit = ref(false)
const splitPercent = ref(50)

const splitLayoutRef = ref(null)
const splitControlWidth = 220
const edgeThresholdPercent = 10
const getSplitBounds = () => {
  const container = splitLayoutRef.value
  if (!container) {
    return {
      left: 0,
      width: window.innerWidth
    }
  }
  return container.getBoundingClientRect()
}

const splitPercentMin = 1
const splitPercentMax = 99
const snapThresholdPercent = 0.5
const stackSnapActive = ref(false)

const clampSplitPercent = (value) => {
  const target = Number.isFinite(value) ? value : splitPercent.value
  return Math.max(splitPercentMin, Math.min(splitPercentMax, target))
}

const updateSplitFromClientX = (clientX) => {
  const { left, width } = getSplitBounds()
  const relative = (clientX - left) / (width || window.innerWidth)
  splitPercent.value = clampSplitPercent(relative * 100)
}

const edgeStateClass = computed(() => {
  if (layoutMode.value !== 'split') return ''
  if (splitPercent.value <= edgeThresholdPercent) return 'edge-left'
  if (splitPercent.value >= 100 - edgeThresholdPercent) return 'edge-right'
  return ''
})

const layoutIndicatorLabel = computed(() => {
  if (stackSnapActive.value) return 'SNAPPED'
  return layoutMode.value === 'stack' ? 'STACK' : 'SPLIT'
})

const topPillWidth = computed(() => (stackSnapActive.value ? 160 : splitControlWidth))
const topBarHalf = computed(() => topPillWidth.value / 2)

const topBarCenterStyles = computed(() => {
  if (layoutMode.value !== 'split') {
    return { left: `calc(50% - ${topBarHalf.value}px)` }
  }

  if (splitPercent.value <= edgeThresholdPercent) {
    return { left: '0px', right: 'auto' }
  }

  if (splitPercent.value >= 100 - edgeThresholdPercent) {
    return { right: '0px', left: 'auto' }
  }

  return { left: `calc(${splitPercent.value}% - ${topBarHalf.value}px)` }
})

const startLayoutDrag = (event) => {
  isDraggingLayout.value = true
  const startX = event.clientX
  let animationFrame = null

  if (layoutMode.value === 'split') {
    updateSplitFromClientX(event.clientX)
    // Adjust splitPercent
    const handlePointerMove = (moveEvent) => {
      if (animationFrame) {
        cancelAnimationFrame(animationFrame)
      }
      animationFrame = requestAnimationFrame(() => {
        updateSplitFromClientX(moveEvent.clientX)
      })
    }

    const handlePointerUp = () => {
      isDraggingLayout.value = false
      if (animationFrame) {
        cancelAnimationFrame(animationFrame)
      }
      document.removeEventListener('pointermove', handlePointerMove)
      document.removeEventListener('pointerup', handlePointerUp)
      maybeSnapToStack(splitPercent.value)
    }

    document.addEventListener('pointermove', handlePointerMove)
    document.addEventListener('pointerup', handlePointerUp)
  } else {
    // Switch mode
    const startMode = layoutMode.value

    const handlePointerMove = (moveEvent) => {
      const deltaX = moveEvent.clientX - startX
      const threshold = 100

      if (Math.abs(deltaX) > threshold) {
        layoutMode.value = deltaX > 0 && startMode === 'stack' ? 'split' : deltaX < 0 && startMode === 'split' ? 'stack' : startMode
      }
    }

    const handlePointerUp = () => {
      isDraggingLayout.value = false
      document.removeEventListener('pointermove', handlePointerMove)
      document.removeEventListener('pointerup', handlePointerUp)
    }

    document.addEventListener('pointermove', handlePointerMove)
    document.addEventListener('pointerup', handlePointerUp)
  }
}

// Split drag handling
const startSplitDrag = (event) => {
  isDraggingSplit.value = true
  const startX = event.clientX
  let animationFrame = null

  const handleMouseMove = (moveEvent) => {
    if (animationFrame) {
      cancelAnimationFrame(animationFrame)
    }
    animationFrame = requestAnimationFrame(() => {
      updateSplitFromClientX(moveEvent.clientX)
    })
  }

  const handleMouseUp = () => {
    isDraggingSplit.value = false
    if (animationFrame) {
      cancelAnimationFrame(animationFrame)
    }
    document.removeEventListener('mousemove', handleMouseMove)
    document.removeEventListener('mouseup', handleMouseUp)
    maybeSnapToStack(splitPercent.value)
  }

  document.addEventListener('mousemove', handleMouseMove)
  document.addEventListener('mouseup', handleMouseUp)
}

const maybeSnapToStack = (percent) => {
  const nearEdge = percent <= snapThresholdPercent || percent >= 100 - snapThresholdPercent
  if (nearEdge) {
    stackSnapActive.value = true
    layoutMode.value = 'stack'
  } else if (stackSnapActive.value && layoutMode.value === 'stack') {
    stackSnapActive.value = false
    layoutMode.value = 'split'
  }
}

watch(layoutMode, (mode) => {
  if (mode === 'split') {
    stackSnapActive.value = false
  }
})

// Panel management
const onPanelResize = (panelId, newHeight) => {
  const panel = panels.find(p => p.id === panelId)
  if (panel) panel.defaultHeight = newHeight
}

const onPanelCollapse = (panelId, collapsed) => {
  const panel = panels.find(p => p.id === panelId)
  if (panel) panel.collapsed = collapsed
}

// Composables
const { filters, currentFilter, currentFilterName, jamlContent, loadFilters, selectFilter, saveFilter, deleteFilter } = useFilters()
const { results, columns, searchStatus, isSearching, activeSearches, currentSearchId, loadActiveSearches, startSearch, stopAll, stopSearch, clearResults, exportResults } = useSearch()
const { connect, disconnect, joinSearchGroup, leaveSearchGroup, isConnected, connectionError } = useSignalR({
  onResult: (result) => {
    results.value.push(result)
  },
  onProgress: (progress) => {
    searchStatus.value = `Progress: ${progress.processed.toLocaleString()} seeds`
  },
  onSearchUpdate: (update) => {
    // Handle search updates
    const searchIndex = activeSearches.value.findIndex(s => s.searchId === update.searchId)
    if (searchIndex >= 0) {
      activeSearches.value[searchIndex] = { ...activeSearches.value[searchIndex], ...update }
    } else {
      activeSearches.value.push(update)
    }
  }
})
const { error: globalError, showError, dismissError } = useGlobalError()

// Event handlers
const goHome = () => window.location.href = '/'
const toggleSettings = () => showSettings.value = !showSettings.value

const handleSelectFilter = async (filter) => {
  await selectFilter(filter)
  showSettings.value = false
}

const handleDeleteFilter = async (filter) => {
  await deleteFilter(filter)
}

const handleSaveFilter = async (jaml) => {
  const success = await saveFilter(jaml)
  if (success) {
    // Show success feedback
  }
}

const handleStartSearch = async (jaml) => {
  const searchId = await startSearch(jaml)
  if (searchId) {
    await joinSearchGroup(searchId)
  }
}

const handleStopSearch = async () => {
  await stopAll()
}

const handleStopSpecificSearch = async (searchId) => {
  await leaveSearchGroup(searchId)
  await stopSearch(searchId)
}

const updateJamlContent = (newJaml) => {
  jamlContent.value = newJaml
}

// Lifecycle
onMounted(async () => {
  const saved = localStorage.getItem('jaml-layout-mode')
  if (saved === 'stack' || saved === 'split') {
    layoutMode.value = saved
  } else {
    updateLayoutMode()
  }
  window.addEventListener('resize', updateLayoutMode)

  // Load data with timeout to prevent hanging
  const loadWithTimeout = async (fn, timeout = 3000) => {
    return Promise.race([
      fn(),
      new Promise((_, reject) => 
        setTimeout(() => reject(new Error('Load timeout')), timeout)
      )
    ]).catch(err => {
      console.warn('Data load failed (continuing anyway):', err?.message)
    })
  }

  await Promise.all([
    loadWithTimeout(() => loadFilters()),
    loadWithTimeout(() => loadActiveSearches())
  ])

  // Connect SignalR without blocking UI
  connect().catch(err => {
    console.warn('SignalR connection failed (non-critical):', err?.message)
  })
})

onUnmounted(() => {
  window.removeEventListener('resize', updateLayoutMode)
  localStorage.setItem('jaml-layout-mode', layoutMode.value)
  disconnect()
})
</script>

<style scoped>
:global(body) {
  position: relative;
  min-height: 100vh;
  background: radial-gradient(circle at 20% 20%, rgba(234, 186, 68, 0.25), transparent 45%),
    radial-gradient(circle at 80% 0%, rgba(79, 115, 255, 0.18), transparent 35%),
    var(--bg);
  color: var(--text);
  font-family: 'm6x11plus', 'Courier New', monospace;
  overflow-y: auto;
}

:global(body)::before {
  content: '';
  position: fixed;
  inset: 0;
  background: linear-gradient(135deg, rgba(30, 46, 63, 0.25), rgba(7, 9, 14, 0.6));
  pointer-events: none;
  mix-blend-mode: screen;
  z-index: -1;
}

.jaml-ui {
  min-height: 100vh;
  background: rgba(0, 0, 0, 0.4);
  padding-top: 32px;
  position: relative;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  border-right: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: inset 0 0 40px rgba(0, 0, 0, 0.6), 0 30px 60px rgba(0, 0, 0, 0.45);
}
/* Navigation links */
.nav-link {
  text-decoration: none;
  color: inherit;
}

.nav-link button {
  background: none;
  border: none;
  color: white;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.nav-link button:hover {
  background: rgba(255, 255, 255, 0.1);
}

/* Top bar styles */
.top-bar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 38px;
  background: linear-gradient(90deg, rgba(46, 55, 68, 0.85), rgba(21, 30, 39, 0.85));
  backdrop-filter: blur(16px);
  border-bottom: 1px solid rgba(255, 255, 255, 0.08);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  z-index: 1000;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.55);
}

.top-bar-center {
  position: absolute;
  top: 4px;
  height: 24px;
  background: linear-gradient(120deg, rgba(60, 74, 93, 0.95), rgba(20, 26, 33, 0.9));
  backdrop-filter: blur(10px);
  border-radius: 12px;
  border: 1px solid rgba(234, 186, 68, 0.4);
  display: flex;
  align-items: center;
  padding: 0 12px;
  cursor: ew-resize;
  user-select: none;
  z-index: 100;
  box-shadow: 0 4px 25px rgba(234, 186, 68, 0.35);
}

.top-bar-center::after {
  content: '';
  position: absolute;
  bottom: -4px;
  left: calc(50% - 1px);
  width: 2px;
  height: 4px;
  background: rgba(185, 194, 210, 0.4);
}

.top-bar-center:hover {
  background: rgba(42, 45, 58, 0.95);
  border-color: rgba(185, 194, 210, 0.3);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
}

.top-bar-center.dragging {
  background: rgba(42, 45, 58, 1);
  border-color: rgba(255, 75, 64, 0.5);
  box-shadow: 0 2px 8px rgba(255, 75, 64, 0.3);
}

.top-bar-center.edge-left {
  border-radius: 999px 0 0 999px;
}

.top-bar-center.edge-right {
  border-radius: 0 999px 999px 0;
}

.layout-controls {
  display: flex;
  align-items: center;
  cursor: ew-resize;
  user-select: none;
  gap: 6px;
}

.layout-controls.dragging {
  opacity: 0.85;
}

.layout-indicator {
  font-size: 12px;
  opacity: 0.7;
  color: var(--text-color, white);
  padding: 0 6px;
  border-radius: 999px;
  border: 1px solid rgba(234, 186, 68, 0.5);
  background: rgba(234, 186, 68, 0.08);
}

.top-bar-title {
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  font-weight: normal;
  color: white;
  text-shadow: 1px 1px 2px rgba(0, 0, 0, 0.3);
  margin: 0 6px;
}

.slider-icon {
  background: none;
  border: none;
  color: white;
  font-size: 12px;
  cursor: grab;
  padding: 0;
  opacity: 0.8;
  transition: opacity 0.2s ease;
}

.slider-icon:hover {
  opacity: 1;
}

.main-layout {
  flex: 1;
  display: flex;
  overflow: hidden;
  position: relative;
  transition: flex-direction 0.3s ease;
}

.split-pane {
  display: flex;
  height: 100%;
  position: relative;
}

.split-pane-vertical {
  flex-direction: row;
}

.split-pane-horizontal {
  flex-direction: column;
}

.split-pane-left,
.split-pane-right {
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.split-pane-left {
  flex: 0 0 50%;
}

.layout-stack {
  flex-direction: column;
}

.split-column {
  flex: 1;
  min-width: 0;
  max-width: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: 0 6px;
  position: relative;
  border-radius: 0 0 12px 12px;
  background: linear-gradient(180deg, rgba(0, 0, 0, 0), rgba(0, 0, 0, 0.35));
  box-shadow: inset 0 0 15px rgba(0, 0, 0, 0.35);
  transition: flex 0.15s ease-out;
}

.split-divider {
  position: absolute;
  top: 34px;
  height: calc(100vh - 34px);
  width: 8px;
  cursor: ew-resize;
  border-radius: 4px;
  background: var(--gold);
  box-shadow: var(--shadow);
  transition: width 0.2s ease, background-color 0.2s ease;
  will-change: width;
}

.split-divider:hover {
  width: 10px;
  background: var(--balatro-gold);
}

.divider-handle {
  display: none;
}
</style>

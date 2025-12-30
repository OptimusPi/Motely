<template>
  <div class="jaml-ui">
    <!-- Panel tabs at top edge -->
    <div class="tab-overflow-row">
      <template v-for="(panel, index) in topVisiblePanels" :key="panel.id">
        <div
          class="panel-tab-inline"
          :class="`panel-tab-${panel.color}`"
        >
          <span class="tab-label">{{ panel.label }}</span>
          <span v-if="panel.badge" class="tab-badge">{{ panel.badge }}</span>
        </div>
      </template>
    </div>

    <!-- Panels -->
    <div 
      class="main-layout" 
      :class="`layout-${layoutMode}`"
    >
      <div v-if="layoutMode === 'stack'" ref="stackContainer" class="layout-stack">
        <template v-for="(panel, index) in panels" :key="panel.id">
          <div
            v-if="index > 0"
            class="stack-divider"
            @pointerdown="startStackResize(index - 1, $event)"
          ></div>

          <PanelSection
            :color="panel.color"
            :label="panel.label"
            :badge="panel.badge"
            :min-height="panel.minHeight"
            :default-height="panel.defaultHeight"
            :layout-mode="layoutMode"
            :fill-remaining="index === panels.length - 1"
            :show-top-grab="false"
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
        </template>
      </div>

      <div v-else ref="splitContainer" class="layout-split">
        <div ref="leftColumnContainer" class="split-column split-left" :style="{ width: splitLeftWidth + '%' }">
          <template v-for="(panel, index) in leftPanels" :key="panel.id">
            <div
              v-if="index > 0"
              class="stack-divider"
              @pointerdown="startColumnResize('left', index - 1, $event)"
            ></div>

            <PanelSection
              :color="panel.color"
              :label="panel.label"
              :badge="panel.badge"
              :min-height="panel.minHeight"
              :default-height="panel.defaultHeight"
              :layout-mode="'stack'"
              :fill-remaining="index === leftPanels.length - 1"
              :show-top-grab="false"
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
          </template>
        </div>

        <div class="split-divider" @pointerdown="startSplitResize">
          <div 
            class="jaml-badge"
            :class="badgeSnapClass"
          >
            <GripVertical v-if="badgeSnapState !== 'left'" :size="16" />
            <Home :size="16" @click.stop="goHome" class="icon-btn" />
            <span class="logo">JAML</span>
            <Settings :size="16" @click.stop="toggleSettings" class="icon-btn" />
            <GripVertical v-if="badgeSnapState !== 'right'" :size="16" />
          </div>
        </div>

        <div ref="rightColumnContainer" class="split-column split-right" :style="{ width: (100 - splitLeftWidth) + '%' }">
          <template v-for="(panel, index) in rightPanels" :key="panel.id">
            <div
              v-if="index > 0"
              class="stack-divider"
              @pointerdown="startColumnResize('right', index - 1, $event)"
            ></div>

            <PanelSection
              :color="panel.color"
              :label="panel.label"
              :badge="panel.badge"
              :min-height="panel.minHeight"
              :default-height="panel.defaultHeight"
              :layout-mode="'split'"
              :fill-remaining="index === rightPanels.length - 1"
              :show-top-grab="false"
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
          </template>
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
import { ref, computed, onMounted, onUnmounted, watch, reactive, markRaw } from 'vue'
import { Home, Settings, GripVertical } from 'lucide-vue-next'
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
    component: markRaw(EditorPanel)
  },
  {
    id: 'blueprint',
    color: 'blue',
    label: 'Blueprint Analyzer',
    minHeight: 220,
    defaultHeight: 320,
    component: markRaw(BlueprintPanel)
  },
  {
    id: 'active-searches',
    color: 'green',
    label: 'Active Searches',
    minHeight: 200,
    defaultHeight: 240,
    component: markRaw(ActiveSearchesPanel)
  },
  {
    id: 'results',
    color: 'purple',
    label: 'Results',
    minHeight: 260,
    defaultHeight: 360,
    component: markRaw(ResultsPanel)
  }
])

const leftPanels = computed(() => panels.slice(0, 2))
const rightPanels = computed(() => panels.slice(2))
const topVisiblePanels = computed(() => {
  // For split layout, show tabs from left column's first panel
  // For stack layout, show tab from first panel
  if (layoutMode.value === 'split') {
    return leftPanels.value.slice(0, 1)
  }
  return panels.slice(0, 1)
})

const showSettings = ref(false)
const splitLeftWidth = ref(50)

// Badge positioning state
const badgeSnapState = ref('center') // 'left', 'center', 'right'
const badgeSnapClass = computed(() => `badge-snap-${badgeSnapState.value}`)

const SNAP_THRESHOLD = 100

const stackContainer = ref(null)
const splitContainer = ref(null)
const leftColumnContainer = ref(null)
const rightColumnContainer = ref(null)

const STACK_DIVIDER_HEIGHT_PX = 4

const layoutMode = computed(() => {
  // Force single column when snapped left or right
  if (badgeSnapState.value === 'left' || badgeSnapState.value === 'right') {
    return 'stack'
  }
  return 'split'
})
const clamp = (value, min, max) => Math.max(min, Math.min(max, value))

const startSplitResize = (event) => {
  if (event.button !== 0) return
  event.preventDefault()

  const dividerEl = event.currentTarget
  dividerEl?.setPointerCapture?.(event.pointerId)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'

  const updateFromPointer = (clientX) => {
    const rect = splitContainer.value?.getBoundingClientRect?.()
    if (!rect || rect.width <= 0) return
    const percent = ((clientX - rect.left) / rect.width) * 100
    splitLeftWidth.value = clamp(percent, 0, 100)
  }

  const onMove = (moveEvent) => {
    updateFromPointer(moveEvent.clientX)
  }

  const onUp = () => {
    dividerEl?.releasePointerCapture?.(event.pointerId)
    dividerEl?.removeEventListener?.('pointermove', onMove)
    dividerEl?.removeEventListener?.('pointerup', onUp)
    dividerEl?.removeEventListener?.('pointercancel', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''

    // Snap logic on release
    if (splitLeftWidth.value < 10) {
      badgeSnapState.value = 'left'
      splitLeftWidth.value = 0
    } else if (splitLeftWidth.value > 90) {
      badgeSnapState.value = 'right'
      splitLeftWidth.value = 100
    } else {
      badgeSnapState.value = 'center'
    }
  }

  dividerEl?.addEventListener?.('pointermove', onMove)
  dividerEl?.addEventListener?.('pointerup', onUp)
  dividerEl?.addEventListener?.('pointercancel', onUp)
  updateFromPointer(event.clientX)
}

const computeMaxHeightForIndex = (stackPanels, resizeIndex, containerHeight) => {
  let minBelow = 0
  for (let i = resizeIndex + 1; i < stackPanels.length; i++) {
    minBelow += stackPanels[i].minHeight
  }
  const dividerCountBelow = stackPanels.length - 1 - resizeIndex
  const dividerSpace = dividerCountBelow * STACK_DIVIDER_HEIGHT_PX
  return containerHeight - minBelow - dividerSpace
}

const startStackResize = (resizeIndex, event) => {
  if (event.button !== 0) return
  event.preventDefault()

  const dividerEl = event.currentTarget
  dividerEl?.setPointerCapture?.(event.pointerId)
  dividerEl?.classList?.add?.('is-dragging')
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'

  const stackPanels = panels
  const containerHeight = stackContainer.value?.getBoundingClientRect?.().height
  if (!containerHeight) return

  const startY = event.clientY
  const startHeight = stackPanels[resizeIndex].defaultHeight

  const onMove = (moveEvent) => {
    const desired = startHeight + (moveEvent.clientY - startY)
    stackPanels[resizeIndex].defaultHeight = desired
  }

  const onUp = () => {
    dividerEl?.releasePointerCapture?.(event.pointerId)
    dividerEl?.classList?.remove?.('is-dragging')
    dividerEl?.removeEventListener?.('pointermove', onMove)
    dividerEl?.removeEventListener?.('pointerup', onUp)
    dividerEl?.removeEventListener?.('pointercancel', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }

  dividerEl?.addEventListener?.('pointermove', onMove)
  dividerEl?.addEventListener?.('pointerup', onUp)
  dividerEl?.addEventListener?.('pointercancel', onUp)
}

const startColumnResize = (side, resizeIndex, event) => {
  if (event.button !== 0) return
  event.preventDefault()

  const dividerEl = event.currentTarget
  dividerEl?.setPointerCapture?.(event.pointerId)
  dividerEl?.classList?.add?.('is-dragging')
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'

  const columnPanels = side === 'left' ? leftPanels.value : rightPanels.value
  const containerEl = side === 'left' ? leftColumnContainer.value : rightColumnContainer.value
  const containerHeight = containerEl?.getBoundingClientRect?.().height
  if (!containerHeight) return

  const startY = event.clientY
  const startHeight = columnPanels[resizeIndex].defaultHeight

  const onMove = (moveEvent) => {
    const desired = startHeight + (moveEvent.clientY - startY)
    columnPanels[resizeIndex].defaultHeight = desired
  }

  const onUp = () => {
    dividerEl?.releasePointerCapture?.(event.pointerId)
    dividerEl?.classList?.remove?.('is-dragging')
    dividerEl?.removeEventListener?.('pointermove', onMove)
    dividerEl?.removeEventListener?.('pointerup', onUp)
    dividerEl?.removeEventListener?.('pointercancel', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }

  dividerEl?.addEventListener?.('pointermove', onMove)
  dividerEl?.addEventListener?.('pointerup', onUp)
  dividerEl?.addEventListener?.('pointercancel', onUp)
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

const onPanelResize = (panelId, newHeight) => {
  // Panel resize handled internally by PanelSection
  // This handler can be used for future analytics/persistence
}

const onPanelCollapse = (panelId, isCollapsed) => {
  // Panel collapse handled internally by PanelSection
  // This handler can be used for future analytics/persistence
}

// Lifecycle
onMounted(async () => {
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
  disconnect()
})
</script>

<style scoped>
:global(html, body) {
  margin: 0;
  padding: 0;
  height: 100vh;
  overflow: hidden;
}

:global(body) {
  position: relative;
  background: var(--bg);
  color: var(--text);
  font-family: 'm6x11plus', 'Courier New', monospace;
}

:global(:root) {
  --balatro-red: #ff4c40;
  --balatro-blue: #0093ff;
  --balatro-green: #429f79;
  --balatro-purple: #9b59b6;
  --balatro-gold: #eaba44;
}

.jaml-ui {
  height: 100vh;
  overflow: hidden;
  background: rgba(0, 0, 0, 0.4);
  position: relative;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  border-right: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: inset 0 0 40px rgba(0, 0, 0, 0.6), 0 30px 60px rgba(0, 0, 0, 0.45);
}

.tab-overflow-row {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  display: flex;
  align-items: flex-start;
  gap: 2px;
  padding: 0 4px;
  background: transparent;
  height: 28px;
  overflow-x: auto;
  overflow-y: hidden;
  z-index: 1199;
  pointer-events: none;
}

.main-layout {
  display: flex;
  position: relative;
  padding: 28px 0 0 0;
  box-sizing: border-box;
  height: 100vh;
  overflow: hidden;
  margin-top: 0;
}

.layout-stack {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
}

.stack-divider {
  height: 4px;
  min-height: 4px;
  cursor: ns-resize;
  background: transparent;
  flex-shrink: 0;
  touch-action: none;
  user-select: none;
}

.stack-divider:hover,
.stack-divider.is-dragging {
  background: rgba(234, 186, 68, 0.65);
}

.layout-split {
  display: flex;
  width: 100%;
}

.split-column {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}

.split-divider {
  width: 12px;
  min-width: 12px;
  cursor: ew-resize;
  background: #d4851f;
  flex-shrink: 0;
  position: relative;
  touch-action: none;
  user-select: none;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 0;
}

.split-divider:hover {
  background: var(--balatro-gold);
}

.jaml-badge {
  position: sticky;
  top: 0;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  font-weight: normal;
  padding: 4px 10px;
  background: rgba(50, 60, 70, 0.95);
  color: #fff;
  height: 28px;
  box-sizing: border-box;
  user-select: none;
  border-radius: 0 0 8px 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  pointer-events: none;
}

.jaml-badge.badge-snap-left {
  border-radius: 0 0 8px 0;
}

.jaml-badge.badge-snap-right {
  border-radius: 0 0 0 8px;
}

.jaml-badge.badge-snap-center {
  border-radius: 0 0 8px 8px;
}

.jaml-badge .logo {
  letter-spacing: 1px;
  font-weight: 600;
}

.jaml-badge .icon-btn {
  cursor: pointer;
  opacity: 0.8;
  pointer-events: auto;
}

.jaml-badge .icon-btn:hover {
  opacity: 1;
}

.logo-btn {
  background: none;
  border: none;
  cursor: pointer;
  font-size: 14px;
  padding: 2px;
  opacity: 0.8;
  color: white;
}

.logo-btn:hover {
  opacity: 1;
}

.tab-overflow-row {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  display: flex;
  align-items: flex-start;
  gap: 2px;
  padding: 0 4px;
  background: transparent;
  height: 28px;
  overflow-x: auto;
  overflow-y: hidden;
  pointer-events: none;
}

.panel-tab-inline {
  height: 28px;
  padding: 0 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--panel-color);
  border-radius: 4px 4px 0 0;
  color: #fff;
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  font-weight: normal;
  cursor: pointer;
  user-select: none;
  white-space: nowrap;
  flex-shrink: 0;
  box-sizing: border-box;
  pointer-events: auto;
}

.panel-tab-inline:hover {
  filter: brightness(1.1);
}

.panel-tab-inline .tab-label {
  display: inline;
}

.panel-tab-inline .tab-badge {
  margin-left: 8px;
  background: rgba(0, 0, 0, 0.2);
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
}

.panel-tab-red {
  background: var(--balatro-red);
  --panel-color: var(--balatro-red);
}

.panel-tab-blue {
  background: var(--balatro-blue);
  --panel-color: var(--balatro-blue);
}

.panel-tab-green {
  background: var(--balatro-green);
  --panel-color: var(--balatro-green);
}

.panel-tab-purple {
  background: var(--balatro-purple);
  --panel-color: var(--balatro-purple);
}
</style>

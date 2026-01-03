<template>
  <div class="jaml-ui">
    <!-- Panels (tabs are part of each panel) -->
    <div 
      class="main-layout" 
      :class="`layout-${layoutMode}`"
    >
      <div v-if="layoutMode === 'stack'" ref="stackContainer" class="layout-stack">
        <template v-for="(panel, index) in panels" :key="panel.id">
          <PanelSection
            v-if="!isMobile || getMobilePanelVisibility(panel.id)"
            :color="panel.color"
            :label="panel.label"
            :badge="panel.badge"
            :min-height="panel.minHeight"
            :default-height="panel.defaultHeight"
            :layout-mode="layoutMode"
            :fill-remaining="index === panels.length - 1"
            @resize="onPanelResize(panel.id, $event)"
            @collapse="onPanelCollapse(panel.id, $event)"
            @top-drag="(e) => { if (index > 0 && !isMobile) startStackResize(index - 1, e) }"
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

      <div v-else ref="splitContainer" class="layout-split" :class="{ 'mobile-hidden': isMobile }">
        <div ref="leftColumnContainer" class="split-column split-left" :style="{ width: splitLeftWidth + '%' }">
          <template v-for="(panel, index) in leftPanels" :key="panel.id">
            <PanelSection
              :color="panel.color"
              :label="panel.label"
              :badge="panel.badge"
              :min-height="panel.minHeight"
              :default-height="panel.defaultHeight"
              :layout-mode="'stack'"
              :fill-remaining="index === leftPanels.length - 1"
              @resize="onPanelResize(panel.id, $event)"
              @collapse="onPanelCollapse(panel.id, $event)"
              @top-drag="(e) => { if (index > 0 && !isMobile) startColumnResize('left', index - 1, e) }"
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

        <div v-if="!isMobile" class="split-divider" @pointerdown="startSplitResize">
          <div 
            class="jaml-badge"
            :class="badgeSnapClass"
            @pointerdown.stop
          >
            <GripVertical v-if="badgeSnapState !== 'left'" :size="16" />
            <Home :size="16" @click.stop.prevent="goHome" class="icon-btn" />
            <span class="logo">JAML</span>
            <Settings :size="16" @click.stop.prevent="toggleSettings" class="icon-btn" />
            <GripVertical v-if="badgeSnapState !== 'right'" :size="16" />
          </div>
        </div>

        <div ref="rightColumnContainer" class="split-column split-right" :style="{ width: (100 - splitLeftWidth) + '%' }">
          <template v-for="(panel, index) in rightPanels" :key="panel.id">
            <PanelSection
              :color="panel.color"
              :label="panel.label"
              :badge="panel.badge"
              :min-height="panel.minHeight"
              :default-height="panel.defaultHeight"
              :layout-mode="'split'"
              :fill-remaining="index === rightPanels.length - 1"
              @resize="onPanelResize(panel.id, $event)"
              @collapse="onPanelCollapse(panel.id, $event)"
              @top-drag="(e) => { if (index > 0 && !isMobile) startColumnResize('right', index - 1, e) }"
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

    <!-- Mobile Bottom Navigation -->
    <div v-if="isMobile" class="mobile-nav">
      <button 
        @click="activePanel = 'editor'"
        :class="['nav-btn', { active: activePanel === 'editor' }]"
        aria-label="Editor"
      >
        <span class="nav-icon">📝</span>
        <span class="nav-label">Editor</span>
      </button>
      <button 
        @click="activePanel = 'searches'"
        :class="['nav-btn', { active: activePanel === 'searches' }]"
        aria-label="Active Searches"
      >
        <span class="nav-icon">🔍</span>
        <span class="nav-label">Searches</span>
      </button>
      <button 
        @click="activePanel = 'results'"
        :class="['nav-btn', { active: activePanel === 'results' }]"
        aria-label="Results"
      >
        <span class="nav-icon">📊</span>
        <span class="nav-label">Results</span>
      </button>
      <button 
        @click="toggleSettings"
        :class="['nav-btn', { active: showSettings }]"
        aria-label="Settings"
      >
        <span class="nav-icon">⚙️</span>
        <span class="nav-label">Settings</span>
      </button>
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
import { useLayout } from '../composables/useLayout'

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
  // Show ALL panel tabs at the top
  return panels
})

const showSettings = ref(false)
const splitLeftWidth = ref(50)
const activePanel = ref('editor') // For mobile navigation

// Badge positioning state
const badgeSnapState = ref('center') // 'left', 'center', 'right'
const badgeSnapClass = computed(() => `badge-snap-${badgeSnapState.value}`)

const SNAP_THRESHOLD = 100

const stackContainer = ref(null)
const splitContainer = ref(null)
const leftColumnContainer = ref(null)
const rightColumnContainer = ref(null)

const STACK_DIVIDER_HEIGHT_PX = 4

// Mobile detection
const { windowWidth } = useLayout()
const isMobile = computed(() => windowWidth.value < 768)

const layoutMode = computed(() => {
  // Force stack layout on mobile
  if (isMobile.value) {
    return 'stack'
  }
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

let isStackDragging = false
let stackResizeIndex = -1
let stackStartY = 0
let stackStartHeight = 0

const startStackResize = (resizeIndex, event) => {
  if (event.button !== 0 && event.type !== 'touchstart') return
  event.preventDefault()
  event.stopPropagation()

  isStackDragging = true
  stackResizeIndex = resizeIndex

  const stackPanels = panels
  if (!stackPanels[resizeIndex]) return

  const dividerEl = event.currentTarget
  dividerEl?.classList?.add?.('is-dragging')
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'

  // Get starting position (works for both mouse and touch)
  stackStartY = event.clientY || (event.touches && event.touches[0]?.clientY) || 0
  stackStartHeight = stackPanels[resizeIndex].defaultHeight || stackPanels[resizeIndex].minHeight || 200

  // Use document-level listeners for smooth dragging (like SplitPane)
  document.addEventListener('mousemove', handleStackMove)
  document.addEventListener('touchmove', handleStackMove, { passive: false })
  document.addEventListener('mouseup', handleStackEnd)
  document.addEventListener('touchend', handleStackEnd)
  document.addEventListener('touchcancel', handleStackEnd)
}

const handleStackMove = (moveEvent) => {
  if (!isStackDragging || stackResizeIndex < 0) return

  const stackPanels = panels
  if (!stackPanels[stackResizeIndex]) return

  // Get current position (works for both mouse and touch)
  const currentY = moveEvent.clientY || (moveEvent.touches && moveEvent.touches[0]?.clientY) || 0
  const deltaY = currentY - stackStartY

  // NO LIMITATIONS - let it drag freely!
  const newHeight = stackStartHeight + deltaY
  stackPanels[stackResizeIndex].defaultHeight = newHeight

  moveEvent.preventDefault()
}

const handleStackEnd = () => {
  if (!isStackDragging) return

  isStackDragging = false
  stackResizeIndex = -1

  // Remove all dragging classes
  document.querySelectorAll('.stack-divider.is-dragging').forEach(el => {
    el.classList.remove('is-dragging')
  })

  document.body.style.cursor = ''
  document.body.style.userSelect = ''

  // Remove document-level listeners
  document.removeEventListener('mousemove', handleStackMove)
  document.removeEventListener('touchmove', handleStackMove)
  document.removeEventListener('mouseup', handleStackEnd)
  document.removeEventListener('touchend', handleStackEnd)
  document.removeEventListener('touchcancel', handleStackEnd)
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

// Mobile panel visibility helper
const getMobilePanelVisibility = (panelId) => {
  if (!isMobile.value) return true
  const panelMap = {
    'jaml-editor': 'editor',
    'blueprint': 'editor',
    'active-searches': 'searches',
    'results': 'results'
  }
  return panelMap[panelId] === activePanel.value
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
  background: var(--bg-color);
  color: var(--text-color);
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

/* Tab overflow row removed - tabs are now part of each panel */

.main-layout {
  display: flex;
  position: relative;
  padding: 0;
  box-sizing: border-box;
  height: 100vh;
  max-height: 100vh; /* Never exceed screen */
  overflow: hidden;
  margin: 0;
}

.layout-stack {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
  gap: 0; /* No gaps - panels touch */
}

/* Stack dividers removed - panels touch each other, top border acts as drag handle */

.layout-split {
  display: flex;
  width: 100%;
}

.split-column {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  gap: 0; /* No gaps - panels touch */
  min-height: 0; /* Force panels to fit */
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
  z-index: 2000; /* High z-index to be above panels, but below modals (10000) */
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
  z-index: 2001; /* Even higher to ensure badge is visible on top of divider */
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

/* Tab overflow row removed - tabs are now part of each panel */

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

/* Mobile Navigation */
.mobile-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  display: flex;
  background: var(--panel-bg);
  border-top: 2px solid var(--border-color);
  z-index: 1000;
  padding: 8px 0;
  box-shadow: 0 -2px 10px rgba(0, 0, 0, 0.3);
}

.nav-btn {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  padding: 8px 4px;
  background: transparent;
  border: none;
  color: var(--muted);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s;
  min-height: 60px; /* Ensure 44px+ touch target */
}

.nav-btn:active {
  background: var(--dark-bg);
}

.nav-btn.active {
  color: var(--balatro-gold);
}

.nav-icon {
  font-size: 20px;
}

.nav-label {
  font-size: 11px;
  font-weight: normal;
}

.mobile-hidden {
  display: none;
}

/* Adjust layout for mobile nav */
@media (max-width: 767px) {
  .jaml-ui {
    padding-bottom: 60px; /* Space for bottom nav */
  }
  
  .main-layout {
    height: calc(100vh - 60px);
  }
}
</style>

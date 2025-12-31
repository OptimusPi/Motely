<template>
  <div class="jaml-ui">
    <!-- Panel tabs at top edge -->
    <div ref="tabRow" class="tab-overflow-row">
      <div class="tab-shelf tab-shelf-left">
        <template v-for="panel in leftTabPanels" :key="panel.id">
          <div
            class="panel-tab-inline"
            :class="`panel-tab-${panel.color}`"
            :data-panel-id="panel.id"
          >
            <span class="tab-label">{{ panel.label }}</span>
            <span v-if="panel.badge" class="tab-badge">{{ panel.badge }}</span>
          </div>
        </template>
      </div>

      <div class="tab-shelf tab-shelf-right">
        <template v-for="panel in rightTabPanels" :key="panel.id">
          <div
            class="panel-tab-inline"
            :class="`panel-tab-${panel.color}`"
            :data-panel-id="panel.id"
          >
            <span class="tab-label">{{ panel.label }}</span>
            <span v-if="panel.badge" class="tab-badge">{{ panel.badge }}</span>
          </div>
        </template>
      </div>
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
            :style="badgeStyle"
            @pointerdown.stop="startSplitResizeFromBadge"
            ref="badgeEl"
          >
            <GripVertical v-if="badgeSnapState !== 'left'" :size="16" />
            <Home :size="16" @click.stop="goHome" @pointerdown.stop class="icon-btn" />
            <span class="logo">JAML</span>
            <Settings :size="16" @click.stop="toggleSettings" @pointerdown.stop class="icon-btn" />
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
import { ref, computed, onMounted, onUnmounted, watch, reactive, markRaw, nextTick } from 'vue'
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
  // Show all panels in tabs
  return panels
})

const showSettings = ref(false)
const splitLeftWidth = ref(50)

// Badge positioning state
const badgeSnapState = ref('center') // 'left', 'center', 'right'
const badgeSnapClass = computed(() => `badge-snap-${badgeSnapState.value}`)

const TOP_TABS_PX = 28 // phone notch margin; tabs live at top: 28px and are 28px tall

const tabRow = ref(null)
const badgeEl = ref(null)
const badgeForceSide = ref(null) // legacy (no longer used for positioning)
const BADGE_EDGE_GAP_PX = 8

let badgeRecalcPending = false
const requestBadgeRecalc = () => {
  if (badgeRecalcPending) return
  badgeRecalcPending = true
  requestAnimationFrame(() => {
    badgeRecalcPending = false
    recomputeBadgeForceSide()
  })
}

const recomputeBadgeForceSide = () => {
  // Only avoid overlap while centered; snapped modes should be stable.
  if (badgeSnapState.value !== 'center') {
    badgeForceSide.value = null
    return
  }
  if (!tabRow.value || !badgeEl.value || typeof window === 'undefined') {
    badgeForceSide.value = null
    return
  }

  const badgeRect = badgeEl.value.getBoundingClientRect?.()
  const badgeWidth = badgeRect?.width || 120

  // Theoretical centered badge bounds (avoid depending on forced-side rendering)
  const centerX = (splitLeftWidth.value / 100) * window.innerWidth
  const badgeLeft = centerX - badgeWidth / 2
  const badgeRight = centerX + badgeWidth / 2

  let overlapsAnyTab = false
  const tabs = tabRow.value.querySelectorAll?.('.panel-tab-inline') || []
  for (const tab of tabs) {
    const r = tab.getBoundingClientRect?.()
    if (!r || r.width <= 0) continue
    // Tabs and badge share the same Y band, so horizontal overlap is the important part.
    if (badgeRight > r.left && badgeLeft < r.right) {
      overlapsAnyTab = true
      break
    }
  }

  if (!overlapsAnyTab) {
    badgeForceSide.value = null
    return
  }

  // Move to opposite side of the divider when it would cover a tab.
  badgeForceSide.value = splitLeftWidth.value < 50 ? 'right' : 'left'
}

// Badge style - always attached to the divider (never teleports as an overlap avoidance mechanism).
const badgeStyle = computed(() => {
  const base = { top: `${TOP_TABS_PX}px` }

  // Position by divider percentage
  const leftPercent = splitLeftWidth.value
  const anchored = { ...base, left: `${leftPercent}%` }

  // Flagpole feel at extremes: keep badge visible/draggable when divider is at 0%/100%.
  if (badgeSnapState.value === 'left') {
    return { ...anchored, transform: `translateX(${BADGE_EDGE_GAP_PX}px)` }
  }
  if (badgeSnapState.value === 'right') {
    return { ...anchored, transform: `translateX(calc(-100% - ${BADGE_EDGE_GAP_PX}px))` }
  }

  // Center: straddle the divider
  return { ...anchored, transform: 'translateX(-50%)' }
})

// --- Tab fling state (two shelves) ---
const tabSideById = reactive({}) // panelId -> 'left' | 'right'

const ensureTabSides = () => {
  for (const p of topVisiblePanels.value) {
    if (!tabSideById[p.id]) tabSideById[p.id] = 'left'
  }
}

const leftTabPanels = computed(() => {
  return topVisiblePanels.value.filter(p => tabSideById[p.id] !== 'right')
})

const rightTabPanels = computed(() => {
  return topVisiblePanels.value.filter(p => tabSideById[p.id] === 'right')
})

const TAB_HYSTERESIS_PX = 8

let tabFlingPending = false
const requestTabFlingRecalc = () => {
  if (tabFlingPending) return
  tabFlingPending = true
  requestAnimationFrame(() => {
    tabFlingPending = false
    recomputeTabSides()
  })
}

const recomputeTabSides = () => {
  ensureTabSides()
  if (!tabRow.value || typeof window === 'undefined') return

  const dividerX = (splitLeftWidth.value / 100) * window.innerWidth

  for (const p of topVisiblePanels.value) {
    const el = tabRow.value.querySelector?.(`.panel-tab-inline[data-panel-id="${p.id}"]`)
    if (!el) continue
    const r = el.getBoundingClientRect?.()
    if (!r || r.width <= 0) continue
    const tabMidX = r.left + r.width / 2

    const side = tabSideById[p.id] || 'left'
    if (side === 'left') {
      // Fire when divider crosses left of midpoint (with hysteresis)
      if (dividerX < tabMidX - TAB_HYSTERESIS_PX) tabSideById[p.id] = 'right'
    } else {
      // Fire when divider crosses right of midpoint (with hysteresis)
      if (dividerX > tabMidX + TAB_HYSTERESIS_PX) tabSideById[p.id] = 'left'
    }
  }
}

const SNAP_THRESHOLD = 100

const stackContainer = ref(null)
const splitContainer = ref(null)
const leftColumnContainer = ref(null)
const rightColumnContainer = ref(null)

const STACK_DIVIDER_HEIGHT_PX = 10

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
    requestTabFlingRecalc()
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
  requestTabFlingRecalc()
}

// Separate handler for badge drag - uses document-level listeners for smooth dragging
const startSplitResizeFromBadge = (event) => {
  if (event.button !== 0) return
  event.preventDefault()
  event.stopPropagation() // Don't trigger divider drag

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
    requestTabFlingRecalc()
    moveEvent.preventDefault()
  }

  const onUp = () => {
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

    // Remove document-level listeners
    document.removeEventListener('pointermove', onMove)
    document.removeEventListener('pointerup', onUp)
    document.removeEventListener('pointercancel', onUp)
  }

  // Use document-level listeners for smooth dragging (like stack dividers)
  document.addEventListener('pointermove', onMove)
  document.addEventListener('pointerup', onUp)
  document.addEventListener('pointercancel', onUp)
  updateFromPointer(event.clientX)
  requestTabFlingRecalc()
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

  // Badge should not cover tabs: recompute after initial layout and on resize.
  await nextTick()
  ensureTabSides()
  requestTabFlingRecalc()
  window.addEventListener('resize', requestTabFlingRecalc)
})

onUnmounted(() => {
  disconnect()
  window.removeEventListener('resize', requestTabFlingRecalc)
})

watch(
  () => [splitLeftWidth.value, badgeSnapState.value, topVisiblePanels.value?.length],
  async () => {
    await nextTick()
    ensureTabSides()
    requestTabFlingRecalc()
  }
)
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
  top: 28px; /* Start below badge */
  left: 0;
  right: 0;
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 0 4px;
  background: transparent;
  height: 28px;
  overflow-x: auto;
  overflow-y: hidden;
  z-index: 1199;
  pointer-events: none;
}

.tab-shelf {
  display: flex;
  align-items: flex-start;
  gap: 2px;
  pointer-events: none;
}

.tab-shelf-left {
  justify-content: flex-start;
  flex: 1 1 auto;
  min-width: 0;
}

.tab-shelf-right {
  justify-content: flex-end;
  flex: 1 1 auto;
  min-width: 0;
}

.main-layout {
  display: flex;
  position: relative;
  padding: 56px 0 0 0; /* Account for badge (28px) + tabs (28px) */
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
  height: 10px;
  min-height: 10px;
  cursor: ns-resize;
  background: rgba(234, 186, 68, 0.28);
  background-image: radial-gradient(rgba(0, 0, 0, 0.22) 1px, transparent 1px);
  background-size: 4px 4px;
  background-position: center;
  flex-shrink: 0;
  touch-action: none;
  user-select: none;
  margin-top: -10px; /* Pull divider up to overlap panel above, removing visual gap */
  position: relative;
  z-index: 5; /* Above panel content so it can still be dragged */
}

.stack-divider:hover,
.stack-divider.is-dragging {
  background: rgba(234, 186, 68, 0.65);
  background-image: radial-gradient(rgba(0, 0, 0, 0.28) 1px, transparent 1px);
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
  width: 10px;
  min-width: 10px;
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
  position: fixed; /* Fixed to viewport top */
  /* top is set via badgeStyle to match the tabs Y */
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  font-weight: normal;
  padding: 4px 10px;
  background: rgba(50, 60, 70, 0.42);
  -webkit-backdrop-filter: blur(10px);
  backdrop-filter: blur(10px);
  border: 1px solid rgba(255, 255, 255, 0.14);
  color: #fff;
  height: 28px;
  box-sizing: border-box;
  user-select: none;
  border-radius: 0 0 8px 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  pointer-events: auto; /* Allow dragging the badge */
  cursor: ew-resize; /* Show resize cursor for dragging */
  z-index: 2002; /* Above tabs (1199) */
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

<template>
  <div class="jaml-ui">
    <!-- Panels (tabs are part of each panel, collapsed tabs appear inline) -->
    <div 
      class="main-layout" 
      :class="`layout-${layoutMode}`"
    >
      <div v-if="layoutMode === 'stack'" ref="stackContainer" class="layout-stack">
        <template v-for="(panel, index) in panels.filter(p => !p.collapsed)" :key="panel.id">
          <PanelSection
            :color="panel.color"
            :label="getPanelLabel(panel)"
            :badge="panel.badge"
            :min-height="panel.minHeight"
            :default-height="panel.defaultHeight"
            :layout-mode="layoutMode"
            :fill-remaining="index === panels.filter(p => !p.collapsed).length - 1"
            :panel-id="panel.id"
            :can-duplicate="true"
            :tab-align="panel.side === 'left' ? 'left' : 'right'"
            @resize="onPanelResize(panel.id, $event)"
            @collapse="onPanelCollapse(panel.id, $event)"
            @duplicate="duplicatePanel(panel.id)"
            @move-to-side="(draggedId, targetSide) => movePanelToSide(draggedId, targetSide)"
            @top-drag="(e) => { if (index > 0 && !isMobile) startStackResize(panels.filter(p => !p.collapsed)[index - 1]?.id, e) }"
          >
            <component
              :is="panel.component"
              v-bind="{
                ...(panel.props || {}),
                ...(panel.baseId === 'jaml-editor' ? { jaml: jamlContent || '' } : {}),
                ...(panel.baseId === 'results' ? { 
                  results, 
                  columns, 
                  status: searchStatus, 
                  isSearching 
                } : {}),
                ...(panel.baseId === 'active-searches' ? { searches: activeSearches } : {})
              }"
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
            <PanelSection
              :color="panel.color"
              :label="getPanelLabel(panel)"
              :badge="panel.badge"
              :min-height="panel.minHeight"
              :default-height="panel.defaultHeight"
              :tab-align="'left'"
              :layout-mode="'stack'"
              :fill-remaining="index === leftPanels.length - 1"
              :panel-id="panel.id"
              :can-duplicate="true"
              @resize="onPanelResize(panel.id, $event)"
              @collapse="onPanelCollapse(panel.id, $event)"
              @duplicate="duplicatePanel(panel.id)"
              @move-to-side="(draggedId, targetSide) => movePanelToSide(draggedId, targetSide)"
              @top-drag="(e) => { if (index > 0 && !isMobile) startColumnResize('left', leftPanels[index - 1]?.id, e) }"
            >
              <component
                :is="panel.component"
                v-bind="{
                  ...(panel.props || {}),
                  ...(panel.baseId === 'jaml-editor' ? { jaml: jamlContent || '' } : {})
                }"
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
              :label="getPanelLabel(panel)"
              :badge="panel.badge"
              :min-height="panel.minHeight"
              :default-height="panel.defaultHeight"
              :tab-align="'right'"
              :layout-mode="'split'"
              :fill-remaining="index === rightPanels.length - 1"
              :panel-id="panel.id"
              :can-duplicate="true"
              @resize="onPanelResize(panel.id, $event)"
              @collapse="onPanelCollapse(panel.id, $event)"
              @duplicate="duplicatePanel(panel.id)"
              @move-to-side="(draggedId, targetSide) => movePanelToSide(draggedId, targetSide)"
              @top-drag="(e) => { if (index > 0 && !isMobile) startColumnResize('right', rightPanels[index - 1]?.id, e) }"
            >
              <component
                :is="panel.component"
                v-bind="{
                  ...(panel.props || {}),
                  ...(panel.baseId === 'results' ? { 
                    results, 
                    columns, 
                    status: searchStatus, 
                    isSearching 
                  } : {}),
                  ...(panel.baseId === 'active-searches' ? { searches: activeSearches } : {})
                }"
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
import ChatPanel from '../components/ChatPanel.vue'
import RequestsPanel from '../components/RequestsPanel.vue'
import JamlGeniePanel from '../components/JamlGeniePanel.vue'
import SettingsModal from '../components/SettingsModal.vue'
import ErrorModal from '../components/ErrorModal.vue'
import { useFilters } from '../composables/useFilters'
import { useSearch } from '../composables/useSearch'
import { useSignalR } from '../composables/useSignalR'
import { useGlobalError } from '../composables/useGlobalError'
import { useLayout } from '../composables/useLayout'

// Helper to generate unique panel IDs
let panelIdCounter = 0
const generatePanelId = (baseId) => {
  panelIdCounter++
  return `${baseId}-${panelIdCounter}`
}

// Layout state - panels now have unique IDs, filterId, side, and collapsed state
const panels = reactive([
  {
    id: generatePanelId('jaml-editor'),
    baseId: 'jaml-editor',
    color: 'red',
    label: 'JAML Editor',
    filterId: null, // Will be set when filter is selected
    side: 'left', // 'left' or 'right'
    collapsed: false,
    minHeight: 220,
    defaultHeight: 320,
    component: markRaw(EditorPanel)
  },
  {
    id: generatePanelId('blueprint'),
    baseId: 'blueprint',
    color: 'blue',
    label: 'Blueprint Analyzer',
    filterId: null,
    side: 'left',
    collapsed: false,
    minHeight: 220,
    defaultHeight: 320,
    component: markRaw(BlueprintPanel)
  },
  {
    id: generatePanelId('active-searches'),
    baseId: 'active-searches',
    color: 'green',
    label: 'Active Searches',
    filterId: null,
    side: 'right',
    collapsed: false,
    minHeight: 200,
    defaultHeight: 240,
    component: markRaw(ActiveSearchesPanel)
  },
  {
    id: generatePanelId('results'),
    baseId: 'results',
    color: 'purple',
    label: 'Search Results',
    filterId: null,
    side: 'right',
    collapsed: false,
    minHeight: 260,
    defaultHeight: 360,
    component: markRaw(ResultsPanel)
  },
  {
    id: generatePanelId('chat'),
    baseId: 'chat',
    color: 'blue',
    label: 'Chat',
    filterId: null,
    side: 'right',
    collapsed: false,
    minHeight: 200,
    defaultHeight: 300,
    component: markRaw(ChatPanel)
  },
  {
    id: generatePanelId('requests'),
    baseId: 'requests',
    color: 'green',
    label: 'API Requests',
    filterId: null,
    side: 'right',
    collapsed: false,
    minHeight: 200,
    defaultHeight: 280,
    component: markRaw(RequestsPanel)
  },
  {
    id: generatePanelId('jaml-genie'),
    baseId: 'jaml-genie',
    color: 'purple',
    label: 'JAML Genie',
    filterId: null,
    side: 'left',
    collapsed: false,
    minHeight: 300,
    defaultHeight: 400,
    component: markRaw(JamlGeniePanel)
  }
])

// Computed panel labels based on type and filterId
const getPanelLabel = (panel) => {
  if (panel.baseId === 'jaml-editor' && panel.filterId) {
    return `${panel.filterId}.jaml`
  } else if (panel.baseId === 'results') {
    return 'Search Results'
  } else if (panel.baseId === 'blueprint') {
    return 'Analyze Seed'
  } else if (panel.baseId === 'chat') {
    return 'Chat'
  } else if (panel.baseId === 'requests') {
    return 'API Requests'
  } else if (panel.baseId === 'jaml-genie') {
    return '🧞‍♂️ JAML Genie'
  } else {
    return panel.label
  }
}

const leftPanels = computed(() => panels.filter(p => p.side === 'left' && !p.collapsed))
const rightPanels = computed(() => panels.filter(p => p.side === 'right' && !p.collapsed))
const collapsedPanels = computed(() => panels.filter(p => p.collapsed))
const collapsedLeftPanels = computed(() => panels.filter(p => p.side === 'left' && p.collapsed))
const collapsedRightPanels = computed(() => panels.filter(p => p.side === 'right' && p.collapsed))
const leftAnchorPanel = computed(() => leftPanels.value[0] || null)
const rightAnchorPanel = computed(() => rightPanels.value[0] || null)
const topVisiblePanels = computed(() => collapsedPanels.value)

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

// Removed STACK_DIVIDER_HEIGHT_PX - no dividers, tabs are the resize handles

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

// Removed computeMaxHeightForIndex - no limits needed

let isStackDragging = false
let stackResizePanelId = null
let stackStartY = 0
let stackStartHeight = 0

const startStackResize = (panelId, event) => {
  if (event.button !== 0 && event.type !== 'touchstart') return
  if (!panelId) return
  event.preventDefault()
  event.stopPropagation()

  const stackPanels = panels.filter(p => !p.collapsed)
  const resizingPanel = stackPanels.find(p => p.id === panelId)
  if (!resizingPanel) return

  isStackDragging = true
  stackResizePanelId = panelId

  const dividerEl = event.currentTarget
  dividerEl?.classList?.add?.('is-dragging')
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'

  // Get starting position (works for both mouse and touch)
  stackStartY = event.clientY || (event.touches && event.touches[0]?.clientY) || 0
  stackStartHeight = resizingPanel.defaultHeight || resizingPanel.minHeight || 200

  // Use document-level listeners for smooth dragging (like SplitPane)
  document.addEventListener('mousemove', handleStackMove)
  document.addEventListener('touchmove', handleStackMove, { passive: false })
  document.addEventListener('mouseup', handleStackEnd)
  document.addEventListener('touchend', handleStackEnd)
  document.addEventListener('touchcancel', handleStackEnd)
}

const handleStackMove = (moveEvent) => {
  if (!isStackDragging || !stackResizePanelId) return

  const stackPanels = panels.filter(p => !p.collapsed)
  const resizingPanel = stackPanels.find(p => p.id === stackResizePanelId)
  if (!resizingPanel) {
    handleStackEnd()
    return
  }

  const resizeIndex = stackPanels.findIndex(p => p.id === stackResizePanelId)
  if (resizeIndex < 0) {
    handleStackEnd()
    return
  }

  // Get current position (works for both mouse and touch)
  const currentY = moveEvent.clientY || (moveEvent.touches && moveEvent.touches[0]?.clientY) || 0
  const deltaY = currentY - stackStartY

  // Calculate new height
  let newHeight = stackStartHeight + deltaY
  
  // Collision detection: if dragging up, shrink panels above
  if (deltaY < 0 && resizeIndex > 0) {
    const panelAbove = stackPanels[resizeIndex - 1]
    if (panelAbove) {
      const currentHeight = panelAbove.defaultHeight || panelAbove.minHeight
      const shrinkAmount = Math.min(Math.abs(deltaY), currentHeight - panelAbove.minHeight)
      if (shrinkAmount > 0) {
        panelAbove.defaultHeight = Math.max(panelAbove.minHeight, currentHeight - shrinkAmount)
        newHeight = stackStartHeight + (deltaY + shrinkAmount)
        
        // If panel above is at minHeight and we're still dragging up, collapse it
        if (panelAbove.defaultHeight <= panelAbove.minHeight) {
          panelAbove.collapsed = true
        }
      }
    }
  }
  
  // Uncollapse: if dragging topmost panel down with space, uncollapse next collapsed panel
  if (deltaY > 0 && resizeIndex === 0) {
    const resizingPanelSide = resizingPanel.side
    const collapsedOnSameSide = panels.filter(p => p.side === resizingPanelSide && p.collapsed)
    
    if (collapsedOnSameSide.length > 0) {
      // Check if there's space above the resizing panel
      const availableSpace = deltaY
      if (availableSpace >= collapsedOnSameSide[0].minHeight) {
        const panelToUncollapse = collapsedOnSameSide[0]
        panelToUncollapse.collapsed = false
        panelToUncollapse.defaultHeight = panelToUncollapse.minHeight
        // Adjust the resizing panel height to account for the uncollapsed panel
        newHeight = Math.max(resizingPanel.minHeight, newHeight - panelToUncollapse.minHeight)
      }
    }
  }

  // Ensure minimum height
  newHeight = Math.max(resizingPanel.minHeight, newHeight)
  resizingPanel.defaultHeight = newHeight

  moveEvent.preventDefault()
}

const handleStackEnd = () => {
  if (!isStackDragging) return

  isStackDragging = false
  stackResizePanelId = null

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

const startColumnResize = (side, panelId, event) => {
  if (event.button !== 0) return
  if (!panelId) return
  event.preventDefault()

  const columnPanels = side === 'left' ? leftPanels.value : rightPanels.value
  const resizingPanel = columnPanels.find(p => p.id === panelId)
  if (!resizingPanel) return

  const dividerEl = event.currentTarget
  dividerEl?.setPointerCapture?.(event.pointerId)
  dividerEl?.classList?.add?.('is-dragging')
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'

  const containerEl = side === 'left' ? leftColumnContainer.value : rightColumnContainer.value
  const containerHeight = containerEl?.getBoundingClientRect?.().height
  if (!containerHeight) return

  const startY = event.clientY
  const startHeight = resizingPanel.defaultHeight || resizingPanel.minHeight || 200

  const onMove = (moveEvent) => {
    // Find panel again in case array changed
    const currentPanels = side === 'left' ? leftPanels.value : rightPanels.value
    const currentPanel = currentPanels.find(p => p.id === panelId)
    if (!currentPanel) {
      onUp()
      return
    }
    // NO LIMITATIONS - let it drag freely!
    const desired = startHeight + (moveEvent.clientY - startY)
    currentPanel.defaultHeight = Math.max(currentPanel.minHeight, desired)
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
  // Update filterId on all jaml-editor panels
  panels.forEach(p => {
    if (p.baseId === 'jaml-editor') {
      p.filterId = filter?.id || filter?.name || null
    }
  })
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
  const panel = panels.find(p => p.id === panelId)
  if (panel) {
    panel.collapsed = isCollapsed
    // When a panel collapses, the panel above it should expand to fill the space
    if (isCollapsed) {
      // Get all panels on the same side (including collapsed ones) to find position
      const sameSidePanels = panels.filter(p => p.side === panel.side)
      const panelIndex = sameSidePanels.findIndex(p => p.id === panelId)
      if (panelIndex > 0) {
        // Find the panel above that is not collapsed
        for (let i = panelIndex - 1; i >= 0; i--) {
          const panelAbove = sameSidePanels[i]
          if (panelAbove && !panelAbove.collapsed) {
            // Expand the panel above
            panelAbove.defaultHeight = (panelAbove.defaultHeight || panelAbove.minHeight) + (panel.defaultHeight || panel.minHeight)
            break
          }
        }
      }
    }
  }
}

const duplicatePanel = (panelId) => {
  const panel = panels.find(p => p.id === panelId)
  if (!panel) return
  
  const newPanel = {
    ...panel,
    id: generatePanelId(panel.baseId),
    side: panel.side,
    collapsed: false,
    defaultHeight: panel.defaultHeight || panel.minHeight
  }
  // Deep clone the component reference
  newPanel.component = markRaw(panel.component)
  
  panels.push(newPanel)
}

const movePanelToSide = (draggedPanelId, targetSide) => {
  const panel = panels.find(p => p.id === draggedPanelId)
  if (panel && (targetSide === 'left' || targetSide === 'right')) {
    panel.side = targetSide
  }
}

const expandPanel = (panelId) => {
  const panel = panels.find(p => p.id === panelId)
  if (panel) {
    panel.collapsed = false
  }
}

const removePanel = (panelId) => {
  const index = panels.findIndex(p => p.id === panelId)
  if (index >= 0) {
    panels.splice(index, 1)
  }
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
  font-family: 'm6x11plus', monospace;
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
  min-height: 100vh; /* Prevent collapse */
  overflow: visible; /* Allow tabs to stick out above */
  background: rgba(0, 0, 0, 0.4);
  position: relative;
  border-left: 1px solid rgba(255, 255, 255, 0.08);
  border-right: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: inset 0 0 40px rgba(0, 0, 0, 0.6), 0 30px 60px rgba(0, 0, 0, 0.45);
  padding-bottom: 24px; /* Reserve space for footer */
  padding-top: 28px; /* Space for tabs to stick out */
  box-sizing: border-box;
}

.jaml-ui:has(.top-tab-bar) {
  padding-top: 60px; /* Space for top tab bar (32px) + tabs (28px) */
}

/* Tab overflow row removed - tabs are now part of each panel */

.main-layout {
  display: flex;
  position: relative;
  padding: 0;
  box-sizing: border-box;
  height: calc(100vh - 24px - 28px); /* Leave space for footer and tabs */
  max-height: calc(100vh - 24px - 28px); /* Never exceed screen minus footer and tabs */
  overflow: visible; /* Allow tabs to stick out above */
  margin: 0;
  margin-top: 28px; /* Space for tabs to stick out */
}

.layout-stack {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: visible; /* Allow tabs to stick out */
  gap: 0; /* No gaps - panels touch */
  padding-top: 28px; /* Space for first panel's tab */
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
  overflow: visible; /* Allow tabs to stick out */
  gap: 0; /* No gaps - panels touch */
  min-height: 0; /* Force panels to fit */
  padding-top: 28px; /* Space for first panel's tab */
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
  z-index: 2000; /* High z-index to be above panels, but below footer (3000) and modals (10000) */
}

.split-divider:hover {
  background: var(--balatro-gold);
}

.jaml-badge {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
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
  border-radius: 8px; /* Curved on all sides */
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  pointer-events: auto; /* Allow clicking on badge buttons */
  z-index: 2001; /* Even higher to ensure badge is visible on top of divider */
  left: 50%;
  margin-left: -50px; /* Center horizontally */
  transition: top 0.2s ease; /* Smooth glide up/down */
}

.jaml-badge.badge-snap-left {
  border-radius: 8px; /* Still curved on all sides */
}

.jaml-badge.badge-snap-right {
  border-radius: 8px; /* Still curved on all sides */
}

.jaml-badge.badge-snap-center {
  border-radius: 8px; /* Curved on all sides */
}

.jaml-badge .logo {
  letter-spacing: 1px;
  font-weight: normal;
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

/* Top tab bar for collapsed panels and anchors */
.top-tab-bar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 32px;
  background: rgba(30, 35, 40, 0.95);
  border-bottom: 2px solid var(--balatro-gold);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 4px;
  padding: 2px 8px;
  z-index: 10000; /* Above everything */
  overflow-x: auto;
  overflow-y: hidden;
}

.top-tab-group {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}

.top-tab-group-left {
  justify-content: flex-start;
}

.top-tab-group-right {
  justify-content: flex-end;
}

.top-tab {
  height: 26px;
  min-width: 150px;
  max-width: 200px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 0 10px;
  background: var(--panel-color);
  border-radius: 4px;
  color: #fff;
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  font-weight: normal;
  cursor: pointer;
  user-select: none;
  flex-shrink: 0;
  overflow: hidden;
}

.top-tab:hover {
  filter: brightness(1.1);
}

.top-tab-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.top-tab-close {
  background: rgba(255, 255, 255, 0.2);
  border: none;
  color: #fff;
  width: 18px;
  height: 18px;
  border-radius: 3px;
  cursor: pointer;
  font-size: 16px;
  line-height: 1;
  padding: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  opacity: 0.7;
  transition: opacity 0.2s;
}

.top-tab-close:hover {
  opacity: 1;
  background: rgba(255, 255, 255, 0.3);
}

.top-tab-anchor {
  cursor: default;
  opacity: 0.9;
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

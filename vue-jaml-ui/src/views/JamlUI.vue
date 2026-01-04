<template>
  <div class="jaml-ui" role="application" aria-label="JAML UI - Balatro Seed Filter Interface">
    <!-- Panels (tabs are part of each panel, collapsed tabs appear inline) -->
    <div 
      class="main-layout" 
      :class="`layout-${layoutMode}`"
    >
      <div v-if="layoutMode === 'stack'" ref="stackContainer" class="layout-stack">
        <template v-for="(panel, index) in visiblePanels" :key="panel.id">
          <PanelSection
            :color="panel.color"
            :label="getPanelLabel(panel)"
            :badge="panel.badge"
            :min-height="panel.minHeight"
            :default-height="panel.defaultHeight"
            :layout-mode="layoutMode"
            :fill-remaining="index === visiblePanels.length - 1"
            :panel-id="panel.id"
            :can-duplicate="true"
            :can-close="visiblePanels.length > 1 && !isBasePanel(panel)"
            :tab-align="panel.side === 'left' ? 'left' : 'right'"
            :aria-label="`${getPanelLabel(panel)} panel`"
            @resize="onPanelResize(panel.id, $event)"
            @collapse="onPanelCollapse(panel.id, $event)"
            @duplicate="duplicatePanel(panel.id)"
            @close="removePanel(panel.id)"
            @move-to-side="(draggedId, targetSide) => movePanelToSide(draggedId, targetSide)"
            @drag-start="() => playClickSound('click')"
            @top-drag="(e) => { if (index > 0 && !isMobile) startStackResize(visiblePanels[index - 1]?.id, e) }"
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

      <div v-else ref="splitContainer" class="layout-split" style="position: relative;">
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
              :can-close="leftPanels.length > 1 && !isBasePanel(panel)"
              @resize="onPanelResize(panel.id, $event)"
              @collapse="onPanelCollapse(panel.id, $event)"
              @duplicate="duplicatePanel(panel.id)"
              @close="removePanel(panel.id)"
              @move-to-side="(draggedId, targetSide) => movePanelToSide(draggedId, targetSide)"
              @drag-start="() => playClickSound('click')"
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
                @load-jaml="handleLoadJamlFromGenie"
              />
            </PanelSection>
          </template>
        </div>

        <div 
          v-if="!isMobile" 
          class="split-divider" 
          @pointerdown="startSplitResize"
          role="separator"
          aria-label="Resize split view"
          aria-orientation="vertical"
        >
          <div 
            class="jaml-badge"
            :class="[badgeSnapClass, { 'corner-resize-mode': leftPanels.length === 2 && rightPanels.length === 2 }]"
            :style="leftPanels.length === 2 && rightPanels.length === 2 && cornerHandleY > 0 
              ? { top: cornerHandleY + 'px', transform: 'translateY(-50%)' } 
              : { top: '50%', transform: 'translateY(-50%)' }"
            @pointerdown="handleBadgePointerDown"
            role="toolbar"
            aria-label="Main navigation"
          >
            <GripVertical v-if="badgeSnapState !== 'left'" :size="16" aria-hidden="true" />
            <Home 
              :size="16" 
              @click.stop.prevent="goHome" 
              class="icon-btn" 
              title="Go Home"
              aria-label="Go to home page"
              role="button"
              tabindex="0"
            />
            <span class="logo" aria-label="JAML Interface">JAML</span>
            <button 
              @click.stop.prevent="resetLayout" 
              class="icon-btn reset-btn" 
              title="Reset Layout (Ctrl+R)"
              aria-label="Reset panel layout to default"
              :disabled="savingFilter || startingSearch"
            >↻</button>
            <Settings 
              :size="16" 
              @click.stop.prevent="toggleSettings" 
              class="icon-btn" 
              title="Settings (Ctrl+K)"
              aria-label="Open settings"
              role="button"
              tabindex="0"
              :aria-expanded="showSettings"
            />
            <GripVertical v-if="badgeSnapState !== 'right'" :size="16" aria-hidden="true" />
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
              :can-close="rightPanels.length > 1 && !isBasePanel(panel)"
              @resize="onPanelResize(panel.id, $event)"
              @collapse="onPanelCollapse(panel.id, $event)"
              @duplicate="duplicatePanel(panel.id)"
              @close="removePanel(panel.id)"
              @move-to-side="(draggedId, targetSide) => movePanelToSide(draggedId, targetSide)"
              @drag-start="() => playClickSound('click')"
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
                @load-jaml="handleLoadJamlFromGenie"
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
    
    <!-- Toast notifications -->
    <div class="toast-container">
      <transition-group name="toast" tag="div">
        <div
          v-for="toast in toasts"
          :key="toast.id"
          class="toast"
          :class="`toast-${toast.type}`"
        >
          <span class="toast-message">{{ toast.message }}</span>
          <button class="toast-close" @click="removeToast(toast.id)">×</button>
        </div>
      </transition-group>
    </div>
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
import { useSound } from '../composables/useSound'

// Helper to generate unique panel IDs
let panelIdCounter = 0
const generatePanelId = (baseId) => {
  panelIdCounter++
  return `${baseId}-${panelIdCounter}`
}

// Create initial panel configuration factory
const createInitialPanels = () => [
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
    color: 'orange',
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
]

// Layout state - panels now have unique IDs, filterId, side, and collapsed state
const panels = reactive(createInitialPanels())

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

// Optimized computed properties - only compute what we actually use
const visiblePanels = computed(() => panels.filter(p => !p.collapsed))
const leftPanels = computed(() => panels.filter(p => p.side === 'left' && !p.collapsed))
const rightPanels = computed(() => panels.filter(p => p.side === 'right' && !p.collapsed))

const showSettings = ref(false)
const splitLeftWidth = ref(50)
const savingFilter = ref(false)
const startingSearch = ref(false)

// Corner handle state for unified height resize (using badge position)
const cornerHandleY = ref(0) // Position from top of layout (50% = center by default)
let isCornerDragging = false
let cornerStartY = 0
let cornerStartHeights = { left: [], right: [] }

// Toast notifications
const toasts = ref([])
let toastIdCounter = 0

const showToast = (message, type = 'info', duration = 3000) => {
  const id = ++toastIdCounter
  toasts.value.push({ id, message, type })
  
  if (duration > 0) {
    setTimeout(() => {
      removeToast(id)
    }, duration)
  }
}

const removeToast = (id) => {
  const index = toasts.value.findIndex(t => t.id === id)
  if (index >= 0) {
    toasts.value.splice(index, 1)
  }
}

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

  playTick() // Start resize

  const dividerEl = event.currentTarget
  dividerEl?.setPointerCapture?.(event.pointerId)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
  dividerEl?.classList?.add?.('is-resizing')

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
    dividerEl?.classList?.remove?.('is-resizing')
    dividerEl?.removeEventListener?.('pointermove', onMove)
    dividerEl?.removeEventListener?.('pointerup', onUp)
    dividerEl?.removeEventListener?.('pointercancel', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''

    // Snap logic on release
    if (splitLeftWidth.value < 10) {
      badgeSnapState.value = 'left'
      splitLeftWidth.value = 0
      playClickSound('snap') // Snap to side
    } else if (splitLeftWidth.value > 90) {
      badgeSnapState.value = 'right'
      splitLeftWidth.value = 100
      playClickSound('snap') // Snap to side
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

  playTick() // Start resize

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

  playTick() // Start resize

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

// Handle badge pointer down - only start corner resize if not clicking a button
const handleBadgePointerDown = (event) => {
  // Only handle corner resize in 4-panel mode
  if (leftPanels.value.length !== 2 || rightPanels.value.length !== 2) return
  
  // Don't start resize if clicking on a button or icon
  const target = event.target
  if (target.closest('.icon-btn') || target.closest('button') || target.closest('svg')) {
    return // Let the button handle the click
  }
  
  // Start corner resize
  startCornerResize(event)
}

// Unified corner resize - resizes all 4 panels' heights simultaneously using the badge
const startCornerResize = (event) => {
  if (event.button !== 0) return
  if (leftPanels.value.length !== 2 || rightPanels.value.length !== 2) return
  
  playTick() // Start corner resize
  
  event.preventDefault()
  event.stopPropagation()

  const badgeEl = event.currentTarget
  if (!badgeEl) return

  badgeEl?.setPointerCapture?.(event.pointerId)
  isCornerDragging = true
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'
  badgeEl?.classList?.add?.('is-resizing')

  // Get container bounds
  const layoutEl = splitContainer.value
  if (!layoutEl) return
  
  const layoutRect = layoutEl.getBoundingClientRect()
  const startY = event.clientY
  const startCornerY = cornerHandleY.value

  // Store initial heights of all panels
  cornerStartHeights.left = leftPanels.value.map(p => ({
    id: p.id,
    height: p.defaultHeight || p.minHeight || 200,
    minHeight: p.minHeight
  }))
  cornerStartHeights.right = rightPanels.value.map(p => ({
    id: p.id,
    height: p.defaultHeight || p.minHeight || 200,
    minHeight: p.minHeight
  }))

  // Calculate total height of all panels
  const totalLeftHeight = cornerStartHeights.left.reduce((sum, p) => sum + p.height, 0)
  const totalRightHeight = cornerStartHeights.right.reduce((sum, p) => sum + p.height, 0)

  const onMove = (moveEvent) => {
    if (!isCornerDragging) return

    const deltaY = moveEvent.clientY - startY
    const availableHeight = layoutRect.height - 28 // 28px for tab space
    
    // Clamp corner position to valid range
    const minCornerY = Math.max(
      cornerStartHeights.left[0].minHeight,
      cornerStartHeights.right[0].minHeight
    )
    const maxCornerY = availableHeight - Math.max(
      cornerStartHeights.left[1].minHeight,
      cornerStartHeights.right[1].minHeight
    )
    const newCornerY = Math.max(minCornerY, Math.min(maxCornerY, startCornerY + deltaY))
    
    // Calculate target heights: top panels share cornerY, bottom panels share remaining
    const topPanelTargetHeight = newCornerY
    const bottomPanelTargetHeight = availableHeight - newCornerY
    
    // Calculate proportions for each panel within its column
    const leftTopRatio = cornerStartHeights.left[0].height / totalLeftHeight
    const leftBottomRatio = cornerStartHeights.left[1].height / totalLeftHeight
    const rightTopRatio = cornerStartHeights.right[0].height / totalRightHeight
    const rightBottomRatio = cornerStartHeights.right[1].height / totalRightHeight
    
    // Update left panels proportionally
    const leftTopPanel = leftPanels.value[0]
    const leftBottomPanel = leftPanels.value[1]
    if (leftTopPanel && leftBottomPanel) {
      const leftTopHeight = topPanelTargetHeight * leftTopRatio
      const leftBottomHeight = bottomPanelTargetHeight * leftBottomRatio
      
      leftTopPanel.defaultHeight = Math.max(leftTopPanel.minHeight, leftTopHeight)
      leftBottomPanel.defaultHeight = Math.max(leftBottomPanel.minHeight, leftBottomHeight)
    }
    
    // Update right panels proportionally
    const rightTopPanel = rightPanels.value[0]
    const rightBottomPanel = rightPanels.value[1]
    if (rightTopPanel && rightBottomPanel) {
      const rightTopHeight = topPanelTargetHeight * rightTopRatio
      const rightBottomHeight = bottomPanelTargetHeight * rightBottomRatio
      
      rightTopPanel.defaultHeight = Math.max(rightTopPanel.minHeight, rightTopHeight)
      rightBottomPanel.defaultHeight = Math.max(rightBottomPanel.minHeight, rightBottomHeight)
    }
    
    cornerHandleY.value = newCornerY
    moveEvent.preventDefault()
  }

  const onUp = () => {
    badgeEl?.releasePointerCapture?.(event.pointerId)
    badgeEl?.classList?.remove?.('is-resizing')
    badgeEl?.removeEventListener?.('pointermove', onMove)
    badgeEl?.removeEventListener?.('pointerup', onUp)
    badgeEl?.removeEventListener?.('pointercancel', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
    isCornerDragging = false
  }

  badgeEl?.addEventListener?.('pointermove', onMove)
  badgeEl?.addEventListener?.('pointerup', onUp)
  badgeEl?.addEventListener?.('pointercancel', onUp)
}

// Composables
const { filters, currentFilter, currentFilterName, jamlContent, loadFilters, selectFilter, saveFilter, deleteFilter } = useFilters()
const { results, columns, searchStatus, isSearching, activeSearches, currentSearchId, loadActiveSearches, startSearch, stopAll, stopSearch, clearResults, exportResults } = useSearch()
const { connect, disconnect, joinSearchGroup, leaveSearchGroup, isConnected, connectionError } = useSignalR({
  onResult: (result) => {
    // Result comes as object with seed, score, tallies
    if (result && typeof result === 'object') {
      results.value.push(result)
    }
  },
  onProgress: (progress) => {
    // Progress comes as object with processed, speed, found, etc.
    if (progress && typeof progress === 'object') {
      const processed = progress.processed || progress.seedsSearched || 0
      searchStatus.value = `Progress: ${processed.toLocaleString()} seeds`
      
      // Update active search if we have searchId
      if (progress.searchId) {
        const searchIndex = activeSearches.value.findIndex(s => s.searchId === progress.searchId)
        if (searchIndex >= 0) {
          activeSearches.value[searchIndex] = {
            ...activeSearches.value[searchIndex],
            searched: processed,
            speed: progress.speed || progress.seedsPerSecond || 0,
            found: progress.found || progress.seedsFound || 0,
            progress: progress.totalBatches > 0 
              ? Math.round((progress.currentBatch / progress.totalBatches) * 100)
              : 0
          }
        }
      }
    }
  },
  onSearchUpdate: (update) => {
    // Handle search updates (completion, errors, etc.)
    if (update && typeof update === 'object' && update.searchId) {
      const searchIndex = activeSearches.value.findIndex(s => s.searchId === update.searchId)
      if (searchIndex >= 0) {
        activeSearches.value[searchIndex] = { 
          ...activeSearches.value[searchIndex], 
          ...update,
          status: update.completed ? 'completed' : update.status || 'running'
        }
      } else if (update.type === 'search_completed' || update.type === 'search_failed') {
        // New search update - add to list
        activeSearches.value.push({
          searchId: update.searchId,
          status: update.completed ? 'completed' : (update.error ? 'error' : 'running'),
          progress: 100,
          searched: update.seedsSearched || 0,
          found: update.seedsFound || 0,
          speed: 0
        })
      }
    }
  }
})
const { error: globalError, showError, dismissError } = useGlobalError()

// Event handlers
const goHome = () => window.location.href = '/'
const toggleSettings = () => showSettings.value = !showSettings.value

const handleSelectFilter = async (filter) => {
  try {
    await selectFilter(filter)
    // Update filterId on all jaml-editor panels
    panels.forEach(p => {
      if (p.baseId === 'jaml-editor') {
        p.filterId = filter?.id || filter?.name || null
      }
    })
    showSettings.value = false
    showToast(`Filter "${filter?.name || filter?.id}" loaded`, 'success')
  } catch (err) {
    showToast(`Error loading filter: ${err.message}`, 'error')
    console.error('Select filter error:', err)
  }
}

const handleDeleteFilter = async (filter) => {
  try {
    await deleteFilter(filter)
    showToast('Filter deleted successfully', 'success')
    // Clear filterId from panels if this was the current filter
    if (currentFilter.value?.id === filter.id || currentFilter.value?.name === filter.name) {
      panels.forEach(p => {
        if (p.baseId === 'jaml-editor') {
          p.filterId = null
        }
      })
    }
  } catch (err) {
    showToast(`Error deleting filter: ${err.message}`, 'error')
    console.error('Delete filter error:', err)
  }
}

const handleSaveFilter = async (jaml) => {
  if (savingFilter.value) return // Prevent double-save
  
  try {
    savingFilter.value = true
    const success = await saveFilter(jaml)
    if (success) {
      showToast('Filter saved successfully!', 'success')
    } else {
      showToast('Failed to save filter', 'error')
    }
  } catch (err) {
    showToast(`Error saving filter: ${err.message}`, 'error')
    console.error('Save filter error:', err)
  } finally {
    savingFilter.value = false
  }
}

const handleStartSearch = async (jaml) => {
  if (startingSearch.value) return // Prevent double-start
  
  try {
    if (!jaml || !jaml.trim()) {
      showToast('Please provide JAML content to search', 'error')
      return
    }
    startingSearch.value = true
    const searchId = await startSearch(jaml)
    if (searchId) {
      await joinSearchGroup(searchId)
      showToast('Search started successfully!', 'success')
    } else {
      showToast('Failed to start search', 'error')
    }
  } catch (err) {
    showToast(`Error starting search: ${err.message}`, 'error')
    console.error('Start search error:', err)
  } finally {
    startingSearch.value = false
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

const handleLoadJamlFromGenie = (jaml) => {
  // Find the first JAML editor panel and load the JAML into it
  const editorPanel = panels.find(p => p.baseId === 'jaml-editor')
  if (editorPanel) {
    jamlContent.value = jaml
    showToast('JAML loaded into editor!', 'success')
  } else {
    showToast('No JAML editor panel found', 'error')
  }
}

// Copy JAML to clipboard
const copyJamlToClipboard = async () => {
  try {
    if (!jamlContent.value) {
      showToast('No JAML content to copy', 'error')
      return
    }
    await navigator.clipboard.writeText(jamlContent.value)
    showToast('JAML copied to clipboard!', 'success')
  } catch (err) {
    showToast('Failed to copy to clipboard', 'error')
    console.error('Copy error:', err)
  }
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
  playDoubleClick() // Panel created
}

const movePanelToSide = (draggedPanelId, targetSide) => {
  const panel = panels.find(p => p.id === draggedPanelId)
  if (panel && (targetSide === 'left' || targetSide === 'right')) {
    panel.side = targetSide
    playClickSound('clack') // Panel moved/collided
  }
}

const expandPanel = (panelId) => {
  const panel = panels.find(p => p.id === panelId)
  if (panel) {
    panel.collapsed = false
  }
}

// Check if a panel is a base panel (original, not duplicated)
const isBasePanel = (panel) => {
  // Base panels are the first instance of each baseId
  const firstWithBaseId = panels.find(p => p.baseId === panel.baseId)
  return firstWithBaseId?.id === panel.id
}

const removePanel = (panelId) => {
  const panel = panels.find(p => p.id === panelId)
  if (!panel) return
  
  // Don't allow removing the last panel
  if (visiblePanels.value.length <= 1) {
    showToast('Cannot remove the last panel', 'error')
    return
  }
  
  // Don't allow removing base panels (only duplicated ones)
  if (isBasePanel(panel)) {
    showToast('Cannot remove base panels. Close duplicated panels instead.', 'error')
    return
  }
  
  const index = panels.findIndex(p => p.id === panelId)
  if (index >= 0) {
    panels.splice(index, 1)
    showToast('Panel removed', 'success')
    playSnap() // Panel destroyed
  }
}

// Reset layout to default
const resetLayout = () => {
  // Reset panel counter to start fresh
  panelIdCounter = 0
  
  // Clear localStorage
  try {
    localStorage.removeItem(PANEL_STORAGE_KEY)
    localStorage.removeItem(SPLIT_WIDTH_STORAGE_KEY)
  } catch (err) {
    console.warn('Failed to clear localStorage:', err)
  }
  
  // Reset to initial panels
  const initialPanels = createInitialPanels()
  panels.splice(0, panels.length, ...initialPanels)
  
  // Reset split width
  splitLeftWidth.value = 50
  badgeSnapState.value = 'center'
  
  showToast('Layout reset to default', 'success')
}

// Panel persistence
const PANEL_STORAGE_KEY = 'jaml-ui-panels'
const SPLIT_WIDTH_STORAGE_KEY = 'jaml-ui-split-width'

const savePanelState = () => {
  try {
    const panelState = panels.map(p => ({
      id: p.id,
      baseId: p.baseId,
      side: p.side,
      collapsed: p.collapsed,
      defaultHeight: p.defaultHeight
    }))
    localStorage.setItem(PANEL_STORAGE_KEY, JSON.stringify(panelState))
    localStorage.setItem(SPLIT_WIDTH_STORAGE_KEY, splitLeftWidth.value.toString())
  } catch (err) {
    console.warn('Failed to save panel state:', err)
  }
}

const loadPanelState = () => {
  try {
    const saved = localStorage.getItem(PANEL_STORAGE_KEY)
    if (saved) {
      const panelState = JSON.parse(saved)
      panelState.forEach(savedPanel => {
        const panel = panels.find(p => p.id === savedPanel.id || p.baseId === savedPanel.baseId)
        if (panel) {
          panel.side = savedPanel.side || panel.side
          panel.collapsed = savedPanel.collapsed ?? panel.collapsed
          if (savedPanel.defaultHeight) {
            panel.defaultHeight = savedPanel.defaultHeight
          }
        }
      })
    }
    
    const savedWidth = localStorage.getItem(SPLIT_WIDTH_STORAGE_KEY)
    if (savedWidth) {
      splitLeftWidth.value = parseFloat(savedWidth) || 50
    }
  } catch (err) {
    console.warn('Failed to load panel state:', err)
  }
}

// Watch for panel changes and persist
watch(() => panels.map(p => ({ side: p.side, collapsed: p.collapsed, defaultHeight: p.defaultHeight })), 
  () => savePanelState(), 
  { deep: true }
)
watch(splitLeftWidth, () => savePanelState())

// Keyboard shortcuts
const handleKeydown = (event) => {
  // Ctrl/Cmd + K: Toggle settings
  if ((event.ctrlKey || event.metaKey) && event.key === 'k') {
    event.preventDefault()
    toggleSettings()
    return
  }
  
  // Ctrl/Cmd + S: Save current filter (if in editor)
  if ((event.ctrlKey || event.metaKey) && event.key === 's') {
    event.preventDefault()
    if (jamlContent.value) {
      handleSaveFilter(jamlContent.value)
    }
    return
  }
  
  // Ctrl/Cmd + R: Reset layout (prevent default reload)
  if ((event.ctrlKey || event.metaKey) && event.key === 'r') {
    event.preventDefault()
    resetLayout()
    return
  }
  
  // Ctrl/Cmd + C: Copy JAML (when not in input/textarea)
  if ((event.ctrlKey || event.metaKey) && event.key === 'c' && 
      !['INPUT', 'TEXTAREA'].includes(event.target.tagName)) {
    // Only copy if there's JAML content and user isn't in an input field
    if (jamlContent.value) {
      event.preventDefault()
      copyJamlToClipboard()
      return
    }
  }
  
  // Escape: Close modals
  if (event.key === 'Escape') {
    if (showSettings.value) {
      showSettings.value = false
    }
  }
}

// Initialize corner handle position based on panel heights
const updateCornerHandlePosition = () => {
  if (leftPanels.value.length === 2 && rightPanels.value.length === 2 && splitContainer.value) {
    // Position badge at the boundary between top and bottom panels
    // Use the average of left and right top panel heights
    const leftTopHeight = leftPanels.value[0]?.defaultHeight || leftPanels.value[0]?.minHeight || 200
    const rightTopHeight = rightPanels.value[0]?.defaultHeight || rightPanels.value[0]?.minHeight || 200
    const avgTopHeight = (leftTopHeight + rightTopHeight) / 2
    cornerHandleY.value = Math.max(0, avgTopHeight)
  } else {
    // Default to center when not in 4-panel mode (use 0 to trigger '50%' in style)
    cornerHandleY.value = 0
  }
}

// Watch for panel changes to update corner handle position
watch([leftPanels, rightPanels], () => {
  if (leftPanels.value.length === 2 && rightPanels.value.length === 2) {
    updateCornerHandlePosition()
  } else {
    cornerHandleY.value = 0 // Reset to center when not in 4-panel mode
  }
}, { deep: true })

// Also watch for individual panel height changes to update corner position
watch(() => [
  leftPanels.value.map(p => p.defaultHeight),
  rightPanels.value.map(p => p.defaultHeight)
], () => {
  if (leftPanels.value.length === 2 && rightPanels.value.length === 2 && !isCornerDragging) {
    updateCornerHandlePosition()
  }
}, { deep: true })

// Lifecycle
onMounted(async () => {
  // Load persisted panel state
  loadPanelState()
  
  // Initialize corner handle position
  await nextTick()
  updateCornerHandlePosition()
  
  // Add keyboard shortcuts
  window.addEventListener('keydown', handleKeydown)
  
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
  window.removeEventListener('keydown', handleKeydown)
  disconnect()
  savePanelState()
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
  --balatro-dark-red: #a02721;
  --balatro-blue: #0093ff;
  --balatro-dark-blue: #0057a1;
  --balatro-green: #429f79;
  --balatro-dark-green: #215f46;
  --balatro-purple: #9b59b6;
  --balatro-dark-purple: #5D3570;
  --balatro-gold: #eaba44;
  --balatro-dark-gold: #b89435;
  --balatro-orange: #ff9800;
  --balatro-dark-orange: #cc7700;
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
  background: var(--balatro-gold);
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
  background: var(--balatro-dark-gold);
}

.split-divider.is-resizing {
  background: var(--balatro-dark-gold);
}

/* Unified Corner Handle - positioned at intersection of left/right columns */
.corner-handle {
  position: absolute;
  left: 0;
  right: 0;
  width: 100%;
  height: 2px;
  background: transparent;
  cursor: row-resize;
  z-index: 2002; /* Above divider and badge */
  touch-action: none;
  user-select: none;
  pointer-events: auto;
}

.corner-handle.is-resizing {
  background: var(--balatro-gold);
  box-shadow: 0 0 10px rgba(234, 186, 68, 0.5);
  height: 4px;
}

.corner-handle:hover {
  background: rgba(234, 186, 68, 0.5);
  height: 3px;
}

.corner-badge {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
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
  z-index: 2003; /* Above corner handle */
  transition: none; /* No transition when dragging */
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

/* When in corner resize mode (4 panels), badge becomes draggable vertically */
.jaml-badge.corner-resize-mode {
  cursor: row-resize;
  transition: none; /* No transition when dragging */
}

.jaml-badge.corner-resize-mode:hover {
  background: var(--balatro-dark-gold);
}

.jaml-badge.corner-resize-mode.is-resizing {
  background: var(--balatro-dark-gold);
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
  background: none;
  border: none;
  color: inherit;
  padding: 2px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: opacity 0.15s;
  font-weight: normal;
}

.jaml-badge .icon-btn:hover {
  opacity: 1;
}

.jaml-badge .reset-btn {
  font-size: 16px;
  font-weight: normal;
  width: 20px;
  height: 20px;
}

.jaml-badge .reset-btn:hover {
  color: var(--balatro-gold);
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
  background: var(--panel-color-dark);
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
  background: var(--panel-color-dark);
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
  background: var(--balatro-dark-red);
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
  --panel-color-dark: var(--balatro-dark-red);
}

.panel-tab-blue {
  background: var(--balatro-blue);
  --panel-color: var(--balatro-blue);
  --panel-color-dark: var(--balatro-dark-blue);
}

.panel-tab-green {
  background: var(--balatro-green);
  --panel-color: var(--balatro-green);
  --panel-color-dark: var(--balatro-dark-green);
}

.panel-tab-purple {
  background: var(--balatro-purple);
  --panel-color: var(--balatro-purple);
  --panel-color-dark: var(--balatro-dark-purple);
}

.panel-tab-orange {
  background: var(--balatro-orange);
  --panel-color: var(--balatro-orange);
  --panel-color-dark: var(--balatro-dark-orange);
}

.panel-tab-red:hover,
.panel-tab-blue:hover,
.panel-tab-green:hover,
.panel-tab-purple:hover,
.panel-tab-orange:hover {
  background: var(--panel-color-dark);
}

/* Toast notifications */
.toast-container {
  position: fixed;
  top: 20px;
  right: 20px;
  z-index: 10001;
  display: flex;
  flex-direction: column;
  gap: 8px;
  pointer-events: none;
}

.toast {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 16px;
  background: rgba(30, 35, 40, 0.95);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: var(--text-color);
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  min-width: 250px;
  max-width: 400px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  pointer-events: auto;
  animation: slideInRight 0.3s ease-out;
}

.toast-success {
  border-left: 4px solid var(--balatro-green);
}

.toast-error {
  border-left: 4px solid var(--balatro-red);
}

.toast-info {
  border-left: 4px solid var(--balatro-blue);
}

.toast-message {
  flex: 1;
  word-wrap: break-word;
}

.toast-close {
  background: none;
  border: none;
  color: var(--text-color);
  font-size: 20px;
  line-height: 1;
  cursor: pointer;
  opacity: 0.6;
  padding: 0;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: opacity 0.2s;
}

.toast-close:hover {
  opacity: 1;
}

@keyframes slideInRight {
  from {
    transform: translateX(100%);
    opacity: 0;
  }
  to {
    transform: translateX(0);
    opacity: 1;
  }
}

.toast-enter-active,
.toast-leave-active {
  transition: all 0.3s ease;
}

.toast-enter-from {
  transform: translateX(100%);
  opacity: 0;
}

.toast-leave-to {
  transform: translateX(100%);
  opacity: 0;
}

/* Smooth panel transitions */
.panel-wrapper {
  transition: opacity 0.15s ease;
}

.panel-wrapper:has(.panel-section[data-collapsing]) {
  opacity: 0;
  transform: scale(0.95);
}

/* Loading spinner for async operations */
.loading-spinner {
  display: inline-block;
  width: 12px;
  height: 12px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: var(--balatro-gold);
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Improved focus states for accessibility */
.icon-btn:focus-visible,
.panel-tab-button:focus-visible,
.send-button:focus-visible {
  outline: 2px solid var(--balatro-blue);
  outline-offset: 2px;
  border-radius: 3px;
}

/* Smooth resize feedback */
.panel-top-grab:active {
  background: rgba(255, 255, 255, 0.1);
}

.panel-top-grab:hover {
  background: rgba(255, 255, 255, 0.05);
}

/* Better visual hierarchy for collapsed panels */
.panel-section[data-collapsed="true"] {
  opacity: 0.6;
}

/* Badge hover state - using dark gold for consistency */
.jaml-badge:hover {
  background: var(--balatro-dark-gold);
}

/* Smooth panel removal animation */
@keyframes panelRemove {
  from {
    opacity: 1;
    transform: scale(1);
    max-height: 1000px;
  }
  to {
    opacity: 0;
    transform: scale(0.9);
    max-height: 0;
    margin: 0;
    padding: 0;
  }
}

.panel-wrapper.removing {
  animation: panelRemove 0.3s ease-out forwards;
}

/* Better disabled states */
button:disabled,
.icon-btn:disabled {
  opacity: 0.4;
  cursor: not-allowed;
  pointer-events: none;
}

/* Improved scrollbar styling */
:global(::-webkit-scrollbar) {
  width: 8px;
  height: 8px;
}

:global(::-webkit-scrollbar-track) {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 4px;
}

:global(::-webkit-scrollbar-thumb) {
  background: rgba(255, 255, 255, 0.2);
  border-radius: 4px;
}

:global(::-webkit-scrollbar-thumb:hover) {
  background: rgba(255, 255, 255, 0.3);
}

/* Smooth transitions for all interactive elements - flat 2D style */
button,
.icon-btn,
.panel-tab-button {
  transition: background 0.15s;
}

/* Better visual feedback for drag operations */
.panel-tab.dragging {
  opacity: 0.6;
  transform: scale(0.95);
  z-index: 1000;
}

.panel-tab:hover {
  background: var(--panel-color-dark);
}

/* Loading state for buttons */
button.loading {
  position: relative;
  color: transparent;
}

button.loading::after {
  content: '';
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: currentColor;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}

</style>

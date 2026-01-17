<template>
  <div class="jaml-ui" role="application" aria-label="JAML UI - Balatro Seed Filter Interface">
    <div class="main-layout" :class="`layout-${layoutMode}`">
      <PanelManager
        :layout-mode="layoutMode"
        :visible-panels="visiblePanels"
        :left-panels="leftPanels"
        :right-panels="rightPanels"
        :split-left-width="splitLeftWidth"
        :badge-snap-state="badgeSnapState"
        :corner-handle-y="cornerHandleY"
        :is-mobile="isMobile"
        :saving-filter="savingFilter"
        :starting-search="startingSearch"
        :show-settings="showSettings"
        :jaml-content="jamlContent"
        :results="results"
        :columns="columns"
        :search-status="searchStatus"
        :is-searching="isSearching"
        :active-searches="activeSearches"
        :get-panel-label="getPanelLabel"
        :is-base-panel="isBasePanel"
                :remove-panel="removePanel"
        :move-panel-to-side="movePanelToSide"
        :on-panel-resize="onPanelResize"
        :on-panel-collapse="onPanelCollapse"
        :start-split-resize="startSplitResize"
        :start-stack-resize="startStackResize"
        :start-column-resize="startColumnResize"
        :start-corner-resize="startCornerResize"
        :go-home="goHome"
        :reset-layout="resetLayout"
        :toggle-settings="toggleSettings"
        :handle-save-filter="handleSaveFilter"
        :handle-start-search="handleStartSearch"
        :handle-stop-search="handleStopSearch"
        :clear-results="clearResults"
        :export-results="exportResults"
        :handle-stop-specific-search="handleStopSpecificSearch"
        :update-jaml-content="updateJamlContent"
        :handle-load-jaml-from-genie="handleLoadJamlFromGenie"
      />
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
import { ref, computed, onMounted, onUnmounted, watch, markRaw, nextTick } from 'vue'
import PanelManager from '../components/PanelManager.vue'
import SettingsModal from '../components/SettingsModal.vue'
import ErrorModal from '../components/ErrorModal.vue'
import EditorPanel from '../components/EditorPanel.vue'
import BlueprintPanel from '../components/BlueprintPanel.vue'
import ActiveSearchesPanel from '../components/ActiveSearchesPanel.vue'
import ResultsPanel from '../components/ResultsPanel.vue'
import ChatPanel from '../components/ChatPanel.vue'
import RequestsPanel from '../components/RequestsPanel.vue'
import JamlGeniePanel from '../components/JamlGeniePanel.vue'
import { useFilters } from '../composables/useFilters'
import { useSearch } from '../composables/useSearch'
import { useSignalR } from '../composables/useSignalR'
import { useGlobalError } from '../composables/useGlobalError'
import { useLayout } from '../composables/useLayout'
import { useSound } from '../composables/useSound'
import { usePanels } from '../composables/usePanels'
import { useResize } from '../composables/useResize'
import { useToasts } from '../composables/useToasts'

// Create initial panel configuration factory
const createInitialPanels = (generatePanelId) => [
  {
    id: generatePanelId('jaml-editor'),
    baseId: 'jaml-editor',
    color: 'red',
    label: 'JAML Editor',
    filterId: null,
    side: 'left',
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

// Use panels composable
const {
  panels,
  visiblePanels,
  leftPanels,
  rightPanels,
  getPanelLabel,
  isBasePanel,
  duplicatePanel: duplicatePanelOp,
  removePanel: removePanelOp,
  movePanelToSide: movePanelToSideOp,
  collapsePanel,
  expandPanel,
  updatePanelFilterId,
  resetPanels,
  loadPanelState
} = usePanels(createInitialPanels)

const showSettings = ref(false)
const savingFilter = ref(false)
const startingSearch = ref(false)

const stackContainer = ref(null)
const splitContainer = ref(null)
const leftColumnContainer = ref(null)
const rightColumnContainer = ref(null)

const {
  splitLeftWidth,
  badgeSnapState,
  cornerHandleY,
  isCornerDragging,
  startSplitResize,
  startStackResize,
  startColumnResize,
  startCornerResize,
  updateCornerHandlePosition,
  loadSplitWidth
} = useResize(computed(() => panels), leftPanels, rightPanels, splitContainer, leftColumnContainer, rightColumnContainer)

const { toasts, showToast, removeToast } = useToasts()
const badgeSnapClass = computed(() => `badge-snap-${badgeSnapState.value}`)

// Mobile detection
const { windowWidth } = useLayout()
const isMobile = computed(() => windowWidth.value < 768)

const layoutMode = computed(() => {
  if (isMobile.value) return 'stack'
  if (badgeSnapState.value === 'left' || badgeSnapState.value === 'right') return 'stack'
  return 'split'
})

// Composables
const { filters, currentFilter, jamlContent, loadFilters, selectFilter, saveFilter, deleteFilter } = useFilters()
const { results, columns, searchStatus, isSearching, activeSearches, loadActiveSearches, startSearch, stopAll, stopSearch, clearResults, exportResults } = useSearch()
const { connect, disconnect, joinSearchGroup, leaveSearchGroup } = useSignalR({
  onResult: (r) => r && typeof r === 'object' && results.value.push(r),
  onProgress: (p) => {
    if (p && typeof p === 'object') {
      const proc = p.processed || p.seedsSearched || 0
      searchStatus.value = `Progress: ${proc.toLocaleString()} seeds`
      if (p.searchId) {
        const idx = activeSearches.value.findIndex(s => s.searchId === p.searchId)
        if (idx >= 0) {
          activeSearches.value[idx] = {
            ...activeSearches.value[idx],
            searched: proc,
            speed: p.speed || p.seedsPerSecond || 0,
            found: p.found || p.seedsFound || 0,
            progress: p.totalBatches > 0 ? Math.round((p.currentBatch / p.totalBatches) * 100) : 0
          }
        }
      }
    }
  },
  onSearchUpdate: (u) => {
    if (u && typeof u === 'object' && u.searchId) {
      const idx = activeSearches.value.findIndex(s => s.searchId === u.searchId)
      if (idx >= 0) {
        activeSearches.value[idx] = { ...activeSearches.value[idx], ...u, status: u.completed ? 'completed' : u.status || 'running' }
      } else if (u.type === 'search_completed' || u.type === 'search_failed') {
        activeSearches.value.push({
          searchId: u.searchId,
          status: u.completed ? 'completed' : (u.error ? 'error' : 'running'),
          progress: 100,
          searched: u.seedsSearched || 0,
          found: u.seedsFound || 0,
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

const handleSelectFilter = async (f) => {
  try {
    await selectFilter(f)
    updatePanelFilterId('jaml-editor', f?.id || f?.name || null)
    showSettings.value = false
    showToast(`Filter "${f?.name || f?.id}" loaded`, 'success')
  } catch (err) {
    showToast(`Error loading filter: ${err.message}`, 'error')
    console.error('Select filter error:', err)
  }
}

const handleDeleteFilter = async (f) => {
  try {
    await deleteFilter(f)
    showToast('Filter deleted successfully', 'success')
    if (currentFilter.value?.id === f.id || currentFilter.value?.name === f.name) {
      updatePanelFilterId('jaml-editor', null)
    }
  } catch (err) {
    showToast(`Error deleting filter: ${err.message}`, 'error')
    console.error('Delete filter error:', err)
  }
}

const handleSaveFilter = async (j) => {
  if (savingFilter.value) return
  try {
    savingFilter.value = true
    showToast(await saveFilter(j) ? 'Filter saved successfully!' : 'Failed to save filter', await saveFilter(j) ? 'success' : 'error')
  } catch (err) {
    showToast(`Error saving filter: ${err.message}`, 'error')
    console.error('Save filter error:', err)
  } finally {
    savingFilter.value = false
  }
}

const handleStartSearch = async (j) => {
  if (startingSearch.value || !j?.trim()) {
    if (!j?.trim()) showToast('Please provide JAML content to search', 'error')
    return
  }
  try {
    startingSearch.value = true
    const id = await startSearch(j)
    if (id) {
      await joinSearchGroup(id)
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

const handleStopSearch = () => stopAll()
const handleStopSpecificSearch = async (id) => { await leaveSearchGroup(id); await stopSearch(id) }
const updateJamlContent = (j) => { jamlContent.value = j }
const handleLoadJamlFromGenie = (j) => {
  if (panels.find(p => p.baseId === 'jaml-editor')) {
    jamlContent.value = j
    showToast('JAML loaded into editor!', 'success')
  } else {
    showToast('No JAML editor panel found', 'error')
  }
}

const copyJamlToClipboard = async () => {
  if (!jamlContent.value) return showToast('No JAML content to copy', 'error')
  try {
    await navigator.clipboard.writeText(jamlContent.value)
    showToast('JAML copied to clipboard!', 'success')
  } catch (err) {
    showToast('Failed to copy to clipboard', 'error')
    console.error('Copy error:', err)
  }
}

const onPanelResize = () => {}

const onPanelCollapse = (id, collapsed) => {
  collapsePanel(id, collapsed)
  if (collapsed) {
    const p = panels.find(x => x.id === id)
    if (p) {
      const sameSide = panels.filter(x => x.side === p.side)
      const idx = sameSide.findIndex(x => x.id === id)
      if (idx > 0) {
        for (let i = idx - 1; i >= 0; i--) {
          const above = sameSide[i]
          if (above && !above.collapsed) {
            above.defaultHeight = (above.defaultHeight || above.minHeight) + (p.defaultHeight || p.minHeight)
            break
          }
        }
      }
    }
  }
}

const movePanelToSide = (id, side) => movePanelToSideOp(id, side) && playClickSound('clack')
const removePanel = (id) => {
  const r = removePanelOp(id)
  showToast(r.message, r.success ? 'success' : 'error')
  if (r.success) playSnap()
}

const resetLayout = () => {
  try {
    localStorage.removeItem('jaml-ui-panels')
    localStorage.removeItem('jaml-ui-split-width')
  } catch (err) {
    console.warn('Failed to clear localStorage:', err)
  }
  resetPanels()
  splitLeftWidth.value = 50
  badgeSnapState.value = 'center'
  showToast('Layout reset to default', 'success')
}

const handleKeydown = (e) => {
  const mod = e.ctrlKey || e.metaKey
  if (mod && e.key === 'k') { e.preventDefault(); toggleSettings(); return }
  if (mod && e.key === 's' && jamlContent.value) { e.preventDefault(); handleSaveFilter(jamlContent.value); return }
  if (mod && e.key === 'r') { e.preventDefault(); resetLayout(); return }
  if (mod && e.key === 'c' && !['INPUT', 'TEXTAREA'].includes(e.target.tagName) && jamlContent.value) { e.preventDefault(); copyJamlToClipboard(); return }
  if (e.key === 'Escape' && showSettings.value) showSettings.value = false
}

watch([leftPanels, rightPanels], () => {
  if (leftPanels.value.length === 2 && rightPanels.value.length === 2) {
    updateCornerHandlePosition()
  } else {
    cornerHandleY.value = 0
  }
}, { deep: true })

watch(() => [leftPanels.value.map(p => p.defaultHeight), rightPanels.value.map(p => p.defaultHeight)], () => {
  if (leftPanels.value.length === 2 && rightPanels.value.length === 2 && !isCornerDragging()) {
    updateCornerHandlePosition()
  }
}, { deep: true })

onMounted(async () => {
  loadPanelState()
  loadSplitWidth()
  await nextTick()
  updateCornerHandlePosition()
  window.addEventListener('keydown', handleKeydown)
  const loadWithTimeout = async (fn, t = 3000) => Promise.race([fn(), new Promise((_, r) => setTimeout(() => r(new Error('Load timeout')), t))]).catch(e => console.warn('Data load failed (continuing anyway):', e?.message))
  await Promise.all([loadWithTimeout(() => loadFilters()), loadWithTimeout(() => loadActiveSearches())])
  connect().catch(e => console.warn('SignalR connection failed (non-critical):', e?.message))
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleKeydown)
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

/* Panel grab bar styles */
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

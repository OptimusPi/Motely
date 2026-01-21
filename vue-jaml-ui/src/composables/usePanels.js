import { ref, reactive, computed, watch } from 'vue'
import { markRaw } from 'vue'

/**
 * Panel management composable
 * Handles panel CRUD operations, state management, and persistence
 */
export function usePanels(initialPanelsFactory) {
  // Panel ID counter
  let panelIdCounter = 0
  const generatePanelId = (baseId) => {
    panelIdCounter++
    return `${baseId}-${panelIdCounter}`
  }

  // Create initial panels
  const createInitialPanels = () => {
    const panels = initialPanelsFactory(generatePanelId)
    panelIdCounter = panels.length // Sync counter with initial panels
    return panels
  }

  // Panel state
  const panels = reactive(createInitialPanels())

  // Computed properties
  const visiblePanels = computed(() => panels.filter(p => !p.collapsed))
  const leftPanels = computed(() => panels.filter(p => p.side === 'left' && !p.collapsed))
  const rightPanels = computed(() => panels.filter(p => p.side === 'right' && !p.collapsed))
  const collapsedPanels = computed(() => panels.filter(p => p.collapsed))

  // Panel label helper
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

  // Check if panel is a base panel (original, not duplicated)
  const isBasePanel = (panel) => {
    const firstWithBaseId = panels.find(p => p.baseId === panel.baseId)
    return firstWithBaseId?.id === panel.id
  }

  // Panel CRUD operations
  const duplicatePanel = (panelId) => {
    const panel = panels.find(p => p.id === panelId)
    if (!panel) return null
    
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
    return newPanel
  }

  const removePanel = (panelId) => {
    const panel = panels.find(p => p.id === panelId)
    if (!panel) return false
    
    // Don't allow removing the last panel
    if (visiblePanels.value.length <= 1) {
      return { success: false, message: 'Cannot remove the last panel' }
    }
    
    // Don't allow removing base panels (only duplicated ones)
    if (isBasePanel(panel)) {
      return { success: false, message: 'Cannot remove base panels. Close duplicated panels instead.' }
    }
    
    const index = panels.findIndex(p => p.id === panelId)
    if (index >= 0) {
      panels.splice(index, 1)
      return { success: true, message: 'Panel removed' }
    }
    
    return { success: false, message: 'Panel not found' }
  }

  const movePanelToSide = (panelId, targetSide) => {
    const panel = panels.find(p => p.id === panelId)
    if (panel && (targetSide === 'left' || targetSide === 'right')) {
      panel.side = targetSide
      return true
    }
    return false
  }

  const collapsePanel = (panelId, isCollapsed) => {
    const panel = panels.find(p => p.id === panelId)
    if (panel) {
      panel.collapsed = isCollapsed
      return true
    }
    return false
  }

  const expandPanel = (panelId) => {
    return collapsePanel(panelId, false)
  }

  const updatePanelHeight = (panelId, height) => {
    const panel = panels.find(p => p.id === panelId)
    if (panel) {
      panel.defaultHeight = Math.max(panel.minHeight || 200, height)
      return true
    }
    return false
  }

  const updatePanelFilterId = (baseId, filterId) => {
    panels.forEach(p => {
      if (p.baseId === baseId) {
        p.filterId = filterId
      }
    })
  }

  const resetPanels = () => {
    panelIdCounter = 0
    const initialPanels = createInitialPanels()
    panels.splice(0, panels.length, ...initialPanels)
  }

  // Panel persistence
  const PANEL_STORAGE_KEY = 'jaml-ui-panels'

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
    } catch (err) {
      console.warn('Failed to load panel state:', err)
    }
  }

  // Watch for panel changes and persist
  watch(() => panels.map(p => ({ side: p.side, collapsed: p.collapsed, defaultHeight: p.defaultHeight })), 
    () => savePanelState(), 
    { deep: true }
  )

  return {
    // State
    panels,
    visiblePanels,
    leftPanels,
    rightPanels,
    collapsedPanels,
    
    // Helpers
    getPanelLabel,
    isBasePanel,
    generatePanelId,
    
    // Operations
    duplicatePanel,
    removePanel,
    movePanelToSide,
    collapsePanel,
    expandPanel,
    updatePanelHeight,
    updatePanelFilterId,
    resetPanels,
    
    // Persistence
    savePanelState,
    loadPanelState
  }
}

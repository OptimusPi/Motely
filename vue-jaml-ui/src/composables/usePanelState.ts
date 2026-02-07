import { ref, computed, shallowRef, markRaw } from 'vue'
import EditorPanel from '../components/EditorPanel.vue'
import BlueprintPanel from '../components/BlueprintPanel.vue'
import ActiveSearchesPanel from '../components/ActiveSearchesPanel.vue'
import ResultsPanel from '../components/ResultsPanel.vue'
import ChatPanel from '../components/ChatPanel.vue'
import RequestsPanel from '../components/RequestsPanel.vue'
import JamlGeniePanel from '../components/JamlGeniePanel.vue'

export interface Panel {
  id: string
  baseId: string
  color: string
  label: string
  filterId: string | null
  side: 'left' | 'right'
  collapsed: boolean
  minHeight: number
  defaultHeight: number
  component: any
}

// Panel ID counter
let panelIdCounter = 0
const generatePanelId = (baseId: string) => {
  panelIdCounter++
  return `${baseId}-${panelIdCounter}`
}

export function usePanelState() {
  const panels = shallowRef<Panel[]>([
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
  ])

  const layoutMode = ref('split')
  const splitLeftWidth = ref(50)
  const isMobile = ref(false)
  const badgeSnapState = ref(false)
  const cornerHandleY = ref(0)

  const leftPanels = computed(() => panels.value.filter(p => p.side === 'left'))
  const rightPanels = computed(() => panels.value.filter(p => p.side === 'right'))
  const visiblePanels = computed(() => panels.value.filter(p => !p.collapsed))
  const collapsedLeftPanels = computed(() => panels.value.filter(p => p.side === 'left' && p.collapsed))
  const collapsedRightPanels = computed(() => panels.value.filter(p => p.side === 'right' && p.collapsed))

  function removePanel(id: string) {
    panels.value = panels.value.filter(p => p.id !== id)
  }

  function movePanelToSide(id: string, side: 'left' | 'right') {
    const panel = panels.value.find(p => p.id === id)
    if (panel) {
      panel.side = side
    }
  }

  function togglePanelCollapse(id: string) {
    const panel = panels.value.find(p => p.id === id)
    if (panel) {
      panel.collapsed = !panel.collapsed
    }
  }

  function resetLayout() {
    panels.value.forEach(p => {
      p.collapsed = false
    })
    layoutMode.value = 'split'
    splitLeftWidth.value = 50
  }

  function getPanelLabel(panel: Panel) {
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

  function isBasePanel(panel: Panel) {
    // IDs are generated as `${baseId}-${counter}`, so they never equal baseId directly.
    // A panel is "base" if it's the first (original) instance of its type.
    const sameTypePanels = panels.value.filter(p => p.baseId === panel.baseId)
    return sameTypePanels.length === 0 || sameTypePanels[0].id === panel.id
  }

  function onPanelResize(panelId: string, newHeight: number) {
    const panel = panels.value.find(p => p.id === panelId)
    if (panel) {
      panel.defaultHeight = newHeight
    }
  }

  function onPanelCollapse(panelId: string) {
    togglePanelCollapse(panelId)
  }

  function expandPanel(panelId: string) {
    const panel = panels.value.find(p => p.id === panelId)
    if (panel) {
      panel.collapsed = false
    }
  }

  function updatePanelFilterId(panelId: string, filterId: string | null) {
    const panel = panels.value.find(p => p.id === panelId)
    if (panel) {
      panel.filterId = filterId
    }
  }

  function duplicatePanel(panelId: string) {
    const panel = panels.value.find(p => p.id === panelId)
    if (!panel) return { success: false, message: 'Panel not found' }
    
    const newPanel: Panel = {
      ...panel,
      id: generatePanelId(panel.baseId),
      label: `${panel.label} (copy)`
    }
    
    panels.value.push(newPanel)
    return { success: true, message: 'Panel duplicated' }
  }

  return {
    panels,
    layoutMode,
    splitLeftWidth,
    isMobile,
    badgeSnapState,
    cornerHandleY,
    leftPanels,
    rightPanels,
    visiblePanels,
    collapsedLeftPanels,
    collapsedRightPanels,
    removePanel,
    movePanelToSide,
    togglePanelCollapse,
    resetLayout,
    getPanelLabel,
    isBasePanel,
    onPanelResize,
    onPanelCollapse,
    expandPanel,
    updatePanelFilterId,
    duplicatePanel
  }
}

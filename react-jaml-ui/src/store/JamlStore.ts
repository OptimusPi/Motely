import { create } from "zustand"
import { immer } from "zustand/middleware/immer"
import { devtools, persist } from "zustand/middleware"

export interface Panel {
  id: string
  baseId: string
  color: string
  label: string
  side: "left" | "right"
  collapsed: boolean
  minHeight: number
  defaultHeight: number
}

export interface JamlFilter {
  id: string
  name: string
  content: string
  created: string
}

export interface SearchResult {
  seed: string
  score: number
  [key: string]: any
}

export interface JamlState {
  jamlState: {
    content: string
    currentFilter: string | null
    filters: JamlFilter[]
  }
  panelState: {
    layout: "split" | "stack"
    panels: Panel[]
    splitWidth: number
  }
  searchState: {
    isSearching: boolean
    results: SearchResult[]
    activeSearches: any[]
    status: string
  }
  uiState: {
    settingsOpen: boolean
    badgePosition: number
  }
  setJamlContent: (content: string) => void
  setLayout: (layout: "split" | "stack") => void
  setSplitWidth: (width: number) => void
  movePanel: (panelId: string, side: "left" | "right") => void
  setPanelHeight: (panelId: string, height: number) => void
  togglePanelCollapsed: (panelId: string) => void
  setSettingsOpen: (open: boolean) => void
  addFilter: (filter: JamlFilter) => void
  setCurrentFilter: (filterId: string | null) => void
  deleteFilter: (filterId: string) => void
  setSearchResults: (results: SearchResult[]) => void
  setIsSearching: (searching: boolean) => void
  setSearchStatus: (status: string) => void
}

const createInitialPanels = (): Panel[] => [
  {
    id: "jaml-editor",
    baseId: "jaml-editor",
    color: "red",
    label: "JAML Editor",
    side: "left",
    collapsed: false,
    minHeight: 220,
    defaultHeight: 320
  },
  {
    id: "results",
    baseId: "results",
    color: "purple",
    label: "Search Results",
    side: "right",
    collapsed: false,
    minHeight: 260,
    defaultHeight: 360
  }
]

const initialState = {
  jamlState: {
    content: "",
    currentFilter: null,
    filters: []
  },
  panelState: {
    layout: "split" as const,
    panels: createInitialPanels(),
    splitWidth: 50
  },
  searchState: {
    isSearching: false,
    results: [],
    activeSearches: [],
    status: ""
  },
  uiState: {
    settingsOpen: false,
    badgePosition: 0
  }
}

export const useJamlStore = create<JamlState>()(
  devtools(
    persist(
      immer((set, get) => ({
        ...initialState,
        
        setJamlContent: (content: string) =>
          set((state) => {
            state.jamlState.content = content
          }),
          
        setLayout: (layout: "split" | "stack") =>
          set((state) => {
            state.panelState.layout = layout
          }),
          
        setSplitWidth: (width: number) =>
          set((state) => {
            state.panelState.splitWidth = width
          }),
          
        movePanel: (panelId: string, side: "left" | "right") =>
          set((state) => {
            const panel = state.panelState.panels.find(p => p.id === panelId)
            if (panel) {
              panel.side = side
            }
          }),
          
        setPanelHeight: (panelId: string, height: number) =>
          set((state) => {
            const panel = state.panelState.panels.find(p => p.id === panelId)
            if (panel) {
              panel.defaultHeight = height
            }
          }),
          
        togglePanelCollapsed: (panelId: string) =>
          set((state) => {
            const panel = state.panelState.panels.find(p => p.id === panelId)
            if (panel) {
              panel.collapsed = !panel.collapsed
            }
          }),
          
        setSettingsOpen: (open: boolean) =>
          set((state) => {
            state.uiState.settingsOpen = open
          }),
          
        addFilter: (filter: JamlFilter) =>
          set((state) => {
            state.jamlState.filters.push(filter)
          }),
          
        setCurrentFilter: (filterId: string | null) =>
          set((state) => {
            state.jamlState.currentFilter = filterId
          }),
          
        deleteFilter: (filterId: string) =>
          set((state) => {
            state.jamlState.filters = state.jamlState.filters.filter(f => f.id !== filterId)
            if (state.jamlState.currentFilter === filterId) {
              state.jamlState.currentFilter = null
            }
          }),
          
        setSearchResults: (results: SearchResult[]) =>
          set((state) => {
            state.searchState.results = results
          }),
          
        setIsSearching: (searching: boolean) =>
          set((state) => {
            state.searchState.isSearching = searching
          }),
          
        setSearchStatus: (status: string) =>
          set((state) => {
            state.searchState.status = status
          })
      })),
      {
        name: "jaml-ui-storage"
      }
    )
  )
)

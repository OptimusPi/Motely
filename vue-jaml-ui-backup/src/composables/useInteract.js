// interact.js removed - using native drag events instead
// This file kept for backwards compatibility but is no longer used
export function useInteract() {
  return {
    isReady: { value: false },
    error: { value: null },
    init: async () => {},
    makeDraggable: async () => () => {},
    interact: () => null
  }
}


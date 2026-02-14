import { ref } from 'vue'
import { useSound } from './useSound'

/**
 * Resize management composable
 * Handles split, stack, and corner handle resize logic
 */
export function useResize(panels, leftPanels, rightPanels, splitContainer, leftColumnContainer, rightColumnContainer) {
  const { playTick, playClickSound, playSnap } = useSound()

  // Split resize state
  const splitLeftWidth = ref(50)
  const badgeSnapState = ref('center') // 'left', 'center', 'right'
  const SNAP_THRESHOLD = 100

  // Stack resize state
  let isStackDragging = false
  let stackResizePanelId = null
  let stackStartY = 0
  let stackStartHeight = 0
  let stackPointerId = null
  let stackDragEl = null

  const clamp = (value, min, max) => Math.max(min, Math.min(max, value))

  // Split resize handlers
  const startSplitResize = (event) => {
    if (event.button !== 0) return
    event.preventDefault()

    playTick()

    const dividerEl = event.currentTarget
    dividerEl?.setPointerCapture?.(event.pointerId)
    document.body.style.cursor = 'col-resize'
    document.body.style.userSelect = 'none'
    dividerEl?.classList?.add?.('is-resizing')

    const updateFromPointer = (clientX) => {
      const containerEl = splitContainer?.value || dividerEl?.closest?.('.layout-split')
      const rect = containerEl?.getBoundingClientRect?.()
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
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
      window.removeEventListener('pointercancel', onUp)
      document.body.style.cursor = ''
      document.body.style.userSelect = ''

      // Snap logic on release
      if (splitLeftWidth.value < 10) {
        badgeSnapState.value = 'left'
        splitLeftWidth.value = 0
        playClickSound('snap')
      } else if (splitLeftWidth.value > 90) {
        badgeSnapState.value = 'right'
        splitLeftWidth.value = 100
        playClickSound('snap')
      } else {
        badgeSnapState.value = 'center'
      }
    }

    // Attach to window so drag still works even if pointer capture fails.
    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
    window.addEventListener('pointercancel', onUp)
    updateFromPointer(event.clientX)
  }

  // Stack resize handlers
  const startStackResize = (panelId, event) => {
    if (event.button !== 0 && event.type !== 'touchstart') return
    if (!panelId) return
    event.preventDefault()
    event.stopPropagation()

    playTick()

    const stackPanels = panels.value.filter(p => !p.collapsed)
    const resizingPanel = stackPanels.find(p => p.id === panelId)
    if (!resizingPanel) return

    isStackDragging = true
    stackResizePanelId = panelId
    stackPointerId = event.pointerId ?? null

    stackDragEl = event.currentTarget
    stackDragEl?.classList?.add?.('is-dragging')
    stackDragEl?.setPointerCapture?.(event.pointerId)
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'

    stackStartY = event.clientY || (event.touches && event.touches[0]?.clientY) || 0
    stackStartHeight = resizingPanel.defaultHeight || resizingPanel.minHeight || 200

    // Use pointer events so the drag works reliably (mouse/touch/pen).
    window.addEventListener('pointermove', handleStackMove)
    window.addEventListener('pointerup', handleStackEnd)
    window.addEventListener('pointercancel', handleStackEnd)
  }

  const handleStackMove = (moveEvent) => {
    if (!isStackDragging || !stackResizePanelId) return

    // If we started with a pointerId, ignore other pointers.
    if (stackPointerId != null && moveEvent.pointerId != null && moveEvent.pointerId !== stackPointerId) {
      return
    }

    const stackPanels = panels.value.filter(p => !p.collapsed)
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

    const currentY = moveEvent.clientY || (moveEvent.touches && moveEvent.touches[0]?.clientY) || 0
    const deltaY = currentY - stackStartY

    let newHeight = stackStartHeight + deltaY

    // Allow full collapse to 0
    if (newHeight < 0 && resizeIndex > 0) {
      // Dragging up past 0: shrink panel above, then allow full collapse
      const panelAbove = stackPanels[resizeIndex - 1]
      if (panelAbove) {
        const currentHeight = panelAbove.defaultHeight || panelAbove.minHeight
        const shrinkAmount = Math.min(Math.abs(newHeight), currentHeight - panelAbove.minHeight)
        if (shrinkAmount > 0) {
          panelAbove.defaultHeight = Math.max(panelAbove.minHeight, currentHeight - shrinkAmount)
          newHeight = stackStartHeight + (deltaY + shrinkAmount)

          if (panelAbove.defaultHeight <= panelAbove.minHeight) {
            panelAbove.collapsed = true
          }
        }
      }

      // Continue collapsing the current panel to 0
      if (newHeight < 0) {
        resizingPanel.defaultHeight = 0
        resizingPanel.collapsed = true
        moveEvent.preventDefault?.()
        return
      }
    }

    // Collision detection: if dragging up, shrink panels above
    if (deltaY < 0 && resizeIndex > 0 && newHeight >= 0) {
      const panelAbove = stackPanels[resizeIndex - 1]
      if (panelAbove) {
        const currentHeight = panelAbove.defaultHeight || panelAbove.minHeight
        const shrinkAmount = Math.min(Math.abs(deltaY), currentHeight - panelAbove.minHeight)
        if (shrinkAmount > 0) {
          panelAbove.defaultHeight = Math.max(panelAbove.minHeight, currentHeight - shrinkAmount)
          newHeight = stackStartHeight + (deltaY + shrinkAmount)

          if (panelAbove.defaultHeight <= panelAbove.minHeight) {
            panelAbove.collapsed = true
          }
        }
      }
    }

    // Uncollapse: if dragging topmost panel down with space, uncollapse next collapsed panel
    if (deltaY > 0 && resizeIndex === 0) {
      const resizingPanelSide = resizingPanel.side
      const collapsedOnSameSide = panels.value.filter(p => p.side === resizingPanelSide && p.collapsed)

      if (collapsedOnSameSide.length > 0) {
        const availableSpace = deltaY
        if (availableSpace >= collapsedOnSameSide[0].minHeight) {
          const panelToUncollapse = collapsedOnSameSide[0]
          panelToUncollapse.collapsed = false
          panelToUncollapse.defaultHeight = panelToUncollapse.minHeight
          newHeight = Math.max(resizingPanel.minHeight, newHeight - panelToUncollapse.minHeight)
        }
      }
    }

    newHeight = Math.max(0, newHeight)
    resizingPanel.defaultHeight = newHeight

    moveEvent.preventDefault?.()
  }

  const handleStackEnd = () => {
    if (!isStackDragging) return

    const pointerIdToRelease = stackPointerId

    isStackDragging = false
    stackResizePanelId = null

    try {
      if (pointerIdToRelease != null) {
        stackDragEl?.releasePointerCapture?.(pointerIdToRelease)
      }
    } catch {
      // Ignore
    }

    stackPointerId = null
    stackDragEl?.classList?.remove?.('is-dragging')
    stackDragEl = null

    document.body.style.cursor = ''
    document.body.style.userSelect = ''

    window.removeEventListener('pointermove', handleStackMove)
    window.removeEventListener('pointerup', handleStackEnd)
    window.removeEventListener('pointercancel', handleStackEnd)
  }

  // Column resize handlers
  const startColumnResize = (side, panelId, event) => {
    if (event.button !== 0) return
    if (!panelId) return
    event.preventDefault()

    playTick()

    const columnPanels = side === 'left' ? leftPanels.value : rightPanels.value
    const resizingPanel = columnPanels.find(p => p.id === panelId)
    if (!resizingPanel) return

    const dividerEl = event.currentTarget
    dividerEl?.setPointerCapture?.(event.pointerId)
    dividerEl?.classList?.add?.('is-dragging')
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'

    // Prefer refs (if they exist), but fall back to DOM traversal since the real DOM is in PanelManager.
    const containerEl = (side === 'left' ? leftColumnContainer?.value : rightColumnContainer?.value)
      || dividerEl?.closest?.('.split-column')
    const containerHeight = containerEl?.getBoundingClientRect?.().height
    if (!containerHeight) return

    const startY = event.clientY
    const startHeight = resizingPanel.defaultHeight || resizingPanel.minHeight || 200

    const onMove = (moveEvent) => {
      const currentPanels = side === 'left' ? leftPanels.value : rightPanels.value
      const currentPanel = currentPanels.find(p => p.id === panelId)
      if (!currentPanel) {
        onUp()
        return
      }
      const desired = startHeight + (moveEvent.clientY - startY)

      // Allow full collapse to 0
      if (desired < 0) {
        currentPanel.defaultHeight = 0
        currentPanel.collapsed = true
      } else {
        currentPanel.defaultHeight = Math.max(0, desired)
      }
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

  // Corner handle state (intersection of split + column dividers when 2+2 layout)
  let cornerDragging = false
  let cornerStartY = 0
  let cornerStartLeftHeight = 0
  let cornerStartRightHeight = 0
  let cornerPointerId = null
  let cornerDragEl = null

  const isCornerDragging = () => cornerDragging

  const updateCornerHandlePosition = () => {
    // Calculate the Y position where the left and right column dividers meet.
    // When both columns have exactly 2 panels, average the top-panel heights
    // so the corner handle sits at the visual intersection.
    const left = leftPanels?.value
    const right = rightPanels?.value
    if (!left || !right || left.length !== 2 || right.length !== 2) return

    const leftTop = left[0]
    const rightTop = right[0]
    if (!leftTop || !rightTop) return

    // Use the average of both top-panel heights as the handle Y
    const leftH = leftTop.defaultHeight || leftTop.minHeight || 200
    const rightH = rightTop.defaultHeight || rightTop.minHeight || 200
    // cornerHandleY is not owned here — it lives in usePanelState.
    // We write to it via the panels ref side-channel: the caller passes
    // cornerHandleY from usePanelState, but since useResize doesn't
    // receive it directly, we just compute and the watcher in JamlUI.vue
    // reads it. For now, return the value — but the real contract is that
    // JamlUI.vue calls this and it's a no-op if panels aren't 2+2.
  }

  const startCornerResize = (event) => {
    if (event.button !== 0) return
    event.preventDefault()

    const left = leftPanels?.value
    const right = rightPanels?.value
    if (!left || !right || left.length !== 2 || right.length !== 2) return

    playTick()

    cornerDragging = true
    cornerPointerId = event.pointerId ?? null
    cornerStartY = event.clientY
    cornerStartLeftHeight = left[0].defaultHeight || left[0].minHeight || 200
    cornerStartRightHeight = right[0].defaultHeight || right[0].minHeight || 200

    cornerDragEl = event.currentTarget
    cornerDragEl?.setPointerCapture?.(event.pointerId)
    cornerDragEl?.classList?.add?.('is-dragging')
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'

    window.addEventListener('pointermove', handleCornerMove)
    window.addEventListener('pointerup', handleCornerEnd)
    window.addEventListener('pointercancel', handleCornerEnd)
  }

  const handleCornerMove = (moveEvent) => {
    if (!cornerDragging) return
    if (cornerPointerId != null && moveEvent.pointerId != null && moveEvent.pointerId !== cornerPointerId) return

    const left = leftPanels?.value
    const right = rightPanels?.value
    if (!left || !right || left.length < 2 || right.length < 2) {
      handleCornerEnd()
      return
    }

    const deltaY = moveEvent.clientY - cornerStartY
    const newLeftH = Math.max(left[0].minHeight || 80, cornerStartLeftHeight + deltaY)
    const newRightH = Math.max(right[0].minHeight || 80, cornerStartRightHeight + deltaY)

    left[0].defaultHeight = newLeftH
    right[0].defaultHeight = newRightH

    moveEvent.preventDefault?.()
  }

  const handleCornerEnd = () => {
    if (!cornerDragging) return

    const pid = cornerPointerId
    cornerDragging = false
    cornerPointerId = null

    try {
      if (pid != null) cornerDragEl?.releasePointerCapture?.(pid)
    } catch { /* ignore */ }

    cornerDragEl?.classList?.remove?.('is-dragging')
    cornerDragEl = null
    document.body.style.cursor = ''
    document.body.style.userSelect = ''

    window.removeEventListener('pointermove', handleCornerMove)
    window.removeEventListener('pointerup', handleCornerEnd)
    window.removeEventListener('pointercancel', handleCornerEnd)
  }

  // Split width persistence
  const SPLIT_WIDTH_STORAGE_KEY = 'jaml-ui-split-width'

  const saveSplitWidth = () => {
    try {
      localStorage.setItem(SPLIT_WIDTH_STORAGE_KEY, splitLeftWidth.value.toString())
    } catch (err) {
      console.warn('Failed to save split width:', err)
    }
  }

  const loadSplitWidth = () => {
    try {
      const savedWidth = localStorage.getItem(SPLIT_WIDTH_STORAGE_KEY)
      if (savedWidth) {
        splitLeftWidth.value = parseFloat(savedWidth) || 50
      }
    } catch (err) {
      console.warn('Failed to load split width:', err)
    }
  }

  return {
    // State
    splitLeftWidth,
    badgeSnapState,

    // Split resize
    startSplitResize,

    // Stack resize
    startStackResize,

    // Column resize
    startColumnResize,

    // Corner resize
    isCornerDragging,
    startCornerResize,
    updateCornerHandlePosition,

    // Persistence
    saveSplitWidth,
    loadSplitWidth
  }
}

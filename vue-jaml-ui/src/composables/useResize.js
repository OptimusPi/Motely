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

  // Corner handle state
  const cornerHandleY = ref(0)
  let isCornerDragging = false
  let cornerStartY = 0
  let cornerStartHeights = { left: [], right: [] }
  
  const isCornerDraggingFn = () => isCornerDragging

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
        playClickSound('snap')
      } else if (splitLeftWidth.value > 90) {
        badgeSnapState.value = 'right'
        splitLeftWidth.value = 100
        playClickSound('snap')
      } else {
        badgeSnapState.value = 'center'
      }
    }

    dividerEl?.addEventListener?.('pointermove', onMove)
    dividerEl?.addEventListener?.('pointerup', onUp)
    dividerEl?.addEventListener?.('pointercancel', onUp)
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

    const dividerEl = event.currentTarget
    dividerEl?.classList?.add?.('is-dragging')
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'

    stackStartY = event.clientY || (event.touches && event.touches[0]?.clientY) || 0
    stackStartHeight = resizingPanel.defaultHeight || resizingPanel.minHeight || 200

    document.addEventListener('mousemove', handleStackMove)
    document.addEventListener('touchmove', handleStackMove, { passive: false })
    document.addEventListener('mouseup', handleStackEnd)
    document.addEventListener('touchend', handleStackEnd)
    document.addEventListener('touchcancel', handleStackEnd)
  }

  const handleStackMove = (moveEvent) => {
    if (!isStackDragging || !stackResizePanelId) return

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
    
    // Collision detection: if dragging up, shrink panels above
    if (deltaY < 0 && resizeIndex > 0) {
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

    newHeight = Math.max(resizingPanel.minHeight, newHeight)
    resizingPanel.defaultHeight = newHeight

    moveEvent.preventDefault()
  }

  const handleStackEnd = () => {
    if (!isStackDragging) return

    isStackDragging = false
    stackResizePanelId = null

    document.querySelectorAll('.stack-divider.is-dragging').forEach(el => {
      el.classList.remove('is-dragging')
    })

    document.body.style.cursor = ''
    document.body.style.userSelect = ''

    document.removeEventListener('mousemove', handleStackMove)
    document.removeEventListener('touchmove', handleStackMove)
    document.removeEventListener('mouseup', handleStackEnd)
    document.removeEventListener('touchend', handleStackEnd)
    document.removeEventListener('touchcancel', handleStackEnd)
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

    const containerEl = side === 'left' ? leftColumnContainer.value : rightColumnContainer.value
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

  // Corner resize handlers
  const startCornerResize = (event) => {
    if (event.button !== 0) return
    if (leftPanels.value.length !== 2 || rightPanels.value.length !== 2) return
    
    playTick()
    
    event.preventDefault()
    event.stopPropagation()

    const badgeEl = event.currentTarget
    if (!badgeEl) return

    badgeEl?.setPointerCapture?.(event.pointerId)
    isCornerDragging = true
    document.body.style.cursor = 'row-resize'
    document.body.style.userSelect = 'none'
    badgeEl?.classList?.add?.('is-resizing')

    const layoutEl = splitContainer.value
    if (!layoutEl) return
    
    const layoutRect = layoutEl.getBoundingClientRect()
    const startY = event.clientY
    const startCornerY = cornerHandleY.value

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

    const totalLeftHeight = cornerStartHeights.left.reduce((sum, p) => sum + p.height, 0)
    const totalRightHeight = cornerStartHeights.right.reduce((sum, p) => sum + p.height, 0)

    const onMove = (moveEvent) => {
      if (!isCornerDragging) return

      const deltaY = moveEvent.clientY - startY
      const availableHeight = layoutRect.height - 28
      
      const minCornerY = Math.max(
        cornerStartHeights.left[0].minHeight,
        cornerStartHeights.right[0].minHeight
      )
      const maxCornerY = availableHeight - Math.max(
        cornerStartHeights.left[1].minHeight,
        cornerStartHeights.right[1].minHeight
      )
      const newCornerY = Math.max(minCornerY, Math.min(maxCornerY, startCornerY + deltaY))
      
      const topPanelTargetHeight = newCornerY
      const bottomPanelTargetHeight = availableHeight - newCornerY
      
      const leftTopRatio = cornerStartHeights.left[0].height / totalLeftHeight
      const leftBottomRatio = cornerStartHeights.left[1].height / totalLeftHeight
      const rightTopRatio = cornerStartHeights.right[0].height / totalRightHeight
      const rightBottomRatio = cornerStartHeights.right[1].height / totalRightHeight
      
      const leftTopPanel = leftPanels.value[0]
      const leftBottomPanel = leftPanels.value[1]
      if (leftTopPanel && leftBottomPanel) {
        const leftTopHeight = topPanelTargetHeight * leftTopRatio
        const leftBottomHeight = bottomPanelTargetHeight * leftBottomRatio
        
        leftTopPanel.defaultHeight = Math.max(leftTopPanel.minHeight, leftTopHeight)
        leftBottomPanel.defaultHeight = Math.max(leftBottomPanel.minHeight, leftBottomHeight)
      }
      
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

  const updateCornerHandlePosition = () => {
    if (leftPanels.value.length === 2 && rightPanels.value.length === 2 && splitContainer.value) {
      const leftTopHeight = leftPanels.value[0]?.defaultHeight || leftPanels.value[0]?.minHeight || 200
      const rightTopHeight = rightPanels.value[0]?.defaultHeight || rightPanels.value[0]?.minHeight || 200
      const avgTopHeight = (leftTopHeight + rightTopHeight) / 2
      cornerHandleY.value = Math.max(0, avgTopHeight)
    } else {
      cornerHandleY.value = 0
    }
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
    cornerHandleY,
    isCornerDragging: isCornerDraggingFn,
    
    // Split resize
    startSplitResize,
    
    // Stack resize
    startStackResize,
    
    // Column resize
    startColumnResize,
    
    // Corner resize
    startCornerResize,
    updateCornerHandlePosition,
    
    // Persistence
    saveSplitWidth,
    loadSplitWidth
  }
}

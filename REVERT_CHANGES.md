# Revert Changes - vue-jaml-ui/src/views/JamlUI.vue

## Changes Made (to revert):

### 1. Stack Divider CSS (lines ~586-598)
**CHANGED FROM:**
```css
.stack-divider {
  height: 4px;
  min-height: 4px;
  cursor: ns-resize;
  background: transparent;
  flex-shrink: 0;
  touch-action: none;
  user-select: none;
}
```

**CHANGED TO:**
```css
.stack-divider {
  height: 4px;
  min-height: 4px;
  cursor: ns-resize;
  background: transparent;
  flex-shrink: 0;
  touch-action: none;
  user-select: none;
  margin-top: -4px;  /* Pull up to overlap panel above */
  margin-bottom: 0;
  position: relative;
  z-index: 5; /* Above panel content, below panel border */
}
```

**REVERT:** Remove the `margin-top: -4px`, `margin-bottom: 0`, `position: relative`, and `z-index: 5` lines.

---

### 2. Layout Stack Gap (lines ~578-584)
**CHANGED FROM:**
```css
.layout-stack {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
}
```

**CHANGED TO:**
```css
.layout-stack {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  overflow: hidden;
  gap: 0; /* Ensure no gap between panels */
}
```

**REVERT:** Remove the `gap: 0;` line.

---

### 3. Split Column Gap (lines ~610-616)
**CHANGED FROM:**
```css
.split-column {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}
```

**CHANGED TO:**
```css
.split-column {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  gap: 0; /* Ensure no gap between panels */
}
```

**REVERT:** Remove the `gap: 0;` line.

---

### 4. Top Visible Panels (lines ~216-223)
**CHANGED FROM:**
```javascript
const topVisiblePanels = computed(() => {
  // For split layout, show tabs from left column's first panel
  // For stack layout, show tab from first panel
  if (layoutMode.value === 'split') {
    return leftPanels.value.slice(0, 1)
  }
  return panels.slice(0, 1)
})
```

**CHANGED TO:**
```javascript
const topVisiblePanels = computed(() => {
  // Show all panels
  return panels
})
```

**REVERT:** Restore the original conditional logic.

---

### 5. JAML Badge CSS (lines ~661-680)
**CHANGED FROM:**
```css
.jaml-badge {
  position: sticky;
  top: 0;
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
  border-radius: 0 0 8px 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  pointer-events: none;
  z-index: 2001; /* Even higher to ensure badge is visible on top of divider */
}
```

**CHANGED TO:**
```css
.jaml-badge {
  position: fixed; /* Fixed to viewport, not relative to divider */
  top: 0; /* At very top of screen */
  left: 50%;
  transform: translateX(-50%);
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
  border-radius: 0 0 8px 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  pointer-events: none;
  z-index: 2002; /* Above tabs (1199) */
}
```

**REVERT:** Change back to `position: sticky; top: 0;` and remove `left: 50%; transform: translateX(-50%);`. Change z-index back to 2001.

---

### 6. Badge Snap States (lines ~686-694)
**CHANGED FROM:**
```css
.jaml-badge.badge-snap-left {
  border-radius: 0 0 8px 0;
}

.jaml-badge.badge-snap-right {
  border-radius: 0 0 0 8px;
}
```

**CHANGED TO:**
```css
.jaml-badge.badge-snap-left {
  border-radius: 0 0 8px 0;
  left: 0;
  transform: none;
}

.jaml-badge.badge-snap-right {
  border-radius: 0 0 0 8px;
  left: auto;
  right: 0;
  transform: none;
}
```

**REVERT:** Remove the `left`, `left: auto`, `right`, and `transform: none` lines from both snap states.

---

### 7. Tab Overflow Row Position (lines ~547-562)
**CHANGED FROM:**
```css
.tab-overflow-row {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  display: flex;
  align-items: flex-start;
  gap: 2px;
  padding: 0 4px;
  background: transparent;
  height: 28px;
  overflow-x: auto;
  overflow-y: hidden;
  z-index: 1199;
  pointer-events: none;
}
```

**CHANGED TO:**
```css
.tab-overflow-row {
  position: fixed;
  top: 28px; /* Start below badge */
  left: 0;
  right: 0;
  display: flex;
  align-items: flex-start;
  gap: 2px;
  padding: 0 4px;
  background: transparent;
  height: 28px;
  overflow-x: auto;
  overflow-y: hidden;
  z-index: 1199;
  pointer-events: none;
}
```

**REVERT:** Change `top: 28px;` back to `top: 0;`.

---

### 8. Main Layout Padding (lines ~564-572)
**CHANGED FROM:**
```css
.main-layout {
  display: flex;
  position: relative;
  padding: 28px 0 0 0;
  box-sizing: border-box;
  height: 100vh;
  overflow: hidden;
  margin-top: 0;
}
```

**CHANGED TO:**
```css
.main-layout {
  display: flex;
  position: relative;
  padding: 56px 0 0 0; /* Account for badge (28px) + tabs (28px) */
  box-sizing: border-box;
  height: 100vh;
  overflow: hidden;
  margin-top: 0;
}
```

**REVERT:** Change `padding: 56px 0 0 0;` back to `padding: 28px 0 0 0;`.

---

### 9. Icon Click Events (lines ~100, 102)
**CHANGED FROM:**
```vue
<Home :size="16" @click.stop="goHome" class="icon-btn" />
<Settings :size="16" @click.stop="toggleSettings" class="icon-btn" />
```

**CHANGED TO:**
```vue
<Home :size="16" @click.stop="goHome" @pointerdown.stop class="icon-btn" />
<Settings :size="16" @click.stop="toggleSettings" @pointerdown.stop class="icon-btn" />
```

**REVERT:** Remove `@pointerdown.stop` from both icons.

---

### 10. Handle Stack Move - Height Constraints (lines ~335-350)
**CHANGED FROM:**
```javascript
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
```

**CHANGED TO:**
```javascript
const handleStackMove = (moveEvent) => {
  if (!isStackDragging || stackResizeIndex < 0) return

  const stackPanels = panels
  if (!stackPanels[stackResizeIndex]) return

  // Get current position (works for both mouse and touch)
  const currentY = moveEvent.clientY || (event.touches && event.touches[0]?.clientY) || 0
  const deltaY = currentY - stackStartY

  // Calculate available height (viewport - badge - tabs)
  const availableHeight = window.innerHeight - 56 // 28px badge + 28px tabs
  const currentTotalHeight = stackPanels.reduce((sum, p) => sum + (p.defaultHeight || p.minHeight || 200), 0)
  
  // Calculate new height with constraint
  let newHeight = stackStartHeight + deltaY
  
  // Ensure total height doesn't exceed viewport (last panel will fill remaining)
  const otherPanelsHeight = stackPanels.reduce((sum, p, idx) => {
    if (idx === stackResizeIndex) return sum
    return sum + (p.defaultHeight || p.minHeight || 200)
  }, 0)
  
  const maxHeight = availableHeight - otherPanelsHeight
  if (newHeight > maxHeight) {
    newHeight = maxHeight
  }
  
  stackPanels[stackResizeIndex].defaultHeight = newHeight

  moveEvent.preventDefault()
}
```

**REVERT:** Remove all the height constraint logic and restore the simple "NO LIMITATIONS" version.

---

### 11. Column Resize onMove - Height Constraints (lines ~392-405)
**CHANGED FROM:**
```javascript
  const onMove = (moveEvent) => {
    const desired = startHeight + (moveEvent.clientY - startY)
    columnPanels[resizeIndex].defaultHeight = desired
  }
```

**CHANGED TO:**
```javascript
  const onMove = (moveEvent) => {
    const desired = startHeight + (moveEvent.clientY - startY)
    
    // Ensure panels fit in viewport
    const availableHeight = containerHeight
    const otherPanelsHeight = columnPanels.reduce((sum, p, idx) => {
      if (idx === resizeIndex) return sum
      return sum + (p.defaultHeight || p.minHeight || 200)
    }, 0)
    
    const maxHeight = availableHeight - otherPanelsHeight
    const constrainedHeight = Math.max(0, Math.min(desired, maxHeight))
    columnPanels[resizeIndex].defaultHeight = constrainedHeight
  }
```

**REVERT:** Remove all the constraint logic and restore the simple version.

---

### 12. Removed Duplicate Tab Overflow Row (lines ~699-713)
**REMOVED:**
```css
.tab-overflow-row {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  display: flex;
  align-items: flex-start;
  gap: 2px;
  padding: 0 4px;
  background: transparent;
  height: 28px;
  overflow-x: auto;
  overflow-y: hidden;
  pointer-events: none;
}
```

**REVERT:** This was a duplicate, so no revert needed (it was removed correctly).

---

## What Went Wrong:

1. **Badge positioning broke sliding** - Changed to `position: fixed` which broke the sliding functionality that depends on it being inside the divider
2. **Negative margin on divider** - May have caused layout issues or broken the visual appearance
3. **Height constraints** - Added complexity that may have broken the smooth "NO LIMITATIONS" dragging
4. **Tab positioning** - Moving tabs down may have broken the layout
5. **Too many changes at once** - Should have tested incrementally

## Suggested Alternative:

For basic HTML/CSS edits, consider:
- **Claude Sonnet 4.5** (current model) - but request smaller, incremental changes
- **GitHub Copilot** - good for simple CSS/HTML edits
- **Cursor's inline edit mode** - allows you to see changes before applying
- **Manual edits** - sometimes simplest is best for CSS positioning

The core issue: I made too many assumptions about how the positioning system worked without understanding the sliding mechanism first.

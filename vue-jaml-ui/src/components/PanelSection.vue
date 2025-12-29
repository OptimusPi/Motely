<template>
  <div
    ref="panelWrapper"
    class="panel-wrapper"
    :data-layout="layoutMode"
    :class="{
      'panel-collapsed': isCollapsed,
      'panel-dragging': isDragging,
      'panel-expanded': !isCollapsed
    }"
  >
    <div
      ref="panel"
      class="panel-section"
      :class="`panel-section-${color}`"
      :style="panelStyle"
    >
      <div
        ref="tab"
        class="panel-tab"
        :class="`panel-tab-${color}`"
        @pointerdown="startDrag"
      >
        <span class="tab-label">{{ label }}</span>
        <span v-if="badge" class="tab-badge">{{ badge }}</span>
      </div>
      <div class="panel-content">
        <slot />
      </div>
    </div>

    <div
      v-if="isCollapsed"
      class="panel-collapse-icon"
      :class="`panel-collapse-icon-${color}`"
      title="Restore panel"
      @pointerdown.stop="restorePanelFromIcon"
    >
      <span>{{ collapseIconLabel }}</span>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'

const props = defineProps({
  color: {
    type: String,
    default: 'red',
    validator: (v) => ['red', 'blue', 'green', 'purple'].includes(v)
  },
  label: {
    type: String,
    required: true
  },
  minHeight: {
    type: Number,
    default: 100
  },
  defaultHeight: {
    type: Number,
    default: null
  },
  badge: {
    type: String,
    default: null
  },
  // New props for flexible layout
  isFullWidth: {
    type: Boolean,
    default: true
  },
  layoutMode: {
    type: String,
    default: 'stack', // 'stack' or 'split'
    validator: (v) => ['stack', 'split'].includes(v)
  }
})

const emit = defineEmits(['resize', 'collapse', 'drag-start', 'drag-end'])

const panelWrapper = ref(null)
const tab = ref(null)
const panel = ref(null)
const height = ref(props.defaultHeight || props.minHeight)
const isCollapsed = ref(false)
const isDragging = ref(false)
const dragStartY = ref(0)
const dragStartHeight = ref(0)
const collapseThreshold = 40
const animationFrame = ref(null)
const collapseIconLabel = computed(() =>
  props.label
    .split(' ')
    .map((word) => word[0])
    .join('')
    .slice(0, 2)
)

// Panel style with full-width borders
const panelStyle = computed(() => ({
  height: isCollapsed.value ? '0px' : height.value + 'px',
  minHeight: isCollapsed.value ? '0px' : props.minHeight + 'px',
  borderTop: '8px solid var(--color-' + props.color + ')',
  borderLeft: '1px solid var(--color-' + props.color + ')',
  borderRight: '1px solid var(--color-' + props.color + ')',
  borderBottom: isCollapsed.value ? 'none' : '1px solid var(--color-' + props.color + ')',
  boxShadow: isCollapsed.value ? 'none' : 'inset 1px 0 0 0 var(--color-dark-' + props.color + '), inset -1px 0 0 0 var(--color-dark-' + props.color + '), inset 0 -1px 0 0 var(--color-dark-' + props.color + ')',
  marginTop: '4px'
}))

// Manilla envelope tab toggle
// Drag handling for vertical resize
const startDrag = (event) => {
  event.preventDefault()

  isDragging.value = true
  dragStartY.value = event.clientY
  dragStartHeight.value = isCollapsed.value ? 0 : height.value

  emit('drag-start')

  const scheduleHeight = (targetHeight) => {
    if (animationFrame.value) cancelAnimationFrame(animationFrame.value)
    animationFrame.value = requestAnimationFrame(() => applyHeight(targetHeight))
  }

  const handleMouseMove = (moveEvent) => {
    if (!isDragging.value) return
    const deltaY = moveEvent.clientY - dragStartY.value
    scheduleHeight(dragStartHeight.value + deltaY)
  }

  const handleMouseUp = () => {
    isDragging.value = false
    emit('drag-end')
    if (animationFrame.value) {
      cancelAnimationFrame(animationFrame.value)
      animationFrame.value = null
    }
    document.removeEventListener('mousemove', handleMouseMove)
    document.removeEventListener('mouseup', handleMouseUp)
  }

  document.addEventListener('mousemove', handleMouseMove)
  document.addEventListener('mouseup', handleMouseUp)
}

const applyHeight = (value) => {
  const normalized = Math.max(0, value)
  if (normalized <= collapseThreshold) {
    if (!isCollapsed.value) {
      isCollapsed.value = true
      emit('collapse', true)
    }
    height.value = 0
    emit('resize', 0)
  } else {
    const finalHeight = Math.max(props.minHeight, normalized)
    if (isCollapsed.value) {
      isCollapsed.value = false
      emit('collapse', false)
    }
    // During drag, height should track the pointer directly (no easing).
    if (height.value !== finalHeight) {
      height.value = finalHeight
      emit('resize', finalHeight)
    }
  }
}

const restorePanelFromIcon = () => {
  if (!isCollapsed.value) return
  const restored = props.defaultHeight || props.minHeight
  isCollapsed.value = false
  height.value = restored
  emit('collapse', false)
  emit('resize', restored)
}

// Watch for layout mode changes
watch(() => props.layoutMode, (newMode) => {
  // Adjust behavior based on layout mode
  if (newMode === 'stack') {
    // Full width in stack mode
  } else if (newMode === 'split') {
    // Adjust for split mode
  }
})

onMounted(() => {
  if (props.defaultHeight) {
    height.value = props.defaultHeight
  }
})

onUnmounted(() => {
  // Cleanup drag listeners
})
</script>

<style scoped>
.panel-wrapper {
  position: relative;
  width: 100%;
  display: flex;
  flex-direction: column;
}

.panel-section {
  transition: height 0.15s ease-out;
  will-change: height;
}

 .panel-dragging .panel-section {
   transition: none;
 }

/* Manilla Envelope Tab */
.panel-tab {
  height: 16px;
  padding: 4px 12px 4px 8px;
  display: flex;
  align-items: center;
  gap: 6px;
  font-family: 'm6x11plus', 'Consolas', monospace;
  font-size: 12px;
  font-weight: normal;
  color: #fff;
  cursor: grab;
  user-select: none;
  touch-action: none;
  transition: all 0.2s ease;
}

.panel-tab:hover {
  filter: brightness(1.2);
  cursor: grab;
}

.panel-tab:active {
  cursor: grabbing;
}

/* Color variants for tabs */
.panel-tab-red {
  background: var(--balatro-red);
}

.panel-tab-blue {
  background: var(--balatro-blue);
}

.panel-tab-green {
  background: var(--balatro-green);
}

.panel-tab-purple {
  background: var(--balatro-purple);
}

.tab-label {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.tab-badge {
  background: rgba(0,0,0,0.3);
  color: #fff;
  font-family: 'm6x11plus', 'Consolas', monospace;
  font-size: 10px;
  font-weight: normal;
  padding: 1px 4px;
  border-radius: 3px;
}

.tab-collapse-btn {
  background: none;
  border: none;
  color: #fff;
  cursor: pointer;
  padding: 0 4px;
  font-size: 12px;
  line-height: 1;
  opacity: 0.9;
  transition: opacity 0.2s;
}

.tab-collapse-btn:hover {
  opacity: 1;
}

/* Full-Width Colored Border Panel */
.panel-section {
  position: relative;
  width: 100%;
  background: var(--panel-bg, #3a5055);
  border-radius: 0 6px 6px 6px;
  overflow: hidden;
  transition: height 0.2s ease-out;
  box-shadow: inset 0 0 0 1px var(--color-dark-red);
}

.panel-section-red {
  box-shadow: inset 0 0 0 1px var(--balatro-dark-red);
}

.panel-section-blue {
  box-shadow: inset 0 0 0 1px var(--balatro-dark-blue);
}

.panel-section-green {
  box-shadow: inset 0 0 0 1px var(--balatro-dark-green);
}

.panel-section-purple {
  box-shadow: inset 0 0 0 1px var(--balatro-dark-purple);
}

.panel-content {
  padding: 8px;
  height: 100%;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

/* Collapsed state */
.panel-collapsed .panel-section {
  height: 0 !important;
  min-height: 0 !important;
  border-width: 0;
  border-radius: 0;
  overflow: hidden;
}

/* Layout mode adjustments */
.panel-wrapper[data-layout="split"] .panel-section {
  /* Adjustments for split mode */
  border-radius: 4px;
}

.panel-wrapper[data-layout="stack"] .panel-section {
  /* Full width in stack mode */
  border-radius: 0;
  overflow: hidden;
}
</style>

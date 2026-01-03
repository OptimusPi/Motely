<template>
  <div
    ref="panelWrapper"
    class="panel-wrapper"
    :data-layout="layoutMode"
    :class="{
      'fill-remaining': fillRemaining
    }"
  >
    <!-- Expanded panel -->
    <div
      ref="panel"
      class="panel-section"
      :class="[`panel-section-${color}`]"
      :style="panelStyle"
    >
      <!-- Colored top border acts as drag handle for panels below this one -->
      <div 
        v-if="showTopGrab"
        class="panel-top-drag-handle"
        :style="{ borderTop: `3px solid var(--balatro-blue)` }"
        @pointerdown="handleTopDrag"
      ></div>
      
      <!-- Panel tab/label area -->
      <div class="panel-tab-area">
        <span class="panel-label">{{ label }}</span>
        <span v-if="badge" class="panel-badge">{{ badge }}</span>
      </div>
      
      <div class="panel-content">
        <slot />
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'

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
  layoutMode: {
    type: String,
    default: 'stack',
    validator: (v) => ['stack', 'split'].includes(v)
  },
  fillRemaining: {
    type: Boolean,
    default: false
  },
  showTopGrab: {
    type: Boolean,
    default: false
  },
  resizeIndex: {
    type: Number,
    default: null
  }
});

const emit = defineEmits(['resize', 'collapse', 'top-drag']);

const panelWrapper = ref(null);
const panel = ref(null);
const height = ref(props.defaultHeight || props.minHeight);

const panelStyle = computed(() => {
  if (props.fillRemaining) {
    return {
      flex: '1 1 0',
      '--panel-color': `var(--balatro-${props.color})`
    };
  }

  // NO LIMITATIONS - use whatever height the user drags to!
  return {
    flex: `0 0 ${height.value}px`,
    height: `${height.value}px`,
    '--panel-color': `var(--balatro-${props.color})`
  };
});

const toggleCollapse = () => {
  // Placeholder for future collapse/expand behavior
};

const handleTopDrag = (event) => {
  // Emit the drag event to parent so it can handle stack resize
  emit('top-drag', event)
}

let isPanelDragging = false
let panelStartY = 0
let panelStartHeight = 0

const startResize = (event) => {
  if (event.button !== 0 && event.type !== 'touchstart') return
  event.preventDefault()
  event.stopPropagation()

  isPanelDragging = true
  panelStartY = event.clientY || (event.touches && event.touches[0]?.clientY) || 0
  panelStartHeight = height.value

  document.body.style.cursor = 'ns-resize'
  document.body.style.userSelect = 'none'

  // Use document-level listeners for smooth dragging (like SplitPane)
  document.addEventListener('mousemove', handlePanelMove)
  document.addEventListener('touchmove', handlePanelMove, { passive: false })
  document.addEventListener('mouseup', handlePanelEnd)
  document.addEventListener('touchend', handlePanelEnd)
  document.addEventListener('touchcancel', handlePanelEnd)
}

const handlePanelMove = (moveEvent) => {
  if (!isPanelDragging) return

  const currentY = moveEvent.clientY || (moveEvent.touches && moveEvent.touches[0]?.clientY) || 0
  const deltaY = currentY - panelStartY

  // NO LIMITATIONS - let it drag freely!
  const newHeight = panelStartHeight + deltaY
  height.value = newHeight
  emit('resize', newHeight)

  moveEvent.preventDefault()
}

const handlePanelEnd = () => {
  if (!isPanelDragging) return

  isPanelDragging = false
  document.body.style.cursor = ''
  document.body.style.userSelect = ''

  // Remove document-level listeners
  document.removeEventListener('mousemove', handlePanelMove)
  document.removeEventListener('touchmove', handlePanelMove)
  document.removeEventListener('mouseup', handlePanelEnd)
  document.removeEventListener('touchend', handlePanelEnd)
  document.removeEventListener('touchcancel', handlePanelEnd)
}

onMounted(() => {
  if (props.defaultHeight) {
    height.value = props.defaultHeight;
  }
});

watch(
  () => props.defaultHeight,
  (newHeight) => {
    if (typeof newHeight === 'number' && !Number.isNaN(newHeight)) {
      height.value = newHeight;
    }
  }
);
</script>

<style scoped>
.panel-wrapper {
  position: relative;
  width: 100%;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  flex: 0 0 auto;
  min-height: 0;
  max-height: 100%; /* Never exceed container */
  margin: 0;
  padding: 0;
}

.panel-wrapper.fill-remaining {
  flex: 1 1 0;
  min-height: 0;
  max-height: 100%; /* Force to fit */
  overflow: hidden; /* Prevent overflow */
}

.panel-section {
  position: relative;
  width: 100%;
  box-sizing: border-box;
  background: var(--dark-bg);
  border-left: 3px solid var(--balatro-blue);
  border-right: 3px solid var(--balatro-blue);
  border-bottom: 3px solid var(--balatro-blue);
  border-top: none; /* Top border removed - panels touch each other */
  box-shadow: inset 0 0 10px rgba(0, 0, 0, 0.3);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
  max-height: 100%; /* Never exceed container */
  margin: 0; /* No gaps between panels */
  padding: 0;
}

.panel-top-drag-handle {
  position: absolute;
  top: -3px; /* Overlap with panel above */
  left: 0;
  right: 0;
  height: 3px;
  cursor: ns-resize;
  z-index: 20;
  background: transparent;
  touch-action: none;
  user-select: none;
}

.panel-tab-area {
  height: 24px;
  padding: 2px 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--panel-color);
  color: #fff;
  font-size: 14px;
  font-weight: normal;
  user-select: none;
  flex-shrink: 0;
}

.panel-label {
  flex: 1;
}

.panel-badge {
  display: inline-block;
  padding: 2px 6px;
  background: rgba(255, 255, 255, 0.2);
  border-radius: 12px;
  font-size: 12px;
  min-width: 20px;
  text-align: center;
}
</style>
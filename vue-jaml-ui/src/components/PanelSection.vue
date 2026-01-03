<template>
  <div
    ref="panelWrapper"
    class="panel-wrapper"
    :data-layout="layoutMode"
    :class="{
      'fill-remaining': fillRemaining
    }"
  >
    <!-- Manila-style tab (title lives here, not on the grab bar) -->
    <div
      class="panel-tab"
      :class="[`panel-tab-${tabAlign}`]"
      :style="{ '--panel-color': `var(--balatro-${color})` }"
      draggable="true"
      @dragstart="handleTabDragStart"
      @dragend="handleTabDragEnd"
      @dragover.prevent
      @drop="handleTabDrop"
    >
      <span class="panel-tab-label">{{ label }}</span>
      <span v-if="badge" class="panel-tab-badge">{{ badge }}</span>
      <button
        v-if="canDuplicate"
        class="panel-tab-duplicate"
        @click.stop="handleDuplicate"
        aria-label="Duplicate panel"
        title="Duplicate panel"
      >+</button>
    </div>

    <div
      ref="panel"
      class="panel-section"
      :class="[`panel-section-${color}`]"
      :style="panelStyle"
    >
      <!-- The top colored edge IS the grab bar (hitbox matches the visible edge) -->
      <div 
        class="panel-top-grab"
        @pointerdown.prevent.stop="handleTopDrag"
      ></div>
      
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
  tabAlign: {
    type: String,
    default: 'left',
    validator: (v) => ['left', 'right'].includes(v)
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
  panelId: {
    type: String,
    default: null
  },
  canDuplicate: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['resize', 'collapse', 'top-drag', 'duplicate', 'move-to-side']);

const handleDuplicate = () => {
  emit('duplicate')
}

const handleTabDragStart = (event) => {
  if (props.panelId) {
    event.dataTransfer.setData('text/plain', props.panelId)
    event.dataTransfer.effectAllowed = 'move'
    event.currentTarget.classList.add('dragging')
  }
}

const handleTabDragEnd = (event) => {
  event.currentTarget.classList.remove('dragging')
}

const handleTabDrop = (event) => {
  event.preventDefault()
  const draggedPanelId = event.dataTransfer.getData('text/plain')
  if (draggedPanelId && draggedPanelId !== props.panelId) {
    // Determine target side based on tab alignment
    const targetSide = props.tabAlign === 'left' ? 'left' : 'right'
    emit('move-to-side', targetSide)
  }
}

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

const handleTopDrag = (event) => {
  // Emit the drag event to parent so it can handle stack resize
  emit('top-drag', event)
}

// Handle tab dragging to move panels between sides
let isTabDragging = false
let tabStartX = 0

const handleTabDrag = (event) => {
  if (event.button !== 0 && event.type !== 'touchstart') return
  event.preventDefault()
  event.stopPropagation()
  
  isTabDragging = true
  tabStartX = event.clientX || (event.touches && event.touches[0]?.clientX) || 0
  
  document.addEventListener('mousemove', handleTabMove)
  document.addEventListener('touchmove', handleTabMove, { passive: false })
  document.addEventListener('mouseup', handleTabEnd)
  document.addEventListener('touchend', handleTabEnd)
  document.addEventListener('touchcancel', handleTabEnd)
}

const handleTabMove = (moveEvent) => {
  if (!isTabDragging) return
  
  const currentX = moveEvent.clientX || (moveEvent.touches && moveEvent.touches[0]?.clientX) || 0
  const deltaX = currentX - tabStartX
  
  // Emit tab drag event to parent
  emit('tab-drag', { deltaX, clientX: currentX })
  
  moveEvent.preventDefault()
}

const handleTabEnd = () => {
  isTabDragging = false
  document.removeEventListener('mousemove', handleTabMove)
  document.removeEventListener('touchmove', handleTabMove)
  document.removeEventListener('mouseup', handleTabEnd)
  document.removeEventListener('touchend', handleTabEnd)
  document.removeEventListener('touchcancel', handleTabEnd)
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
}

.panel-wrapper.fill-remaining {
  flex: 1 1 0;
}

.panel-section {
  position: relative;
  width: 100%;
  box-sizing: border-box;
  background: var(--dark-bg);

  /* Style contract (A): Balatro frame — thick top border, thin sides/bottom, flat/square */
  --panel-top-h: 10px;
  border-top: var(--panel-top-h) solid var(--panel-color);
  border-left: 4px solid var(--panel-color);
  border-right: 4px solid var(--panel-color);
  border-bottom: 4px solid var(--panel-color);
  border-radius: 0;
  box-shadow: none;
  overflow: visible; /* Allow grab bar to extend above border */
  display: flex;
  flex-direction: column;
  min-height: 0;
  max-height: 100vh; /* Ensure panels never exceed viewport */
}

.panel-top-grab {
  position: absolute;
  top: -2px; /* Scoot up to align with colored border edge */
  left: 0;
  right: 0;
  height: calc(var(--panel-top-h) + 2px); /* Extend to cover the border (10px + 2px overlap) */
  cursor: ns-resize;
  z-index: 40;
  background: transparent;
  touch-action: none;
  user-select: none;
}

.panel-tab {
  position: absolute;
  top: -28px;
  height: 28px;
  width: 200px; /* Fixed width */
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  box-sizing: border-box;
  background: var(--panel-color);
  border: 0;
  border-radius: 6px 6px 0 0;
  color: #fff;
  font-family: 'm6x11plus', monospace;
  font-size: 18px;
  font-weight: normal;
  user-select: none;
  pointer-events: auto; /* Allow dragging and clicking */
  z-index: 60; /* above grab bar */
  box-shadow: none;
  cursor: move;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.panel-tab.dragging {
  opacity: 0.5;
}

.panel-tab-left {
  left: 8px;
}

.panel-tab-right {
  right: 8px;
}

.panel-tab-badge {
  background: rgba(0, 0, 0, 0.22);
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 14px;
  font-weight: normal;
}

.panel-content {
  flex: 1;
  overflow: auto; /* Allow scrolling if content exceeds panel height */
  min-height: 0;
}

/* Colored tabs */
.panel-section-red {
  --panel-color: var(--balatro-red);
}

.panel-section-blue {
  --panel-color: var(--balatro-blue);
}

.panel-section-green {
  --panel-color: var(--balatro-green);
}

.panel-section-purple {
  --panel-color: var(--balatro-purple);
}
</style>

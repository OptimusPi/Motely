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
      <!-- Resize handle at top (mobile-friendly 24px+ area) -->
      <div 
        v-if="!fillRemaining"
        class="panel-resize-handle"
        @pointerdown="startResize"
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
  layoutMode: {
    type: String,
    default: 'stack',
    validator: (v) => ['stack', 'split'].includes(v)
  },
  fillRemaining: {
    type: Boolean,
    default: false
  }
});

const emit = defineEmits(['resize', 'collapse']);

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
}

.panel-wrapper.fill-remaining {
  flex: 1 1 0;
}

.panel-section {
  position: relative;
  width: 100%;
  box-sizing: border-box;
  background: var(--panel-dark, #2c3e50);
  border-top: 3px solid var(--panel-color);
  border-left: 3px solid var(--panel-color);
  border-right: 3px solid var(--panel-color);
  border-bottom: 3px solid var(--panel-color);
  box-shadow: inset 0 0 10px rgba(0, 0, 0, 0.3);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
  max-height: 100vh; /* Ensure panels never exceed viewport */
}

.panel-content {
  flex: 1;
  overflow: auto; /* Allow scrolling if content exceeds panel height */
  min-height: 0;
}

.panel-resize-handle {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 24px;
  cursor: ns-resize;
  z-index: 10;
  background: transparent;
  touch-action: none;
  user-select: none;
}
</style>
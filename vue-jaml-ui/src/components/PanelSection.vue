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
      minHeight: props.minHeight + 'px',
      '--panel-color': `var(--balatro-${props.color})`
    };
  }

  return {
    flex: `0 0 ${height.value}px`,
    height: `${height.value}px`,
    minHeight: props.minHeight + 'px',
    '--panel-color': `var(--balatro-${props.color})`
  };
});

const toggleCollapse = () => {
  // Placeholder for future collapse/expand behavior
};

const startResize = (event) => {
  if (event.button !== 0) return
  event.preventDefault()
  event.stopPropagation()

  const handle = event.currentTarget
  handle?.setPointerCapture?.(event.pointerId)
  document.body.style.cursor = 'ns-resize'
  document.body.style.userSelect = 'none'

  const startY = event.clientY
  const startHeight = height.value

  const onMove = (moveEvent) => {
    const deltaY = moveEvent.clientY - startY
    const newHeight = Math.max(props.minHeight, startHeight + deltaY)
    height.value = newHeight
    emit('resize', newHeight)
  }

  const onUp = () => {
    handle?.releasePointerCapture?.(event.pointerId)
    handle?.removeEventListener?.('pointermove', onMove)
    handle?.removeEventListener?.('pointerup', onUp)
    handle?.removeEventListener?.('pointercancel', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }

  handle?.addEventListener?.('pointermove', onMove)
  handle?.addEventListener?.('pointerup', onUp)
  handle?.addEventListener?.('pointercancel', onUp)
};

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
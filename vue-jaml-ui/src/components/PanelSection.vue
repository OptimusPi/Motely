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
      <!-- Panel tab/label area - THIS IS THE DIVIDER/GRAB HANDLE -->
      <!-- The colored tab bar IS the divider - dragging it resizes the panel ABOVE -->
      <div 
        class="panel-tab-area"
        :class="`panel-tab-${color}`"
        @pointerdown="handleTopDrag"
        @mousedown="handleTopDrag"
        @touchstart="handleTopDrag"
      >
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
  }
});

const emit = defineEmits(['resize', 'collapse', 'top-drag']); // top-drag: dragging tab resizes panel above

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
  // The colored tab bar IS the divider - dragging it resizes the panel ABOVE
  // Emit the drag event to parent so it can handle stack resize
  emit('top-drag', event)
}

// Removed unused startResize/handlePanelMove/handlePanelEnd - tab drag handled by parent via top-drag event

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

/* Removed panel-top-drag-handle - the tab IS the divider */

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
  cursor: ns-resize; /* This IS the divider - resize cursor */
  touch-action: none;
  position: relative;
  z-index: 10; /* Above panel content */
  /* Manila envelope tab effect - tab sticks out slightly */
  border-radius: 4px 4px 0 0;
  box-shadow: 0 -2px 4px rgba(0, 0, 0, 0.2);
}

.panel-tab-area:active {
  cursor: ns-resize;
  filter: brightness(1.2);
}

.panel-tab-area:hover {
  filter: brightness(1.1);
}

/* Colored tabs like manila envelopes */
.panel-tab-red {
  background: var(--balatro-red);
  --panel-color: var(--balatro-red);
}

.panel-tab-blue {
  background: var(--balatro-blue);
  --panel-color: var(--balatro-blue);
}

.panel-tab-green {
  background: var(--balatro-green);
  --panel-color: var(--balatro-green);
}

.panel-tab-purple {
  background: var(--balatro-purple);
  --panel-color: var(--balatro-purple);
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
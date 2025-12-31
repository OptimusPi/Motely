<template>
  <div
    ref="panelWrapper"
    class="panel-wrapper"
    :data-layout="layoutMode"
    :class="{
      'fill-remaining': fillRemaining
    }"
  >
    <!-- Manilla-style tab (title lives here, not on the grab bar) -->
    <div
      v-if="showTab"
      class="panel-tab"
      :class="[`panel-tab-${tabAlign}`]"
      :style="{ '--panel-color': `var(--balatro-${color})` }"
    >
      <span class="panel-tab-label">{{ label }}</span>
      <span v-if="badge" class="panel-tab-badge">{{ badge }}</span>
    </div>

    <div
      ref="panel"
      class="panel-section"
      :class="[`panel-section-${color}`]"
      :style="panelStyle"
    >
      <!-- The top colored edge IS the grab bar (hitbox matches the visible edge) -->
      <div
        v-if="showTopGrab"
        class="panel-top-grab"
        @pointerdown.prevent.stop="emit('topgrab', $event)"
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
  showTopGrab: {
    type: Boolean,
    default: false
  },
  showTab: {
    type: Boolean,
    default: true
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
  }
});

const emit = defineEmits(['resize', 'collapse', 'topgrab']);

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
  /* Style contract (A): Balatro frame — thick top border, thin sides/bottom, flat/square */
  --panel-top-h: 8px;
  border-top: var(--panel-top-h) solid var(--panel-color);
  border-left: 2px solid var(--panel-color);
  border-right: 2px solid var(--panel-color);
  border-bottom: 2px solid var(--panel-color);
  border-radius: 0;
  box-shadow: none;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
  max-height: 100vh; /* Ensure panels never exceed viewport */
}

.panel-top-grab {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: var(--panel-top-h);
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
  font-size: 14px;
  user-select: none;
  pointer-events: none; /* purely visual for now */
  z-index: 60; /* above stack-divider */
  box-shadow: none;
}

.panel-tab-left {
  left: 8px;
}

.panel-tab-right {
  right: 8px;
}

.panel-tab-badge {
  background: rgba(0, 0, 0, 0.22);
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
}

.panel-content {
  flex: 1;
  overflow: auto; /* Allow scrolling if content exceeds panel height */
  min-height: 0;
}
</style>
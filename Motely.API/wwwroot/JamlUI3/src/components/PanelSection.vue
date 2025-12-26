<template>
  <div
    ref="wrapper"
    class="panel-section"
    :style="{ flex: `0 0 ${height}px`, minHeight: `${minHeight}px` }"
  >
    <div
      ref="tab"
      class="panel-tab"
      :class="`panel-tab-${color}`"
      @dblclick="toggleCollapse"
    >
      <span>☰</span>
      <span>{{ label }}</span>
      <span v-if="badge" class="badge">{{ badge }}</span>
    </div>
    <div
      ref="content"
      class="panel-content"
      :style="{ display: isCollapsed ? 'none' : 'flex' }"
    >
      <slot />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useInteract } from '../composables/useInteract'

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
  }
})

const emit = defineEmits(['resize'])

const wrapper = ref(null)
const tab = ref(null)
const content = ref(null)
const height = ref(props.defaultHeight || props.minHeight)
const isCollapsed = ref(false)
const { makeDraggable, init } = useInteract()
let cleanup = null

const toggleCollapse = () => {
  isCollapsed.value = !isCollapsed.value
  if (isCollapsed.value) {
    height.value = 24
  } else {
    height.value = props.defaultHeight || props.minHeight
  }
  emit('resize', height.value)
}

onMounted(async () => {
  if (props.defaultHeight) {
    height.value = props.defaultHeight
  }
  
  await new Promise(resolve => setTimeout(resolve, 100))
  
  // Initialize interact.js
  await init()
  
  // Setup drag handler
  if (tab.value && wrapper.value) {
    cleanup = await makeDraggable(tab.value, {
      axis: 'y',
      onMove: (event) => {
        const newHeight = Math.max(props.minHeight, height.value + event.dy)
        if (newHeight > 0 && newHeight < 10000) {
          height.value = newHeight
          emit('resize', newHeight)
        }
      }
    })
  }
})

onUnmounted(() => {
  if (cleanup) {
    cleanup()
    cleanup = null
  }
})
</script>

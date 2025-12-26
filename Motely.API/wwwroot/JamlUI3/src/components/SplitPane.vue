<template>
  <div
    ref="container"
    class="split-pane"
    :class="{
      'split-pane-vertical': split === 'vertical',
      'split-pane-horizontal': split === 'horizontal'
    }"
  >
    <div
      ref="leftPane"
      class="split-pane-left"
      :style="{ flex: `0 0 ${leftPercent}%` }"
    >
      <slot name="left" />
    </div>

    <div
      ref="splitter"
      class="splitter"
      :class="{
        'splitter-vertical': split === 'vertical',
        'splitter-horizontal': split === 'horizontal'
      }"
    />

    <div
      ref="rightPane"
      class="split-pane-right"
      :style="{ flex: 1 }"
    >
      <slot name="right" />
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useInteract } from '../composables/useInteract'

const props = defineProps({
  split: {
    type: String,
    default: 'vertical',
    validator: (v) => ['vertical', 'horizontal'].includes(v)
  },
  defaultPercent: {
    type: Number,
    default: 50
  },
  minPercent: {
    type: Number,
    default: 10
  },
  maxPercent: {
    type: Number,
    default: 90
  }
})

const emit = defineEmits(['resize'])

const container = ref(null)
const leftPane = ref(null)
const splitter = ref(null)

const leftPercent = ref(props.defaultPercent)
const { makeDraggable, init } = useInteract()
let cleanup = null

onMounted(async () => {
  // Load saved state
  const saved = localStorage.getItem('jamlui3-split')
  if (saved) {
    try {
      const parsed = JSON.parse(saved)
      if (parsed[props.split]) {
        leftPercent.value = parsed[props.split]
      }
    } catch (e) {
      console.warn('Failed to parse saved split state:', e)
    }
  }
  
  // Wait for DOM to be ready
  await new Promise(resolve => setTimeout(resolve, 100))
  
  // Initialize interact.js
  await init()
  
  // Setup drag handler
  if (splitter.value && container.value) {
    cleanup = await makeDraggable(splitter.value, {
      axis: props.split === 'vertical' ? 'x' : 'y',
      onMove: (event) => {
        const rect = container.value.getBoundingClientRect()
        const containerSize = props.split === 'vertical' ? rect.width : rect.height
        if (containerSize <= 0) return
        
        const delta = props.split === 'vertical' ? event.dx : event.dy
        const deltaPercent = (delta / containerSize) * 100
        const newPercent = Math.max(
          props.minPercent,
          Math.min(props.maxPercent, leftPercent.value + deltaPercent)
        )
        
        leftPercent.value = newPercent
        emit('resize', newPercent)
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

// Save state on change
watch(leftPercent, (newVal) => {
  try {
    const saved = JSON.parse(localStorage.getItem('jamlui3-split') || '{}')
    saved[props.split] = newVal
    localStorage.setItem('jamlui3-split', JSON.stringify(saved))
  } catch (e) {
    console.warn('Failed to save split state:', e)
  }
})
</script>

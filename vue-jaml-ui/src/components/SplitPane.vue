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
const splitter = ref(null)
const leftPercent = ref(props.defaultPercent)

let isDragging = false
let startPos = 0
let startPercent = 0

const handleStart = (e) => {
  isDragging = true
  startPercent = leftPercent.value
  
  if (props.split === 'vertical') {
    startPos = e.clientX || (e.touches && e.touches[0]?.clientX) || 0
  } else {
    startPos = e.clientY || (e.touches && e.touches[0]?.clientY) || 0
  }
  
  if (splitter.value) {
    splitter.value.classList.add('dragging')
  }
  document.body.style.cursor = props.split === 'vertical' ? 'col-resize' : 'row-resize'
  document.body.style.userSelect = 'none'
  
  e.preventDefault()
}

const handleMove = (e) => {
  if (!isDragging || !container.value) return
  
  const rect = container.value.getBoundingClientRect()
  const containerSize = props.split === 'vertical' ? rect.width : rect.height
  if (containerSize <= 0) return
  
  let currentPos = 0
  if (props.split === 'vertical') {
    currentPos = e.clientX || (e.touches && e.touches[0]?.clientX) || 0
  } else {
    currentPos = e.clientY || (e.touches && e.touches[0]?.clientY) || 0
  }
  
  const delta = currentPos - startPos
  const deltaPercent = (delta / containerSize) * 100
  let newPercent = startPercent + deltaPercent
  
  // Clamp to limits - but don't fight if already at limit
  if (newPercent < props.minPercent) {
    newPercent = props.minPercent
  } else if (newPercent > props.maxPercent) {
    newPercent = props.maxPercent
  }
  
  leftPercent.value = newPercent
  emit('resize', newPercent)
  
  e.preventDefault()
}

const handleEnd = () => {
  if (!isDragging) return
  
  isDragging = false
  if (splitter.value) {
    splitter.value.classList.remove('dragging')
  }
  document.body.style.cursor = ''
  document.body.style.userSelect = ''
}

onMounted(() => {
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
  
  // Setup native drag handlers
  if (splitter.value) {
    splitter.value.addEventListener('mousedown', handleStart)
    splitter.value.addEventListener('touchstart', handleStart, { passive: false })
  }
  
  document.addEventListener('mousemove', handleMove)
  document.addEventListener('touchmove', handleMove, { passive: false })
  document.addEventListener('mouseup', handleEnd)
  document.addEventListener('touchend', handleEnd)
})

onUnmounted(() => {
  if (splitter.value) {
    splitter.value.removeEventListener('mousedown', handleStart)
    splitter.value.removeEventListener('touchstart', handleStart)
  }
  document.removeEventListener('mousemove', handleMove)
  document.removeEventListener('touchmove', handleMove)
  document.removeEventListener('mouseup', handleEnd)
  document.removeEventListener('touchend', handleEnd)
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

<template>
  <div class="blueprint-container">
    <div class="blueprint-toolbar">
      <input
        v-model="localSeed"
        @keyup.enter="loadSeed"
        class="blueprint-input"
        placeholder="Enter seed..."
      />
      <button @click="loadSeed" class="btn btn-primary">Go</button>
    </div>
    <iframe
      ref="iframe"
      class="blueprint-iframe"
      :src="iframeSrc"
      sandbox="allow-scripts allow-same-origin allow-forms allow-popups"
    />
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  seed: {
    type: String,
    default: ''
  }
})

const emit = defineEmits(['update:seed'])

const localSeed = ref(props.seed)
const iframe = ref(null)
const iframeSrc = ref('about:blank')

watch(() => props.seed, (newVal) => {
  localSeed.value = newVal
})

watch(localSeed, (newVal) => {
  emit('update:seed', newVal)
})

const loadSeed = () => {
  const seed = localSeed.value.trim()
  if (!seed) return
  
  iframeSrc.value = `https://miaklwalker.github.io/Blueprint/#/seed/${encodeURIComponent(seed)}`
}
</script>



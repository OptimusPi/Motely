<template>
  <div class="editor-container">
    <div class="editor-toolbar">
      <select v-model="selectedFilter" @change="handleFilterChange" class="select">
        <option value="">New Filter</option>
        <optgroup
          v-for="group in filterGroups"
          :key="group.author"
          :label="group.label"
        >
          <option
            v-for="filter in group.filters"
            :key="filter.id"
            :value="filter.id"
          >
            {{ filter.name }}
          </option>
        </optgroup>
      </select>
      <button @click="handleNew" class="btn">+ New</button>
      <button @click="toggleEditor" class="btn">{{ editorMode === 'monaco' ? 'Text' : 'Monaco' }}</button>
      <button @click="handleFormat" class="btn">Format</button>
      <button @click="$emit('save')" class="btn">💾 Save</button>
    </div>
    <div class="editor-area">
      <div v-if="editorMode === 'monaco'" ref="monacoContainer" class="monaco-wrapper" />
      <textarea
        v-else
        v-model="localJaml"
        class="textarea-editor"
        placeholder="Enter JAML filter..."
        spellcheck="false"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, onUnmounted, nextTick } from 'vue'
import { useMonaco } from '../composables/useMonaco'
import { useApi } from '../composables/useApi'

const props = defineProps({
  jaml: {
    type: String,
    required: true
  }
})

const emit = defineEmits(['update:jaml', 'save', 'format'])

const monacoContainer = ref(null)
const editorMode = ref('text') // 'text' | 'monaco'
const selectedFilter = ref('')
const localJaml = ref(props.jaml)
const filterGroups = ref([])

const { createEditor, init: initMonaco } = useMonaco()
const { get } = useApi()

let monacoEditor = null
let monacoCleanup = null

// Sync localJaml with props
watch(() => props.jaml, (newVal) => {
  localJaml.value = newVal
  if (monacoEditor) {
    monacoEditor.setValue(newVal)
  }
})

// Sync localJaml to parent
watch(localJaml, (newVal) => {
  emit('update:jaml', newVal)
})

const handleFilterChange = async (e) => {
  const filterId = e.target.value
  if (!filterId) return
  
  try {
    const data = await get(`/filters/${encodeURIComponent(filterId)}`)
    localJaml.value = data.filterJaml || ''
    if (monacoEditor) {
      monacoEditor.setValue(localJaml.value)
    }
  } catch (e) {
    console.error('Failed to load filter:', e)
  }
}

const handleNew = () => {
  localJaml.value = `name: New Filter
deck: Red
stake: White
must:
  - joker: Blueprint
`
  if (monacoEditor) {
    monacoEditor.setValue(localJaml.value)
  }
  selectedFilter.value = ''
}

const toggleEditor = async () => {
  editorMode.value = editorMode.value === 'monaco' ? 'text' : 'monaco'
  
  if (editorMode.value === 'monaco') {
    await nextTick()
    await initMonacoEditor()
  } else {
    // Cleanup Monaco when switching to text mode
    if (monacoCleanup) {
      monacoCleanup()
      monacoCleanup = null
      monacoEditor = null
      window.monacoEditor = null
    }
  }
}

const initMonacoEditor = async () => {
  if (monacoEditor || !monacoContainer.value) return
  
  try {
    await initMonaco()
    const { editor, cleanup } = await createEditor(monacoContainer.value, {
      value: localJaml.value
    })
    
    monacoEditor = editor
    monacoCleanup = cleanup
    window.monacoEditor = editor
    
    editor.onDidChangeModelContent(() => {
      localJaml.value = editor.getValue()
    })
  } catch (e) {
    console.error('Failed to load Monaco:', e)
    editorMode.value = 'text'
  }
}

// Load filters
const loadFilters = async () => {
  try {
    const data = await get('/filters')
    const filters = data.filters || data || []
    
    // Group by author
    const groups = {}
    filters.forEach(f => {
      const author = f.author || 'Default'
      if (!groups[author]) {
        groups[author] = []
      }
      groups[author].push(f)
    })
    
    filterGroups.value = Object.keys(groups).map(author => ({
      author,
      label: author === 'Default' ? '(Default)' : `author: ${author}`,
      filters: groups[author]
    }))
  } catch (e) {
    console.error('Failed to load filters:', e)
  }
}

const handleFormat = async () => {
  try {
    const yaml = await import('js-yaml')
    const parsed = yaml.load(localJaml.value)
    const formatted = yaml.dump(parsed, { indent: 2 })
    localJaml.value = formatted
    if (monacoEditor) {
      monacoEditor.setValue(formatted)
    }
  } catch (e) {
    console.error('Format error:', e)
    alert('Failed to format JAML: ' + e.message)
  }
}

onMounted(() => {
  loadFilters()
})

onUnmounted(() => {
  if (monacoCleanup) {
    monacoCleanup()
    monacoCleanup = null
  }
  monacoEditor = null
  window.monacoEditor = null
})
</script>


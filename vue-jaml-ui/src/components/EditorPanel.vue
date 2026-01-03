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
      <button v-if="!isMobile" @click="toggleBuilder" class="btn">
        {{ showBuilder ? 'Hide Builder' : 'Show Builder' }}
      </button>
      <button v-if="!isMobile" @click="toggleEditor" class="btn">{{ editorMode === 'monaco' ? 'Text' : 'Monaco' }}</button>
      <button @click="handleFormat" class="btn">Format</button>
      <button @click="$emit('save')" class="btn">💾 Save</button>
    </div>

    <div class="editor-body" :class="{ 'builder-hidden': !showBuilder }">
      <div v-if="showBuilder" class="builder-pane">
        <JamlBuilder
          :jaml="localJaml"
          @update:jaml="handleBuilderUpdate"
        />
      </div>
      <div class="code-pane">
        <div class="code-header">
          <span>{{ editorMode === 'monaco' ? 'Monaco YAML' : 'Plain Text' }}</span>
          <small>Schema-aware autocomplete + manual editing</small>
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
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, onUnmounted, nextTick, computed } from 'vue'
import yaml from 'js-yaml'
import { useMonaco } from '../composables/useMonaco'
import { useApi } from '../composables/useApi'
import { useLayout } from '../composables/useLayout'
import JamlBuilder from './JamlBuilder.vue'

const props = defineProps({
  jaml: {
    type: String,
    required: true
  }
})

const emit = defineEmits(['update:jaml', 'save', 'format'])

const monacoContainer = ref(null)
const { windowWidth } = useLayout()
const isMobile = computed(() => windowWidth.value < 768)
// Default to textarea on mobile, monaco on desktop
const editorMode = ref(isMobile.value ? 'text' : 'monaco') // 'text' | 'monaco'
const showBuilder = ref(!isMobile.value) // Hide builder on mobile by default
const selectedFilter = ref('')
const localJaml = ref(props.jaml)
const filterGroups = ref([])
const loading = ref(false)
const error = ref(null)

const { createEditor, init: initMonaco } = useMonaco()
const { get } = useApi()

let monacoEditor = null
let monacoCleanup = null
let builderSyncing = false

watch(() => props.jaml, (newVal) => {
  if (newVal === localJaml.value) return
  localJaml.value = newVal
  if (monacoEditor && monacoEditor.getValue() !== newVal) {
    monacoEditor.setValue(newVal)
  }
})

watch(localJaml, (newVal) => {
  if (builderSyncing) return
  emit('update:jaml', newVal)
})

const handleBuilderUpdate = (val) => {
  if (val === localJaml.value) return
  builderSyncing = true
  localJaml.value = val
  if (monacoEditor && monacoEditor.getValue() !== val) {
    monacoEditor.setValue(val)
  }
  builderSyncing = false
}

const handleFilterChange = async (e) => {
  const filterId = e.target.value
  if (!filterId) return
  
  try {
    const data = await get(`/filters/${encodeURIComponent(filterId)}`)
    localJaml.value = data.filterJaml || ''
    if (monacoEditor) {
      monacoEditor.setValue(localJaml.value)
    }
  } catch (err) {
    console.error('Failed to load filter:', err)
  }
}

const handleNew = () => {
  localJaml.value = `name: New Filter
deck: Red
stake: White
must:
  - type: Joker
    value: Blueprint
`
  if (monacoEditor) {
    monacoEditor.setValue(localJaml.value)
  }
  selectedFilter.value = ''
}

const toggleBuilder = () => {
  showBuilder.value = !showBuilder.value
}

const toggleEditor = async () => {
  editorMode.value = editorMode.value === 'monaco' ? 'text' : 'monaco'
  
  if (editorMode.value === 'monaco') {
    await nextTick()
    await initMonacoEditor()
  } else if (monacoCleanup) {
    monacoCleanup()
    monacoCleanup = null
    monacoEditor = null
    window.monacoEditor = null
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
      const value = editor.getValue()
      if (value !== localJaml.value) {
        localJaml.value = value
      }
    })
  } catch (err) {
    console.error('Failed to load Monaco:', err)
    editorMode.value = 'text'
  }
}

const loadFilters = async () => {
  loading.value = true
  error.value = null
  try {
    const data = await get('/filters')
    let filters = data.filters || data || []
    
    if (data?._fallback) {
      // Dev fallback when API is down
      filters = []
      console.warn('API unavailable: no filters loaded')
    }
    
    const groups = {}
    if (Array.isArray(filters)) {
      filters.forEach((f) => {
        const author = f.author || 'Default'
        if (!groups[author]) {
          groups[author] = []
        }
        groups[author].push(f)
      })
    }
    
    filterGroups.value = Object.keys(groups).map((author) => ({
      author,
      label: author === 'Default' ? '(Default)' : `author: ${author}`,
      filters: groups[author]
    }))
  } catch (err) {
    console.error('Failed to load filters:', err)
    filterGroups.value = []
  } finally {
    loading.value = false
  }
}

const handleFormat = async () => {
  try {
    const parsed = yaml.load(localJaml.value)
    const formatted = yaml.dump(parsed, { indent: 2 })
    localJaml.value = formatted
    if (monacoEditor && monacoEditor.getValue() !== formatted) {
      monacoEditor.setValue(formatted)
    }
    emit('format')
  } catch (err) {
    console.error('Format error:', err)
    alert('Failed to format JAML: ' + err.message)
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

<style scoped>
.editor-container {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.editor-toolbar {
  display: flex;
  gap: 8px;
  padding: 8px;
  background: var(--panel);
  border-bottom: 1px solid var(--border);
}

.editor-body {
  flex: 1;
  display: flex;
  gap: 12px;
  padding: 12px;
  overflow: hidden;
}

.builder-pane,
.code-pane {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.editor-body.builder-hidden .builder-pane {
  display: none;
}

.editor-body.builder-hidden .code-pane {
  flex: 1 1 100%;
}

.code-header {
  display: flex;
  flex-direction: column;
  padding-bottom: 8px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  margin-bottom: 8px;
}

.code-header span {
  font-weight: normal;
}

.code-header small {
  opacity: 0.7;
}

.editor-area {
  flex: 1;
  position: relative;
  min-height: 200px;
}

.monaco-wrapper {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
}

/* Monaco editor uses its own font - don't override with m6x11 */
.monaco-wrapper :deep(.monaco-editor),
.monaco-wrapper :deep(.monaco-editor *) {
  font-family: 'Courier New', 'Consolas', 'Monaco', monospace !important; /* Coder-friendly monospace font for Monaco editor */
}

.textarea-editor {
  width: 100%;
  height: 100%;
  background: var(--dark-bg, #1e2b2d);
  color: var(--text-color, #fff);
  border: none;
  padding: 12px;
  font-family: 'Courier New', 'Consolas', 'Monaco', monospace; /* Coder-friendly monospace font for text editor */
  font-size: 14px;
  line-height: 1.6;
  resize: none;
  outline: none;
}

@media (max-width: 900px) {
  .editor-body {
    flex-direction: column;
  }

  .builder-pane,
  .code-pane {
    flex: unset;
  }
}

@media (max-width: 768px) {
  .editor-toolbar {
    flex-wrap: wrap;
    gap: 6px;
  }
  
  .btn {
    padding: 12px 16px; /* Larger touch targets */
    font-size: 14px;
    min-height: 44px; /* Ensure 44px touch target */
  }
  
  .select {
    padding: 12px 16px;
    font-size: 16px; /* Prevent zoom on iOS */
    min-height: 44px;
  }
  
  .editor-body {
    padding: 8px;
  }
  
  .textarea-editor {
    font-size: 16px; /* Prevent zoom on iOS */
    padding: 16px;
    line-height: 1.8; /* Better readability on mobile */
  }
}
</style>

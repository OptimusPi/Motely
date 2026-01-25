<template>
  <div class="soft-editor">
    <div class="editor-header">
      <div class="title-bubble">
        <input 
          v-model="formState.name" 
          class="title-input" 
          placeholder="Filter Name..."
          @input="emitChange"
        />
      </div>
    </div>

    <div class="sections-container">
      <div v-for="section in sections" :key="section.key" class="section-card" :class="section.key">
        <div class="section-badge">{{ section.label }}</div>
        
        <div class="clauses-list">
          <div 
            v-for="(clause, index) in formState[section.key]" 
            :key="index"
            class="clause-bubble"
            @click="editClause(section.key, index)"
          >
            <span class="clause-type">{{ clause.type || '???' }}</span>
            <span class="clause-separator">:</span>
            <span class="clause-value">{{ clause.value || 'any' }}</span>
            <button class="delete-mini" @click.stop="deleteClause(section.key, index)">×</button>
          </div>
          
          <button class="add-bubble" @click="addClause(section.key)">
            + Add {{ section.label }}
          </button>
        </div>
      </div>
    </div>

    <!-- Soft Modal for editing -->
    <div v-if="editing" class="soft-modal-overlay" @click="stopEditing">
      <div class="soft-modal" @click.stop>
        <div class="modal-header">
          <h3>Edit {{ editedSection }} Item</h3>
        </div>
        <div class="modal-body">
          <div class="edit-row">
            <span class="label">Type</span>
            <div class="pill-selector">
              <button 
                v-for="t in typeOptions" 
                :key="t"
                :class="{ active: editedClause.type === t }"
                @click="editedClause.type = t"
              >
                {{ t }}
              </button>
            </div>
          </div>

          <div class="edit-row">
            <span class="label">Value</span>
            <input v-model="editedClause.value" class="soft-input" placeholder="e.g. Blueprint" />
          </div>

          <div class="edit-row">
            <span class="label">Antes</span>
            <div class="pill-selector">
              <button 
                v-for="a in [1,2,3,4,5,6,7,8]" 
                :key="a"
                :class="{ active: (editedClause.antes || []).includes(a) }"
                @click="toggleAnte(a)"
              >
                {{ a }}
              </button>
              <button @click="editedClause.antes = '1..8'">1..8</button>
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button class="soft-btn save" @click="saveEdit">Done! ✨</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, watch, onMounted } from 'vue'
import yaml from 'js-yaml'

const props = defineProps({
  jaml: {
    type: String,
    required: true
  }
})

const emit = defineEmits(['update:jaml'])

const sections = [
  { key: 'must', label: 'Must Have' },
  { key: 'should', label: 'Cool to Have' },
  { key: 'mustNot', label: 'No Thanks' }
]

const typeOptions = ['Joker', 'TarotCard', 'PlanetCard', 'Voucher', 'SpectralCard', 'Tag']

const formState = reactive({
  name: '',
  must: [],
  should: [],
  mustNot: []
})

const editing = ref(false)
const editedSection = ref('')
const editedIndex = ref(-1)
const editedClause = reactive({})

const loadJaml = (text) => {
  try {
    const parsed = yaml.load(text) || {}
    formState.name = parsed.name || ''
    formState.must = parsed.must || []
    formState.should = parsed.should || []
    formState.mustNot = parsed.mustNot || []
  } catch (e) {
    console.warn('SoftEditor: Failed to parse JAML', e)
  }
}

const emitChange = () => {
  const output = {
    name: formState.name,
    must: formState.must,
    should: formState.should,
    mustNot: formState.mustNot
  }
  emit('update:jaml', yaml.dump(output, { indent: 2 }))
}

const addClause = (section) => {
  formState[section].push({ type: 'Joker', value: '' })
  editClause(section, formState[section].length - 1)
}

const editClause = (section, index) => {
  editedSection.value = section
  editedIndex.value = index
  const clause = formState[section][index]
  Object.assign(editedClause, JSON.parse(JSON.stringify(clause)))
  editing.value = true
}

const deleteClause = (section, index) => {
  formState[section].splice(index, 1)
  emitChange()
}

const saveEdit = () => {
  formState[editedSection.value][editedIndex.value] = JSON.parse(JSON.stringify(editedClause))
  editing.value = false
  emitChange()
}

const stopEditing = () => {
  editing.value = false
}

const toggleAnte = (a) => {
  if (!Array.isArray(editedClause.antes)) {
    editedClause.antes = []
  }
  const idx = editedClause.antes.indexOf(a)
  if (idx > -1) editedClause.antes.splice(idx, 1)
  else editedClause.antes.push(a)
}

watch(() => props.jaml, (newVal) => {
  // We only load if it's external change, but to simplify we'll just skip deeper checks for now
  // In a real app we'd check if it's different enough
  loadJaml(newVal)
}, { immediate: true })

</script>

<style scoped>
.soft-editor {
  padding: 20px;
  background: #fdf6e3; /* Soft cream background */
  border-radius: 24px;
  color: #5c4b37;
  font-family: 'm6x11', 'Courier New', sans-serif;
  height: 100%;
  overflow-y: auto;
  box-shadow: inset 0 0 20px rgba(0,0,0,0.05);
}

.editor-header {
  margin-bottom: 24px;
  display: flex;
  justify-content: center;
}

.title-bubble {
  background: white;
  padding: 12px 24px;
  border-radius: 99px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.1);
  border: 3px solid #e6dcc3;
}

.title-input {
  border: none;
  font-size: 1.5rem;
  text-align: center;
  color: #5c4b37;
  outline: none;
  width: min(300px, 80vw);
}

.sections-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.section-card {
  background: white;
  border-radius: 20px;
  padding: 24px;
  position: relative;
  box-shadow: 0 4px 6px rgba(0,0,0,0.05);
  border: 2px solid #eee;
}

.section-card.must { border-color: #ffb3b3; background: #fffefe; }
.section-card.should { border-color: #b3d9ff; background: #fefeff; }
.section-card.mustNot { border-color: #b3ffb3; background: #fafffa; }

.section-badge {
  position: absolute;
  top: -12px;
  left: 20px;
  background: #5c4b37;
  color: white;
  padding: 4px 12px;
  border-radius: 8px;
  font-size: 0.8rem;
  text-transform: uppercase;
  font-weight: bold;
}

.clauses-list {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 8px;
}

@media (max-width: 600px) {
  .clauses-list {
    flex-direction: column;
  }
  .clause-bubble {
    width: 100%;
    justify-content: space-between;
  }
}

.clause-bubble {
  background: #f8f9fa;
  border: 2px solid #ddd;
  padding: 8px 16px;
  border-radius: 12px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: transform 0.1s, box-shadow 0.1s;
}

.clause-bubble:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 8px rgba(0,0,0,0.1);
  border-color: #bbb;
}

.clause-type { font-weight: bold; color: #d32f2f; }
.clause-value { color: #1976d2; }

.delete-mini {
  border: none;
  background: #eee;
  color: #888;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
}

.add-bubble {
  background: #eee;
  border: 2px dashed #ccc;
  padding: 8px 16px;
  border-radius: 12px;
  color: #888;
  cursor: pointer;
}

.add-bubble:hover {
  background: #e5e5e5;
  color: #666;
}

/* Modal */
.soft-modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(92, 75, 55, 0.4);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 9999;
  padding: 20px;
}

.soft-modal {
  background: white;
  width: min(400px, 100%);
  border-radius: 32px;
  padding: min(32px, 5vw);
  box-shadow: 0 20px 40px rgba(0,0,0,0.2);
  border: 4px solid #5c4b37;
  max-height: 90vh;
  overflow-y: auto;
}

.edit-row {
  margin-bottom: 24px;
}

.edit-row .label {
  display: block;
  margin-bottom: 8px;
  font-weight: bold;
  text-transform: uppercase;
  font-size: 0.8rem;
  opacity: 0.6;
}

.pill-selector {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.pill-selector button {
  border: 2px solid #eee;
  background: white;
  padding: 6px 12px;
  border-radius: 12px;
  cursor: pointer;
}

.pill-selector button.active {
  background: #5c4b37;
  color: white;
  border-color: #5c4b37;
}

.soft-input {
  width: 100%;
  padding: 12px 16px;
  border: 2px solid #eee;
  border-radius: 12px;
  outline: none;
}

.soft-btn.save {
  width: 100%;
  background: #5c4b37;
  color: white;
  border: none;
  padding: 16px;
  border-radius: 16px;
  font-size: 1.1rem;
  cursor: pointer;
}
</style>

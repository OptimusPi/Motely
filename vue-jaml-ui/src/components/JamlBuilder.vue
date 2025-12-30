<template>
  <div class="jaml-builder">
    <section class="builder-section meta">
      <div class="section-header">
        <h3>Filter Details</h3>
      </div>
      <div class="field-grid">
        <label class="field">
          <span>Name</span>
          <input v-model="formState.name" placeholder="My Filter" />
        </label>
        <label class="field">
          <span>Author</span>
          <input v-model="formState.author" placeholder="pifreak" />
        </label>
        <label class="field">
          <span>Description</span>
          <textarea
            v-model="formState.description"
            rows="2"
            placeholder="Explain what this filter finds..."
          />
        </label>
      </div>

      <div class="pill-row">
        <div class="pill-group">
          <span class="pill-label">Deck</span>
          <button
            v-for="deck in deckOptions"
            :key="deck"
            :class="['pill', { active: formState.deck === deck }]"
            @click="formState.deck = deck"
          >
            {{ deck }}
          </button>
        </div>
        <div class="pill-group">
          <span class="pill-label">Stake</span>
          <button
            v-for="stake in stakeOptions"
            :key="stake"
            :class="['pill', { active: formState.stake === stake }]"
            @click="formState.stake = stake"
          >
            {{ stake }}
          </button>
        </div>
      </div>
    </section>

    <section class="builder-section defaults">
      <div class="section-header">
        <h3>Defaults</h3>
        <small>Applied to clauses when omitted</small>
      </div>
      <div class="chip-grid">
        <div class="chip-group">
          <div class="chip-label">Antes</div>
          <button
            v-for="ante in anteOptions"
            :key="`default-ante-${ante}`"
            :class="['chip', { active: formState.defaults.antes.includes(ante) }]"
            @click="toggleArrayValue(formState.defaults.antes, ante)"
          >
            {{ ante }}
          </button>
        </div>
        <div class="chip-group">
          <div class="chip-label">Pack Slots</div>
          <button
            v-for="slot in slotOptions"
            :key="`pack-slot-${slot}`"
            :class="['chip', { active: formState.defaults.packSlots.includes(slot) }]"
            @click="toggleArrayValue(formState.defaults.packSlots, slot)"
          >
            {{ slot }}
          </button>
        </div>
        <div class="chip-group">
          <div class="chip-label">Shop Slots</div>
          <button
            v-for="slot in slotOptions"
            :key="`shop-slot-${slot}`"
            :class="['chip', { active: formState.defaults.shopSlots.includes(slot) }]"
            @click="toggleArrayValue(formState.defaults.shopSlots, slot)"
          >
            {{ slot }}
          </button>
        </div>
        <label class="field score-field">
          <span>Default Score</span>
          <input
            type="number"
            min="0"
            v-model.number="formState.defaults.score"
          />
        </label>
      </div>
    </section>

    <section class="builder-section clauses">
      <ClauseBucket
        title="Must"
        description="Items that must appear"
        color="var(--balatro-red)"
        :clauses="formState.must"
        bucket="must"
        @add="handleAddClause"
        @edit="openClauseEditor"
        @delete="deleteClause"
      />
      <ClauseBucket
        title="Should"
        description="Bonus scoring items"
        color="var(--balatro-blue)"
        bucket="should"
        :clauses="formState.should"
        :show-score="true"
        @add="handleAddClause"
        @edit="openClauseEditor"
        @delete="deleteClause"
      />
      <ClauseBucket
        title="Must Not"
        description="Items to avoid"
        color="var(--balatro-green)"
        bucket="mustNot"
        :clauses="formState.mustNot"
        @add="handleAddClause"
        @edit="openClauseEditor"
        @delete="deleteClause"
      />
    </section>

    <div v-if="editingClause" class="clause-dialog-backdrop">
      <div class="clause-dialog">
        <div class="dialog-header">
          <h3>{{ editingClause.index === null ? 'Add Clause' : 'Edit Clause' }}</h3>
          <button class="close-btn" @click="closeClauseEditor">✕</button>
        </div>

        <div class="dialog-body">
          <label class="field">
            <span>Type</span>
            <select v-model="clauseForm.type">
              <option disabled value="">Select type</option>
              <option v-for="type in clauseTypeOptions" :key="type" :value="type">
                {{ type }}
              </option>
            </select>
          </label>
          <label class="field">
            <span>Value</span>
            <input
              v-model="clauseForm.value"
              list="clause-values"
              placeholder="Perkeo, Coupon, etc."
            />
            <datalist id="clause-values">
              <option
                v-for="val in suggestedValues"
                :key="`value-${val}`"
                :value="val"
              />
            </datalist>
          </label>
          <label class="field">
            <span>Label (optional)</span>
            <input v-model="clauseForm.label" placeholder="Custom label" />
          </label>

          <div class="chip-group">
            <div class="chip-label">Antes</div>
            <button
              v-for="ante in anteOptions"
              :key="`clause-ante-${ante}`"
              :class="['chip', { active: clauseForm.antes.includes(ante) }]"
              @click="toggleArrayValue(clauseForm.antes, ante)"
            >
              {{ ante }}
            </button>
          </div>

          <div class="chip-row">
            <label class="field">
              <span>Edition</span>
              <select v-model="clauseForm.edition">
                <option value="">Any</option>
                <option v-for="edition in editionOptions" :key="edition" :value="edition">
                  {{ edition }}
                </option>
              </select>
            </label>
            <label class="field">
              <span>Seal</span>
              <select v-model="clauseForm.seal">
                <option value="">Any</option>
                <option v-for="seal in sealOptions" :key="seal" :value="seal">{{ seal }}</option>
              </select>
            </label>
            <label class="field">
              <span>Enhancement</span>
              <select v-model="clauseForm.enhancement">
                <option value="">Any</option>
                <option v-for="enh in enhancementOptions" :key="enh" :value="enh">
                  {{ enh }}
                </option>
              </select>
            </label>
          </div>

          <div
            class="chip-row"
            v-if="['PlayingCard', 'StandardCard'].includes(clauseForm.type)"
          >
            <label class="field">
              <span>Rank</span>
              <select v-model="clauseForm.rank">
                <option value="">Any</option>
                <option v-for="rank in playingCardRanks" :key="rank" :value="rank">
                  {{ rank }}
                </option>
              </select>
            </label>
            <label class="field">
              <span>Suit</span>
              <select v-model="clauseForm.suit">
                <option value="">Any</option>
                <option v-for="suit in playingCardSuits" :key="suit" :value="suit">
                  {{ suit }}
                </option>
              </select>
            </label>
          </div>

          <div class="chip-group">
            <div class="chip-label">Sources</div>
            <button
              v-for="source in sourceOptions"
              :key="`clause-source-${source}`"
              :class="['chip', { active: clauseForm.sources.includes(source) }]"
              @click="toggleArrayValue(clauseForm.sources, source)"
            >
              {{ source }}
            </button>
          </div>

          <label class="field" v-if="editingClause.bucket === 'should'">
            <span>Score Weight</span>
            <input
              type="number"
              min="0"
              v-model.number="clauseForm.score"
            />
          </label>
        </div>

        <div class="dialog-footer">
          <button class="btn" @click="closeClauseEditor">Cancel</button>
          <button class="btn btn-primary" @click="saveClause">Save</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, nextTick, reactive, ref, watch } from 'vue'
import yaml from 'js-yaml'
import { preProcessJaml, postProcessJaml } from '../utils/jamlUtils'
import ClauseBucket from './ClauseBucket.vue'
import {
  anteOptions,
  clauseTypeOptions,
  deckOptions,
  editionOptions,
  enhancementOptions,
  playingCardRanks,
  playingCardSuits,
  sealOptions,
  slotOptions,
  sourceOptions,
  stakeOptions,
  valueSuggestionsMap
} from '../constants/jamlOptions'

const props = defineProps({
  jaml: {
    type: String,
    default: ''
  }
})

const emit = defineEmits(['update:jaml'])

const blankFilter = () => ({
  name: 'My Filter',
  description: '',
  author: '',
  deck: 'Red',
  stake: 'White',
  defaults: {
    antes: [...anteOptions],
    packSlots: [...slotOptions],
    shopSlots: [...slotOptions],
    score: 1
  },
  must: [],
  should: [],
  mustNot: []
})

const createEmptyClause = () => ({
  type: '',
  value: '',
  label: '',
  antes: [],
  score: 1,
  edition: '',
  seal: '',
  enhancement: '',
  rank: '',
  suit: '',
  sources: []
})

const formState = reactive(blankFilter())
const editingClause = ref(null) // { bucket, index }
const clauseForm = reactive(createEmptyClause())

const toggleArrayValue = (arr, value) => {
  const idx = arr.indexOf(value)
  if (idx === -1) {
    arr.push(value)
  } else {
    arr.splice(idx, 1)
  }
}

const suggestedValues = computed(() => {
  return valueSuggestionsMap[clauseForm.type] || []
})

const openClauseEditor = ({ bucket, index = null }) => {
  editingClause.value = { bucket, index }
  Object.assign(clauseForm, createEmptyClause())
  if (index !== null) {
    const existing = formState[bucket][index]
    if (existing) {
      Object.assign(clauseForm, JSON.parse(JSON.stringify(existing)))
    }
  }
}

const closeClauseEditor = () => {
  editingClause.value = null
  Object.assign(clauseForm, createEmptyClause())
}

const saveClause = () => {
  if (!editingClause.value) return
  const target = formState[editingClause.value.bucket]
  const clause = JSON.parse(JSON.stringify(clauseForm))
  if (editingClause.value.index === null) {
    target.push(clause)
  } else {
    target.splice(editingClause.value.index, 1, clause)
  }
  closeClauseEditor()
}

const deleteClause = ({ bucket, index }) => {
  formState[bucket].splice(index, 1)
}

const handleAddClause = (bucket) => {
  openClauseEditor({ bucket, index: null })
}

const sanitizeFilter = (data) => {
  const base = blankFilter()
  return {
    ...base,
    ...data,
    defaults: {
      ...base.defaults,
      ...(data?.defaults || {}),
      antes: Array.isArray(data?.defaults?.antes) ? [...new Set(data.defaults.antes)] : [...base.defaults.antes],
      packSlots: Array.isArray(data?.defaults?.packSlots) ? data.defaults.packSlots : [...base.defaults.packSlots],
      shopSlots: Array.isArray(data?.defaults?.shopSlots) ? data.defaults.shopSlots : [...base.defaults.shopSlots],
      score: typeof data?.defaults?.score === 'number' ? data.defaults.score : base.defaults.score
    },
    must: Array.isArray(data?.must) ? data.must : [],
    should: Array.isArray(data?.should) ? data.should : [],
    mustNot: Array.isArray(data?.mustNot) ? data.mustNot : []
  }
}

let applyingFromYaml = false
let suppressEmit = false

const applyJamlToForm = (text) => {
  if (!text) return
  try {
    const preProcessed = preProcessJaml(text)
    const parsed = yaml.load(preProcessed) || {}
    const sanitized = sanitizeFilter(parsed)
    applyingFromYaml = true
    Object.assign(formState, sanitized)
    applyingFromYaml = false
  } catch (err) {
    console.warn('Invalid JAML for builder:', err)
  }
}

const emitJaml = () => {
  const output = {
    name: formState.name,
    description: formState.description,
    author: formState.author,
    deck: formState.deck,
    stake: formState.stake,
    defaults: JSON.parse(JSON.stringify(formState.defaults)),
    must: JSON.parse(JSON.stringify(formState.must)),
    should: JSON.parse(JSON.stringify(formState.should)),
    mustNot: JSON.parse(JSON.stringify(formState.mustNot))
  }
  suppressEmit = true
  const rawYaml = yaml.dump(output, { indent: 2 })
  const formattedJaml = postProcessJaml(rawYaml)
  emit('update:jaml', formattedJaml)
  Promise.resolve().then(() => {
    suppressEmit = false
  })
}

watch(() => props.jaml, (value) => {
  if (suppressEmit) return
  applyJamlToForm(value)
}, { immediate: true })

watch(formState, () => {
  if (applyingFromYaml) return
  emitJaml()
}, { deep: true })
</script>

<style scoped>
.jaml-builder {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.builder-section {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 16px;
  background: rgba(0, 0, 0, 0.15);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.section-header h3 {
  margin: 0;
  font-size: 1rem;
}

.section-header small {
  opacity: 0.7;
}

.field-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 12px;
}

.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 0.85rem;
}

.field input,
.field textarea,
.field select {
  background: rgba(0, 0, 0, 0.25);
  border: 1px solid rgba(255, 255, 255, 0.15);
  border-radius: 8px;
  padding: 8px 10px;
  color: white;
  font-size: 0.9rem;
  font-family: inherit;
}

.pill-row {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-top: 16px;
}

.pill-group {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.pill-label {
  text-transform: uppercase;
  letter-spacing: 0.1em;
  font-size: 0.7rem;
  opacity: 0.6;
}

.pill {
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 20px;
  padding: 6px 14px;
  background: transparent;
  color: white;
  cursor: pointer;
  font-size: 0.85rem;
}

.pill.active {
  background: rgba(255, 255, 255, 0.15);
  border-color: var(--balatro-gold);
}

.chip-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.chip-group {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
}

.chip-label {
  width: 110px;
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.1em;
  opacity: 0.6;
}

.chip {
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 999px;
  padding: 4px 10px;
  background: transparent;
  color: white;
  font-size: 0.8rem;
  cursor: pointer;
}

.chip.active {
  background: rgba(0, 0, 0, 0.3);
  border-color: var(--balatro-blue);
}

.chip-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 12px;
}

.clauses {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.clause-dialog-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.65);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 3000;
}

.clause-dialog {
  width: min(640px, 90vw);
  max-height: 90vh;
  background: #1e2b2d;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 16px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.dialog-header,
.dialog-footer {
  padding: 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.dialog-footer {
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  border-bottom: none;
}

.dialog-body {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
}

.close-btn {
  border: none;
  background: transparent;
  color: white;
  font-size: 1.2rem;
  cursor: pointer;
}

@media (max-width: 768px) {
  .field-grid {
    grid-template-columns: 1fr;
  }

  .chip-label {
    width: 80px;
  }
}
</style>

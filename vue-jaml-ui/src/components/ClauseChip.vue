<template>
  <div class="clause-chip">
    <div class="chip-main">
      <div class="chip-title">
        <span class="chip-type">{{ clause.type || 'Clause' }}</span>
        <span class="chip-value">{{ clause.label || clause.value || 'Any' }}</span>
      </div>
      <div class="chip-meta">
        <span v-if="clause.antes?.length">Ante {{ clause.antes.join(', ') }}</span>
        <span v-if="clause.edition">Edition {{ clause.edition }}</span>
        <span v-if="clause.seal">Seal {{ clause.seal }}</span>
        <span v-if="clause.score != null && showScore">Score {{ clause.score }}</span>
      </div>
    </div>
    <div class="chip-actions">
      <button class="icon-btn" title="Edit" @click="$emit('edit')">✎</button>
      <button class="icon-btn" title="Remove" @click="$emit('delete')">✕</button>
    </div>
  </div>
</template>

<script setup>
defineProps({
  clause: {
    type: Object,
    required: true
  },
  showScore: {
    type: Boolean,
    default: false
  }
})

defineEmits(['edit', 'delete'])
</script>

<style scoped>
.clause-chip {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 12px;
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  background: rgba(0, 0, 0, 0.15);
  color: white;
  gap: 12px;
}

.chip-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.chip-title {
  display: flex;
  gap: 8px;
  font-size: 0.9rem;
  align-items: center;
}

.chip-type {
  text-transform: uppercase;
  letter-spacing: 0.1em;
  font-size: 0.7rem;
  opacity: 0.7;
}

.chip-value {
  font-weight: 600;
}

.chip-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 0.75rem;
  opacity: 0.8;
}

.chip-actions {
  display: flex;
  gap: 6px;
}

.icon-btn {
  width: 28px;
  height: 28px;
  border-radius: 6px;
  border: 1px solid rgba(255, 255, 255, 0.15);
  background: rgba(255, 255, 255, 0.05);
  color: white;
  cursor: pointer;
  font-size: 0.8rem;
}

.icon-btn:hover {
  background: rgba(255, 255, 255, 0.15);
}
</style>

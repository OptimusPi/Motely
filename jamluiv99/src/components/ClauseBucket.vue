<template>
  <div class="bucket">
    <div class="bucket-header" :style="{ borderColor: color }">
      <div>
        <h4>{{ title }}</h4>
        <p>{{ description }}</p>
      </div>
      <button class="btn btn-secondary" @click="$emit('add', bucket)">
        + Clause
      </button>
    </div>

    <div v-if="clauses.length === 0" class="bucket-empty">
      No clauses yet
    </div>
    <div v-else class="bucket-body">
      <ClauseChip
        v-for="(clause, index) in clauses"
        :key="`${bucket}-${index}`"
        :clause="clause"
        :show-score="showScore"
        @edit="$emit('edit', { bucket, index })"
        @delete="$emit('delete', { bucket, index })"
      />
    </div>
  </div>
</template>

<script setup>
import ClauseChip from './ClauseChip.vue'

defineProps({
  title: String,
  description: String,
  color: {
    type: String,
    default: 'var(--balatro-blue)'
  },
  bucket: {
    type: String,
    required: true
  },
  clauses: {
    type: Array,
    default: () => []
  },
  showScore: {
    type: Boolean,
    default: false
  }
})

defineEmits(['add', 'edit', 'delete'])
</script>

<style scoped>
.bucket {
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  background: rgba(0, 0, 0, 0.2);
}

.bucket-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-left: 4px solid;
  padding-left: 12px;
}

.bucket-header h4 {
  margin: 0;
  font-size: 1rem;
}

.bucket-header p {
  margin: 0;
  font-size: 0.8rem;
  opacity: 0.8;
}

.bucket-empty {
  padding: 16px;
  border: 1px dashed rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  text-align: center;
  color: rgba(255, 255, 255, 0.6);
}

.bucket-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
</style>

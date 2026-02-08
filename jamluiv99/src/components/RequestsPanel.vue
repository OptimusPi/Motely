<template>
  <div class="requests-panel">
    <div class="requests-header">
      <h3>API Requests</h3>
      <button class="clear-btn" @click="clearRequests" :disabled="requests.length === 0">
        Clear
      </button>
    </div>
    <div class="requests-list" ref="requestsContainer">
      <div
        v-for="(req, index) in requests"
        :key="index"
        class="request-item"
        :class="`request-${req.status}`"
      >
        <div class="request-header">
          <span class="request-method">{{ req.method }}</span>
          <span class="request-url">{{ req.url }}</span>
          <span class="request-status" :class="`status-${req.status}`">
            {{ req.status }}
          </span>
        </div>
        <div v-if="req.error" class="request-error">{{ req.error }}</div>
        <div class="request-time">{{ formatTime(req.timestamp) }}</div>
      </div>
      <div v-if="requests.length === 0" class="requests-empty">
        No requests yet
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRequests } from '../composables/useRequests'

const { requests, clearRequests } = useRequests()
const requestsContainer = ref(null)

const formatTime = (timestamp) => {
  const date = new Date(timestamp)
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}
</script>

<style scoped>
.requests-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-color);
}

.requests-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(0, 0, 0, 0.2);
}

.requests-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: normal;
  color: var(--text-color);
}

.clear-btn {
  padding: 6px 12px;
  background: rgba(255, 76, 64, 0.2);
  border: 1px solid var(--balatro-red);
  border-radius: 4px;
  color: var(--balatro-red);
  font-family: 'm6x11plus', monospace;
  font-size: 12px;
  cursor: pointer;
  font-weight: normal;
}

.clear-btn:hover:not(:disabled) {
  background: rgba(255, 76, 64, 0.3);
}

.clear-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.requests-list {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.request-item {
  padding: 10px;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.05);
  border-left: 3px solid var(--muted);
  font-size: 13px;
}

.request-success {
  border-left-color: var(--balatro-green);
}

.request-error {
  border-left-color: var(--balatro-red);
}

.request-pending {
  border-left-color: var(--balatro-gold);
}

.request-header {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 4px;
}

.request-method {
  padding: 2px 6px;
  background: rgba(0, 147, 255, 0.2);
  border: 1px solid var(--balatro-blue);
  border-radius: 3px;
  font-size: 11px;
  font-weight: normal;
  color: var(--balatro-blue);
  font-family: 'm6x11plus', monospace;
}

.request-url {
  flex: 1;
  color: var(--text-color);
  word-break: break-all;
  font-family: 'm6x11plus', monospace;
}

.request-status {
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 11px;
  font-weight: normal;
  font-family: 'm6x11plus', monospace;
}

.status-success {
  background: rgba(66, 159, 121, 0.2);
  color: var(--balatro-green);
}

.status-error {
  background: rgba(255, 76, 64, 0.2);
  color: var(--balatro-red);
}

.status-pending {
  background: rgba(234, 186, 68, 0.2);
  color: var(--balatro-gold);
}

.request-error {
  color: var(--balatro-red);
  font-size: 12px;
  margin-top: 4px;
  padding: 4px;
  background: rgba(255, 76, 64, 0.1);
  border-radius: 3px;
}

.request-time {
  font-size: 11px;
  color: var(--muted);
  opacity: 0.7;
  margin-top: 4px;
}

.requests-empty {
  text-align: center;
  color: var(--muted);
  padding: 40px 20px;
  font-size: 14px;
}
</style>

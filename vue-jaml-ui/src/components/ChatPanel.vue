<template>
  <div class="chat-panel">
    <div class="chat-messages" ref="messagesContainer">
      <div
        v-for="(msg, index) in messages"
        :key="index"
        class="chat-message"
        :class="{ 'chat-message-own': msg.isOwn }"
      >
        <span class="chat-author">{{ msg.author }}:</span>
        <span class="chat-text">{{ msg.text }}</span>
        <span class="chat-time">{{ formatTime(msg.timestamp) }}</span>
      </div>
    </div>
    <div class="chat-input-container">
      <input
        v-model="inputMessage"
        class="chat-input"
        type="text"
        placeholder="Type a message..."
        @keyup.enter="sendMessage"
        @keyup.escape="inputMessage = ''"
      />
      <button class="chat-send" @click="sendMessage" :disabled="!inputMessage.trim()">
        Send
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, watch, nextTick } from 'vue'
import { useChat } from '../composables/useChat'

const { messages, sendMessage: sendChatMessage, connect, disconnect, isConnected } = useChat()

const inputMessage = ref('')
const messagesContainer = ref(null)

const sendMessage = () => {
  if (!inputMessage.value.trim()) return
  sendChatMessage(inputMessage.value)
  inputMessage.value = ''
}

const formatTime = (timestamp) => {
  const date = new Date(timestamp)
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

// Auto-scroll to bottom when new messages arrive
watch(messages, () => {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
    }
  })
}, { deep: true })

onMounted(() => {
  connect()
})

onUnmounted(() => {
  disconnect()
})
</script>

<style scoped>
.chat-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  background: var(--bg-color);
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.chat-message {
  display: flex;
  gap: 8px;
  padding: 6px 10px;
  border-radius: 4px;
  background: rgba(255, 255, 255, 0.05);
  font-size: 14px;
  line-height: 1.4;
}

.chat-message-own {
  background: rgba(0, 147, 255, 0.15);
  margin-left: 20px;
}

.chat-author {
  font-weight: normal;
  color: var(--balatro-blue);
  flex-shrink: 0;
}

.chat-text {
  flex: 1;
  color: var(--text-color);
  word-wrap: break-word;
}

.chat-time {
  font-size: 11px;
  color: var(--muted);
  opacity: 0.7;
  flex-shrink: 0;
  margin-left: auto;
}

.chat-input-container {
  display: flex;
  gap: 8px;
  padding: 12px;
  border-top: 1px solid rgba(255, 255, 255, 0.1);
  background: rgba(0, 0, 0, 0.2);
}

.chat-input {
  flex: 1;
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 4px;
  color: var(--text-color);
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
}

.chat-input:focus {
  outline: none;
  border-color: var(--balatro-blue);
  background: rgba(255, 255, 255, 0.15);
}

.chat-send {
  padding: 8px 16px;
  background: var(--balatro-blue);
  border: none;
  border-radius: 4px;
  color: #fff;
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  cursor: pointer;
  font-weight: normal;
}

.chat-send:hover:not(:disabled) {
  background: var(--balatro-gold);
}

.chat-send:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>

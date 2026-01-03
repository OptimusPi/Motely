<template>
  <div class="jaml-genie-panel">
    <div class="genie-chat">
      <div class="chat-messages" ref="messagesContainer">
        <div
          v-for="message in messages"
          :key="message.id"
          class="message"
          :class="{ 'user-message': message.role === 'user', 'genie-message': message.role === 'genie' }"
        >
          <div class="message-avatar">
            {{ message.role === 'user' ? '👤' : '🧞‍♂️' }}
          </div>
          <div class="message-content">
            <div class="message-text" v-html="formatMessage(message.content)"></div>
            <div class="message-time">{{ formatTime(message.timestamp) }}</div>
          </div>
        </div>

        <div v-if="isTyping" class="message genie-message typing">
          <div class="message-avatar">🧞‍♂️</div>
          <div class="message-content">
            <div class="typing-indicator">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
        </div>
      </div>

      <div class="chat-input">
        <textarea
          v-model="userInput"
          @keydown.enter.exact.prevent="sendMessage"
          @keydown.enter.shift.exact="userInput += '\n'"
          placeholder="Ask me about JAML filters, Balatro strategies..."
          class="input-field"
          :disabled="isTyping"
          ref="inputRef"
        ></textarea>
        <button
          @click="sendMessage"
          class="send-button"
          :disabled="!userInput.trim() || isTyping"
        >
          {{ isTyping ? '⏳' : '📤' }}
        </button>
      </div>
    </div>

    <div class="genie-sidebar">
      <div class="sidebar-section">
        <h3>Quick Actions</h3>
        <button @click="generateFilter" class="action-button" :disabled="isTyping">
          🎯 Generate Filter
        </button>
        <button @click="analyzeDeck" class="action-button" :disabled="isTyping">
          🃏 Analyze Deck
        </button>
        <button @click="suggestStrategy" class="action-button" :disabled="isTyping">
          🎲 Strategy Tips
        </button>
      </div>

      <div class="sidebar-section">
        <h3>Recent Filters</h3>
        <div class="filter-list">
          <div
            v-for="filter in recentFilters"
            :key="filter.id"
            class="filter-item"
            @click="loadFilter(filter)"
          >
            <div class="filter-name">{{ filter.name }}</div>
            <div class="filter-meta">{{ filter.created }}</div>
          </div>
          <div v-if="recentFilters.length === 0" class="filter-empty">
            No recent filters
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick, watch } from 'vue'
import { useApi } from '../composables/useApi'

const { get } = useApi()

const messages = ref([
  {
    id: 1,
    role: 'genie',
    content: 'Greetings, seeker of Balatro wisdom! 🧞‍♂️ I am the JAML Genie, your AI assistant for creating powerful seed filters. What mysteries of probability and poker shall we unravel today?',
    timestamp: new Date()
  }
])

const userInput = ref('')
const isTyping = ref(false)
const messagesContainer = ref(null)
const inputRef = ref(null)

const recentFilters = ref([])

const sendMessage = async () => {
  if (!userInput.value.trim() || isTyping.value) return

  const message = userInput.value.trim()
  userInput.value = ''

  // Add user message
  messages.value.push({
    id: Date.now(),
    role: 'user',
    content: message,
    timestamp: new Date()
  })

  // Scroll to bottom
  await nextTick()
  scrollToBottom()

  // Simulate Genie response
  isTyping.value = true

  setTimeout(async () => {
    const response = await generateGenieResponse(message)
    messages.value.push({
      id: Date.now() + 1,
      role: 'genie',
      content: response,
      timestamp: new Date()
    })

    isTyping.value = false
    await nextTick()
    scrollToBottom()
    inputRef.value?.focus()
  }, 1000 + Math.random() * 2000)
}

const generateGenieResponse = async (message) => {
  // Simulate AI response - in real implementation, this would call an AI service
  const responses = [
    "Ah, an excellent question! Let me weave some JAML magic for you... 🎭",
    "Your curiosity pleases me! Here's what the cards reveal about that filter... 🃏",
    "Mortal wisdom seeks eternal patterns. Consider this approach... 🔮",
    "The seeds of destiny hold many secrets. Try this configuration... 🌱",
    "Balatro's mysteries are deep, but your question cuts to the heart! Here's my insight... 💎"
  ]

  const baseResponse = responses[Math.floor(Math.random() * responses.length)]

  // Add context-specific responses
  const lowerMessage = message.toLowerCase()
  if (lowerMessage.includes('planet')) {
    return baseResponse + "\n\nFor planet-focused runs, try targeting specific constellations: `planets_required: ['neptune', 'pluto']` - the outer planets bring great power! 🪐"
  } else if (lowerMessage.includes('tag')) {
    return baseResponse + "\n\nTags are the wild cards of Balatro! Consider: `tags_required: ['top_up', 'orbital']` for maximum chaos and fun! 🎪"
  } else if (lowerMessage.includes('deck')) {
    return baseResponse + "\n\nDeck selection is destiny's foundation. The Red Deck offers power, Blue offers mystery, while Yellow dances with chance! 🎨"
  } else if (lowerMessage.includes('joker') || lowerMessage.includes('card')) {
    return baseResponse + "\n\nJokers are the heart of every run! Consider filters like: `must: [{ type: 'Joker', value: 'Blueprint' }]` to find specific jokers. The deck favors the prepared! 🃏"
  } else if (lowerMessage.includes('filter') || lowerMessage.includes('jaml')) {
    return baseResponse + "\n\nJAML filters let you search for seeds with specific conditions. Use `must` for required conditions, `should` for preferred ones, and `mustNot` to exclude. The genie's wisdom flows through YAML! ✨"
  }

  return baseResponse + "\n\nWhat other secrets of the deck shall we explore together? The genie is listening... 👂"
}

const scrollToBottom = () => {
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

const generateFilter = () => {
  const prompt = "Create a JAML filter for an exciting Balatro run"
  userInput.value = prompt
  sendMessage()
}

const analyzeDeck = () => {
  const prompt = "What deck should I choose for my next Balatro run?"
  userInput.value = prompt
  sendMessage()
}

const suggestStrategy = () => {
  const prompt = "Give me strategy tips for a successful Balatro run"
  userInput.value = prompt
  sendMessage()
}

const loadFilter = async (filter) => {
  try {
    const data = await get(`/filters/${encodeURIComponent(filter.id || filter.name)}`)
    if (data.filterJaml) {
      const prompt = `Load this filter: ${data.filterJaml.substring(0, 200)}...`
      userInput.value = prompt
      sendMessage()
    }
  } catch (err) {
    console.error('Failed to load filter:', err)
    messages.value.push({
      id: Date.now(),
      role: 'genie',
      content: `I couldn't load that filter, mortal. Perhaps it has vanished into the void? 🔮`,
      timestamp: new Date()
    })
  }
}

const loadRecentFilters = async () => {
  try {
    const filters = await get('/filters')
    if (Array.isArray(filters)) {
      recentFilters.value = filters.slice(0, 5).map(f => ({
        id: f.id || f.name,
        name: f.name || 'Unnamed Filter',
        created: f.createdAt ? new Date(f.createdAt).toLocaleDateString() : 'Unknown'
      }))
    }
  } catch (err) {
    console.warn('Could not load recent filters:', err)
  }
}

const formatMessage = (content) => {
  // Simple text formatting
  return content
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/`(.*?)`/g, '<code style="background: rgba(255,255,255,0.1); padding: 2px 4px; border-radius: 3px; font-family: monospace;">$1</code>')
    .replace(/\n/g, '<br>')
}

const formatTime = (timestamp) => {
  return timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

watch(messages, () => {
  nextTick(() => scrollToBottom())
}, { deep: true })

onMounted(async () => {
  await loadRecentFilters()
  await nextTick()
  inputRef.value?.focus()
})
</script>

<style scoped>
.jaml-genie-panel {
  display: flex;
  height: 100%;
  background: var(--dark-bg);
  color: var(--text-color);
  font-family: 'm6x11plus', monospace;
  overflow: hidden;
}

.genie-chat {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 12px;
  min-width: 0;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  margin-bottom: 12px;
  padding-right: 8px;
}

.chat-messages::-webkit-scrollbar {
  width: 6px;
}

.chat-messages::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.1);
  border-radius: 3px;
}

.chat-messages::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.3);
  border-radius: 3px;
}

.message {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  opacity: 0;
  animation: fadeIn 0.3s ease-out forwards;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.message-avatar {
  font-size: 1.2rem;
  flex-shrink: 0;
  margin-top: 2px;
}

.message-content {
  flex: 1;
  min-width: 0;
}

.message-text {
  background: rgba(255, 255, 255, 0.05);
  padding: 8px 12px;
  border-radius: 8px;
  line-height: 1.4;
  word-wrap: break-word;
  font-size: 13px;
}

.user-message .message-text {
  background: var(--balatro-red);
  margin-left: 20px;
}

.genie-message .message-text {
  background: rgba(155, 89, 182, 0.15);
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.message-time {
  font-size: 10px;
  opacity: 0.6;
  margin-top: 4px;
  margin-left: 12px;
}

.typing .message-content {
  display: flex;
  align-items: center;
}

.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 8px 12px;
  background: rgba(155, 89, 182, 0.15);
  border: 1px solid rgba(155, 89, 182, 0.3);
  border-radius: 8px;
}

.typing-indicator span {
  width: 6px;
  height: 6px;
  background: rgba(155, 89, 182, 0.8);
  border-radius: 50%;
  animation: typing 1.4s infinite ease-in-out;
}

.typing-indicator span:nth-child(2) { animation-delay: 0.2s; }
.typing-indicator span:nth-child(3) { animation-delay: 0.4s; }

@keyframes typing {
  0%, 60%, 100% { transform: translateY(0); }
  30% { transform: translateY(-8px); }
}

.chat-input {
  display: flex;
  gap: 8px;
  align-items: flex-end;
}

.input-field {
  flex: 1;
  min-height: 40px;
  max-height: 120px;
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 6px;
  color: var(--text-color);
  font-family: inherit;
  font-size: 13px;
  resize: vertical;
  outline: none;
  transition: border-color 0.2s;
}

.input-field:focus {
  border-color: var(--balatro-blue);
}

.input-field::placeholder {
  color: rgba(255, 255, 255, 0.4);
}

.send-button {
  padding: 8px 16px;
  background: var(--balatro-blue);
  border: none;
  border-radius: 6px;
  color: white;
  cursor: pointer;
  font-size: 1rem;
  transition: all 0.2s;
  flex-shrink: 0;
  height: 40px;
}

.send-button:hover:not(:disabled) {
  background: var(--balatro-gold);
  transform: translateY(-1px);
}

.send-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.genie-sidebar {
  width: 200px;
  min-width: 180px;
  border-left: 1px solid rgba(255, 255, 255, 0.1);
  padding: 12px;
  overflow-y: auto;
  background: rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.sidebar-section {
  flex-shrink: 0;
}

.sidebar-section h3 {
  font-family: 'm6x11plus', monospace;
  font-size: 14px;
  margin: 0 0 8px 0;
  color: var(--balatro-gold);
  font-weight: normal;
}

.action-button {
  width: 100%;
  padding: 8px 12px;
  margin-bottom: 6px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  color: var(--text-color);
  cursor: pointer;
  font-family: inherit;
  font-size: 12px;
  transition: all 0.2s;
}

.action-button:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.1);
  transform: translateY(-1px);
}

.action-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.filter-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.filter-item {
  padding: 8px 10px;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.filter-item:hover {
  background: rgba(255, 255, 255, 0.08);
  transform: translateX(2px);
}

.filter-name {
  font-weight: normal;
  font-size: 12px;
  margin-bottom: 2px;
}

.filter-meta {
  font-size: 10px;
  opacity: 0.6;
}

.filter-empty {
  font-size: 11px;
  opacity: 0.5;
  padding: 8px;
  text-align: center;
}
</style>

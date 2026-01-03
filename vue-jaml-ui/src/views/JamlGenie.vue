<template>
  <div class="jaml-genie">
    <div class="genie-header">
      <router-link to="/" class="back-link">
        <button title="Back to JAML">⬅️ JAML</button>
      </router-link>
      <h1 class="genie-title">🧞‍♂️ JAML Genie</h1>
      <div class="genie-subtitle">Your AI Assistant for JAML Filters</div>
    </div>

    <div class="genie-content">
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
            placeholder="Ask me about JAML filters, Balatro strategies, or anything else..."
            class="input-field"
            :disabled="isTyping"
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
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from 'vue'

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

const recentFilters = ref([
  { id: 1, name: 'High Score Seeker', created: '2 hours ago' },
  { id: 2, name: 'Planet Collector', created: '1 day ago' },
  { id: 3, name: 'Tag Hunter', created: '3 days ago' }
])

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
  if (message.toLowerCase().includes('planet')) {
    return baseResponse + "\n\nFor planet-focused runs, try targeting specific constellations: `planets_required: ['neptune', 'pluto']` - the outer planets bring great power! 🪐"
  } else if (message.toLowerCase().includes('tag')) {
    return baseResponse + "\n\nTags are the wild cards of Balatro! Consider: `tags_required: ['top_up', 'orbital']` for maximum chaos and fun! 🎪"
  } else if (message.toLowerCase().includes('deck')) {
    return baseResponse + "\n\nDeck selection is destiny's foundation. The Red Deck offers power, Blue offers mystery, while Yellow dances with chance! 🎨"
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

const loadFilter = (filter) => {
  // In real implementation, this would load the filter into the JAML editor
  console.log('Loading filter:', filter.name)
}

const formatMessage = (content) => {
  // Simple text formatting - in real implementation, use a proper markdown parser
  return content
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/`(.*?)`/g, '<code>$1</code>')
    .replace(/\n/g, '<br>')
}

const formatTime = (timestamp) => {
  return timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

onMounted(() => {
  // Focus input on mount
  nextTick(() => {
    const input = document.querySelector('.input-field')
    if (input) input.focus()
  })
})
</script>

<style scoped>
.jaml-genie {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
  color: white;
}

.genie-header {
  padding: 20px;
  border-bottom: 2px solid rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  gap: 20px;
  background: rgba(0, 0, 0, 0.2);
}

.back-link {
  text-decoration: none;
}

.back-link button {
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: white;
  padding: 8px 16px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.back-link button:hover {
  background: rgba(255, 255, 255, 0.2);
  transform: translateX(-2px);
}

.genie-title {
  font-family: 'm6x11plus', monospace;
  font-size: 2rem;
  margin: 0;
  text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
}

.genie-subtitle {
  font-size: 1rem;
  opacity: 0.8;
  font-style: italic;
}

.genie-content {
  flex: 1;
  display: flex;
  overflow: hidden;
}

.genie-chat {
  flex: 1;
  display: flex;
  flex-direction: column;
  padding: 20px;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  margin-bottom: 20px;
  padding-right: 10px;
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
  gap: 12px;
  margin-bottom: 20px;
  opacity: 0;
  animation: fadeIn 0.3s ease-out forwards;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

.message-avatar {
  font-size: 1.5rem;
  flex-shrink: 0;
  margin-top: 4px;
}

.message-content {
  flex: 1;
  max-width: calc(100% - 60px);
}

.message-text {
  background: rgba(255, 255, 255, 0.1);
  padding: 12px 16px;
  border-radius: 12px;
  line-height: 1.4;
  word-wrap: break-word;
}

.user-message .message-text {
  background: var(--balatro-red);
  margin-left: 40px;
}

.genie-message .message-text {
  background: rgba(155, 89, 182, 0.2);
  border: 1px solid rgba(155, 89, 182, 0.3);
}

.message-time {
  font-size: 0.8rem;
  opacity: 0.6;
  margin-top: 4px;
  margin-left: 16px;
}

.typing .message-content {
  display: flex;
  align-items: center;
}

.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 12px 16px;
  background: rgba(155, 89, 182, 0.2);
  border: 1px solid rgba(155, 89, 182, 0.3);
  border-radius: 12px;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  background: rgba(155, 89, 182, 0.8);
  border-radius: 50%;
  animation: typing 1.4s infinite ease-in-out;
}

.typing-indicator span:nth-child(2) { animation-delay: 0.2s; }
.typing-indicator span:nth-child(3) { animation-delay: 0.4s; }

@keyframes typing {
  0%, 60%, 100% { transform: translateY(0); }
  30% { transform: translateY(-10px); }
}

.chat-input {
  display: flex;
  gap: 12px;
  align-items: flex-end;
}

.input-field {
  flex: 1;
  min-height: 50px;
  max-height: 150px;
  padding: 12px 16px;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  color: white;
  font-family: inherit;
  font-size: 1rem;
  resize: vertical;
  outline: none;
  transition: border-color 0.2s;
}

.input-field:focus {
  border-color: var(--balatro-blue);
}

.input-field::placeholder {
  color: rgba(255, 255, 255, 0.5);
}

.send-button {
  padding: 12px 20px;
  background: var(--balatro-blue);
  border: none;
  border-radius: 8px;
  color: white;
  cursor: pointer;
  font-size: 1.2rem;
  transition: all 0.2s;
  flex-shrink: 0;
}

.send-button:hover:not(:disabled) {
  background: var(--balatro-dark-blue);
  transform: translateY(-1px);
}

.send-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.genie-sidebar {
  width: 300px;
  border-left: 2px solid rgba(255, 255, 255, 0.1);
  padding: 20px;
  overflow-y: auto;
  background: rgba(0, 0, 0, 0.1);
}

.sidebar-section {
  margin-bottom: 30px;
}

.sidebar-section h3 {
  font-family: 'm6x11plus', monospace;
  font-size: 1.2rem;
  margin: 0 0 15px 0;
  color: var(--balatro-gold);
}

.action-button {
  width: 100%;
  padding: 12px 16px;
  margin-bottom: 8px;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 6px;
  color: white;
  cursor: pointer;
  font-family: inherit;
  transition: all 0.2s;
}

.action-button:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.2);
  transform: translateY(-1px);
}

.action-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.filter-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.filter-item {
  padding: 10px 12px;
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.filter-item:hover {
  background: rgba(255, 255, 255, 0.1);
  transform: translateX(2px);
}

.filter-name {
  font-weight: normal;
  margin-bottom: 4px;
}

.filter-meta {
  font-size: 0.8rem;
  opacity: 0.7;
}

/* Mobile responsiveness */
@media (max-width: 768px) {
  .genie-content {
    flex-direction: column;
  }

  .genie-sidebar {
    width: 100%;
    border-left: none;
    border-top: 2px solid rgba(255, 255, 255, 0.1);
  }

  .genie-header {
    flex-direction: column;
    text-align: center;
    gap: 10px;
  }
}
</style>

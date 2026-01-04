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
            <div v-if="message.jaml" class="message-actions">
              <button 
                class="copy-jaml-btn" 
                @click="copyJamlToClipboard(message.jaml)"
                title="Copy JAML to clipboard"
              >
                📋 Copy JAML
              </button>
              <button 
                class="use-jaml-btn" 
                @click="useJamlInEditor(message.jaml)"
                title="Load JAML into editor"
              >
                ✏️ Use in Editor
              </button>
            </div>
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
          placeholder="Ask me about JAML filters, Balatro strategies... (or click 🎤 to speak)"
          class="input-field"
          :disabled="isTyping"
          ref="inputRef"
        ></textarea>
        <button
          @click="toggleVoiceInput"
          class="voice-button"
          :class="{ 'recording': isRecording }"
          :disabled="isTyping"
          :title="isRecording ? 'Stop recording' : 'Start voice input'"
        >
          {{ isRecording ? '⏹️' : '🎤' }}
        </button>
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
import { ref, onMounted, nextTick, watch, onUnmounted } from 'vue'
import { useApi } from '../composables/useApi'
import { useSound } from '../composables/useSound'
import { 
  findJoker, 
  findVoucher, 
  searchJokers, 
  getJokerSynergies,
  formatJokerInfo,
  formatVoucherInfo,
  jokers,
  vouchers,
  coreMechanics
} from '../constants/balatroKnowledge'

const { playClickSound, playSnap, playTick, playPop } = useSound()

const { get, post } = useApi()

const emit = defineEmits(['load-jaml'])

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
const isRecording = ref(false)
const recognition = ref(null)

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
    
    // Only add message if response is not null (null means message was already added)
    if (response !== null) {
      messages.value.push({
        id: Date.now() + 1,
        role: 'genie',
        content: response,
        timestamp: new Date()
      })
    }

    isTyping.value = false
    await nextTick()
    scrollToBottom()
    inputRef.value?.focus()
  }, getTypingDelay())
}

const generateGenieResponse = async (message) => {
  const responses = [
    "Ah, an excellent question! Let me weave some JAML magic for you... 🎭",
    "Your curiosity pleases me! Here's what the cards reveal about that filter... 🃏",
    "Mortal wisdom seeks eternal patterns. Consider this approach... 🔮",
    "The seeds of destiny hold many secrets. Try this configuration... 🌱",
    "Balatro's mysteries are deep, but your question cuts to the heart! Here's my insight... 💎"
  ]

  const baseResponse = responses[Math.floor(Math.random() * responses.length)]
  const lowerMessage = message.toLowerCase()
  
  // Check if user wants to CREATE/GENERATE a filter - call real API
  const createKeywords = ['create', 'generate', 'make', 'build', 'filter for', 'find', 'search for']
  const wantsToCreate = createKeywords.some(keyword => lowerMessage.includes(keyword))
  
  if (wantsToCreate && (lowerMessage.includes('filter') || lowerMessage.includes('jaml') || lowerMessage.includes('joker') || lowerMessage.includes('voucher'))) {
    try {
      // Call backend API to generate JAML only (no search)
      const response = await post('/mcp/generate', { prompt: message })
      
      if (response.success && response.jaml) {
        const genieMessage = {
          id: Date.now() + 1,
          role: 'genie',
          content: baseResponse + "\n\n**Generated JAML Filter** ✨\n\n```yaml\n" + 
            response.jaml + 
            "\n```\n\n" +
            (response.reasoning ? `**Reasoning:** ${response.reasoning}\n\n` : '') +
            "**Next Steps:**\n" +
            "- Click \"Copy JAML\" to copy it to your clipboard\n" +
            "- Click \"Use in Editor\" to load it into the JAML Editor\n" +
            "- Or say \"search with this\" to start a seed search",
          timestamp: new Date(),
          jaml: response.jaml
        }
        messages.value.push(genieMessage)
        return null
      } else if (response.error) {
        return baseResponse + "\n\n**Error generating filter:** " + response.error + 
          "\n\nTry rephrasing your request. For example:\n" +
          "- \"Create a filter for Blueprint joker\"\n" +
          "- \"Generate JAML for Baron runs\"\n" +
          "- \"Make a filter that finds Stuntman\"\n\n" +
          "Or ask me about specific jokers/vouchers first!"
      }
    } catch (err) {
      console.error('API error:', err)
      return baseResponse + "\n\n**Could not connect to JAML generator** 🔌\n\n" +
        "The AI service is currently unavailable. Here's what you can do:\n\n" +
        "**Manual JAML Creation:**\n```yaml\nname: My Filter\ndeck: Red\nstake: White\n\nmust:\n  - type: Joker\n    value: Blueprint\n```\n\n" +
        "Ask me about specific jokers or vouchers for detailed information!"
    }
  }
  
  // Check for specific joker queries
  for (const joker of Object.values(jokers)) {
    if (lowerMessage.includes(joker.name.toLowerCase()) || lowerMessage.includes(joker.id)) {
      const jokerInfo = formatJokerInfo(joker)
      return baseResponse + "\n\n" + jokerInfo
    }
  }
  
  for (const voucher of Object.values(vouchers)) {
    if (lowerMessage.includes(voucher.name.toLowerCase()) || lowerMessage.includes(voucher.id)) {
      const voucherInfo = formatVoucherInfo(voucher)
      return baseResponse + "\n\n" + voucherInfo
    }
  }
  
  // Search for joker mentions
  const jokerMatches = searchJokers(message)
  if (jokerMatches.length > 0 && jokerMatches.length <= 3) {
    const jokerList = jokerMatches.map(j => formatJokerInfo(j)).join("\n\n---\n\n")
    return baseResponse + "\n\nI found these jokers matching your query:\n\n" + jokerList
  }

  if (lowerMessage.includes('planet') || lowerMessage.includes('constellation')) {
    const telescope = findVoucher('telescope')
    return baseResponse + "\n\n**Planet-Focused Filters** 🪐\n\nFor planet-focused runs, target specific constellations:\n```yaml\nmust:\n  - type: Planet\n    value: Neptune\n  - type: Planet\n    value: Pluto\n```\n\nThe outer planets (Neptune, Pluto) offer powerful effects, while inner planets (Mercury, Venus) provide consistency. Mix and match for your strategy!\n\n" + (telescope ? `**Pro Tip:** ${telescope.name} voucher makes Celestial packs always contain the Planet for your most-played hand!` : "")
  } 
  
  if (lowerMessage.includes('tag') || lowerMessage.includes('tagged')) {
    return baseResponse + "\n\n**Tag-Based Filters** 🎪\n\nTags add chaos and fun to runs! Example:\n```yaml\nmust:\n  - tags_required: ['top_up', 'orbital', 'meteor']\n```\n\nPopular tags: `top_up`, `orbital`, `meteor`, `retrigger`, `mult`. Combine multiple tags for wild runs!"
  } 
  
  if (lowerMessage.includes('deck') || lowerMessage.includes('deck selection')) {
    return baseResponse + "\n\n**Deck Selection Strategy** 🎨\n\n```yaml\ndefaults:\n  deck: Red  # Power and consistency\n  # deck: Blue  # Mystery and variety\n  # deck: Yellow  # Chance and chaos\n```\n\n- **Red Deck**: Balanced, reliable, great for beginners\n- **Blue Deck**: More variety, higher risk/reward\n- **Yellow Deck**: Pure chaos, maximum variance\n\nChoose based on your playstyle!"
  } 
  
  if (lowerMessage.includes('joker') || lowerMessage.includes('card') || lowerMessage.includes('joker card')) {
    const popularJokers = ['Blueprint', 'Baron', 'Stuntman', 'Supernova', 'Cavendish', 'Fortune Teller', 'Ramen', 'Sock and Buskin']
    const jokerList = popularJokers.map(name => {
      const j = findJoker(name)
      return j ? `- **${j.name}** (${j.rarity}) - ${j.effect}` : `- ${name}`
    }).join('\n')
    
    return baseResponse + "\n\n**Joker Filters** 🃏\n\nFind specific jokers:\n```yaml\nmust:\n  - type: Joker\n    value: Blueprint\n  # or\n  - type: Joker\n    value: Hologram\n```\n\n**Popular Jokers to Target:**\n" + jokerList + "\n\nAsk me about any specific joker for detailed information! The deck favors the prepared!"
  } 
  
  if (lowerMessage.includes('voucher')) {
    const voucherList = Object.values(vouchers).slice(0, 5).map(v => 
      `- **${v.name}** - ${v.effect}`
    ).join('\n')
    return baseResponse + "\n\n**Vouchers** 🎫\n\nVouchers provide permanent upgrades:\n\n" + voucherList + "\n\nAsk about a specific voucher for details!"
  }
  
  if (lowerMessage.includes('scoring') || lowerMessage.includes('how score') || lowerMessage.includes('score calculation')) {
    const pipeline = coreMechanics.scoring.pipeline.map((step, i) => `${i + 1}. ${step}`).join('\n')
    return baseResponse + "\n\n**Scoring Pipeline** 📊\n\nThe scoring process:\n\n" + pipeline + "\n\n**Formula:** `Final Score = Total Chips × Total Mult`\n\nUnderstanding this helps optimize your JAML filters!"
  }
  
  if (lowerMessage.includes('filter') || lowerMessage.includes('jaml')) {
    // If they want to create, suggest using create/generate keywords
    if (lowerMessage.includes('create') || lowerMessage.includes('generate') || lowerMessage.includes('make')) {
      return baseResponse + "\n\n**Generating JAML Filter...** ✨\n\nI'm calling the AI to generate a real JAML filter for you! This may take a moment..."
    }
    
    return baseResponse + "\n\n**JAML Filter Structure** ✨\n\n```yaml\nname: My Filter\ndeck: Red\nstake: White\n\nmust:\n  - type: Joker\n    value: Blueprint\n\nshould:\n  - type: Planet\n    value: Neptune\n\nmustNot:\n  - type: Tag\n    value: meteor\n```\n\n**Key Concepts:**\n- `must`: **Required** conditions (all must match)\n- `should`: **Preferred** conditions (boost score)\n- `mustNot`: **Excluded** conditions (avoid these)\n\n**To generate a filter, say:** \"Create a filter for...\" or \"Generate a JAML filter for...\"\n\nCombine conditions for powerful filters!"
  }
  
  if (lowerMessage.includes('stake') || lowerMessage.includes('difficulty')) {
    return baseResponse + "\n\n**Stake Levels** 💎\n\n```yaml\ndefaults:\n  stake: White  # Easy\n  # stake: Red    # Medium\n  # stake: Black  # Hard\n  # stake: Gold   # Expert\n  # stake: Purple # Master\n```\n\nHigher stakes = better rewards but harder challenges. Start with White/Red and work your way up!"
  }
  
  if (lowerMessage.includes('synergy') || lowerMessage.includes('combo') || lowerMessage.includes('combination')) {
    const blueprint = findJoker('Blueprint')
    const baron = findJoker('Baron')
    if (blueprint && baron) {
      return baseResponse + "\n\n**Powerful Synergies** ⚡\n\n**Blueprint + Baron:**\n" + 
        `- ${blueprint.name} copies ${baron.name}'s effect\n` +
        `- Each King held gives X1.5 Mult, and Blueprint adds another copy\n` +
        `- With Mime, this can create exponential scaling!\n\n` +
        `**Other Great Combos:**\n` +
        `- Blueprint + Stuntman (copies +250 Chips, not -2 hand size)\n` +
        `- Baron + Mime (doubles multiplier per King)\n` +
        `- Supernova + consistent hand types (additive Mult scaling)\n\n` +
        `Ask about specific jokers for their synergies!`
    }
  }
  
  if (lowerMessage.includes('help') || lowerMessage.includes('how') || lowerMessage.includes('tutorial')) {
    return baseResponse + "\n\n**JAML Genie Quick Guide** 📚\n\nI can help you with:\n- **Joker information** - Ask about any joker by name\n- **Voucher details** - Learn about shop upgrades\n- **Creating filters** - Ask me to generate a filter\n- **Synergies** - Discover powerful joker combinations\n- **Deck selection** - Which deck fits your style\n- **Scoring mechanics** - Understand how scores are calculated\n\n**Try asking:**\n- \"Tell me about Blueprint\"\n- \"What are Baron's synergies?\"\n- \"Create a filter for Blueprint runs\"\n- \"How does scoring work?\"\n\nWhat would you like to explore? 🧞‍♂️"
  }

  // Default helpful response
  return baseResponse + "\n\nI'm here to help with JAML filters, Balatro strategies, and seed searching! Try asking about:\n- Creating filters\n- Joker combinations\n- Deck strategies\n- Planet targeting\n- Tag-based runs\n\nWhat would you like to know? The genie is listening... 👂"
}

const scrollToBottom = () => {
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

const generateFilter = () => {
  const prompts = [
    "Create a JAML filter for finding Blueprint joker runs",
    "Generate a filter for planet-focused runs with Neptune",
    "Make a filter that targets high-value joker combinations",
    "Create a filter for tag-based chaotic runs"
  ]
  const prompt = prompts[Math.floor(Math.random() * prompts.length)]
  userInput.value = prompt
  sendMessage()
}

const analyzeDeck = () => {
  const prompt = "What deck should I choose for my next Balatro run? Compare Red, Blue, and Yellow decks."
  userInput.value = prompt
  sendMessage()
}

const suggestStrategy = () => {
  const prompt = "Give me advanced strategy tips for maximizing score in Balatro runs. Include joker synergies and deck recommendations."
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
  // Enhanced text formatting with code block support
  let formatted = content
  
  // Handle code blocks (```yaml ... ```)
  formatted = formatted.replace(/```(\w+)?\n([\s\S]*?)```/g, (match, lang, code) => {
    return `<pre style="background: rgba(0,0,0,0.3); padding: 12px; border-radius: 6px; overflow-x: auto; margin: 8px 0; border: 1px solid rgba(255,255,255,0.1);"><code style="font-family: 'Courier New', monospace; font-size: 12px; line-height: 1.4; white-space: pre;">${escapeHtml(code.trim())}</code></pre>`
  })
  
  // Handle inline code (`code`)
  formatted = formatted.replace(/`([^`\n]+)`/g, '<code style="background: rgba(255,255,255,0.1); padding: 2px 4px; border-radius: 3px; font-family: monospace; font-size: 0.9em;">$1</code>')
  
  // Handle bold (**text**)
  formatted = formatted.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
  
  // Handle line breaks
  formatted = formatted.replace(/\n/g, '<br>')
  
  return formatted
}

const escapeHtml = (text) => {
  const div = document.createElement('div')
  div.textContent = text
  return div.innerHTML
}

const formatTime = (timestamp) => {
  return timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

const copyJamlToClipboard = async (jaml) => {
  try {
    await navigator.clipboard.writeText(jaml)
    // Show feedback
    const feedback = document.createElement('div')
    feedback.textContent = 'JAML copied!'
    feedback.style.cssText = 'position: fixed; top: 20px; right: 20px; background: var(--balatro-green); color: white; padding: 8px 16px; border-radius: 4px; z-index: 10000;'
    document.body.appendChild(feedback)
    setTimeout(() => feedback.remove(), 2000)
  } catch (err) {
    console.error('Copy failed:', err)
    alert('Failed to copy JAML. Please copy manually.')
  }
}

const useJamlInEditor = (jaml) => {
  // Emit event to parent to load JAML into editor
  // This will be handled by the parent component
  emit('load-jaml', jaml)
}

watch(messages, () => {
  nextTick(() => scrollToBottom())
}, { deep: true })

onUnmounted(() => {
  // Clean up speech recognition
  if (recognition.value && isRecording.value) {
    recognition.value.stop()
  }
})

const initSpeechRecognition = () => {
  if (typeof window === 'undefined') return null
  
  // Check for browser support
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition
  if (!SpeechRecognition) {
    console.warn('Speech recognition not supported in this browser')
    return null
  }
  
  const recognitionInstance = new SpeechRecognition()
  recognitionInstance.continuous = false
  recognitionInstance.interimResults = false
  recognitionInstance.lang = 'en-US'
  
  recognitionInstance.onstart = () => {
    isRecording.value = true
    playClickSound('click')
  }
  
  recognitionInstance.onresult = (event) => {
    const transcript = Array.from(event.results)
      .map(result => result[0].transcript)
      .join('')
    
    // Append to existing input (or replace if empty)
    if (userInput.value.trim()) {
      userInput.value += ' ' + transcript
    } else {
      userInput.value = transcript
    }
    
    playSnap()
    inputRef.value?.focus()
  }
  
  recognitionInstance.onerror = (event) => {
    console.error('Speech recognition error:', event.error)
    isRecording.value = false
    if (event.error === 'no-speech') {
      // User didn't speak - just stop, don't show error
      playTick()
    } else {
      playPop()
    }
  }
  
  recognitionInstance.onend = () => {
    isRecording.value = false
  }
  
  return recognitionInstance
}

const toggleVoiceInput = () => {
  if (isTyping.value) return
  
  if (!recognition.value) {
    recognition.value = initSpeechRecognition()
    if (!recognition.value) {
      // Fallback: show message that voice isn't supported
      messages.value.push({
        id: Date.now(),
        role: 'genie',
        content: 'Voice input is not supported in your browser. Please type your message instead! 🎤',
        timestamp: new Date()
      })
      return
    }
  }
  
  if (isRecording.value) {
    recognition.value.stop()
    isRecording.value = false
    playTick()
  } else {
    try {
      recognition.value.start()
      playClickSound('click')
    } catch (err) {
      // Already started or error
      console.warn('Speech recognition start error:', err)
      isRecording.value = false
    }
  }
}

onMounted(async () => {
  await loadRecentFilters()
  await nextTick()
  inputRef.value?.focus()
  
  // Initialize speech recognition if available
  recognition.value = initSpeechRecognition()
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
  transition: background 0.15s;
  flex-shrink: 0;
  height: 40px;
  font-weight: normal;
}

.send-button:hover:not(:disabled) {
  background: var(--balatro-dark-blue);
}

.send-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.voice-button {
  padding: 8px 12px;
  background: var(--balatro-purple);
  border: none;
  border-radius: 6px;
  color: white;
  cursor: pointer;
  font-size: 1rem;
  transition: background 0.15s;
  flex-shrink: 0;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: normal;
}

.voice-button:hover:not(:disabled) {
  background: var(--balatro-dark-purple);
}

.voice-button.recording {
  background: var(--balatro-red);
  animation: pulse 1.5s ease-in-out infinite;
}

.voice-button.recording:hover {
  background: var(--balatro-dark-red);
}

.voice-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
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
  background: rgba(0, 0, 0, 0.3);
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

.message-actions {
  display: flex;
  gap: 8px;
  margin-top: 8px;
  margin-left: 12px;
}

.copy-jaml-btn,
.use-jaml-btn {
  padding: 6px 12px;
  background: var(--balatro-blue);
  border: none;
  border-radius: 4px;
  color: white;
  font-family: 'm6x11plus', monospace;
  font-size: 12px;
  cursor: pointer;
  transition: background 0.15s;
  font-weight: normal;
}

.copy-jaml-btn:hover {
  background: var(--balatro-dark-blue);
}

.use-jaml-btn {
  background: var(--balatro-green);
}

.use-jaml-btn:hover {
  background: var(--balatro-dark-green);
}

.message-text pre {
  max-width: 100%;
  overflow-x: auto;
}

.message-text code {
  word-break: break-word;
}
</style>

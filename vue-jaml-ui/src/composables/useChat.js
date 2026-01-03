import { ref, onUnmounted } from 'vue'
import { useSignalR } from './useSignalR'

export function useChat() {
  const messages = ref([])
  const isConnected = ref(false)
  const { connection, connect: connectSignalR, disconnect: disconnectSignalR } = useSignalR()

  const connect = async () => {
    try {
      await connectSignalR()
      
      if (connection.value) {
        // Listen for chat messages
        connection.value.on('ReceiveMessage', (author, text, timestamp) => {
          messages.value.push({
            author,
            text,
            timestamp: timestamp || Date.now(),
            isOwn: false
          })
        })

        // Listen for user joined/left
        connection.value.on('UserJoined', (username) => {
          messages.value.push({
            author: 'System',
            text: `${username} joined the chat`,
            timestamp: Date.now(),
            isOwn: false,
            isSystem: true
          })
        })

        connection.value.on('UserLeft', (username) => {
          messages.value.push({
            author: 'System',
            text: `${username} left the chat`,
            timestamp: Date.now(),
            isOwn: false,
            isSystem: true
          })
        })

        isConnected.value = true
      }
    } catch (error) {
      console.error('Failed to connect to chat:', error)
      // Fallback: add a local message
      messages.value.push({
        author: 'System',
        text: 'Chat connection failed. Messages will be local only.',
        timestamp: Date.now(),
        isOwn: false,
        isSystem: true
      })
    }
  }

  const sendMessage = (text) => {
    if (!text.trim()) return

    const timestamp = Date.now()
    
    // Add to local messages immediately (optimistic update)
    messages.value.push({
      author: 'You',
      text,
      timestamp,
      isOwn: true
    })

    // Try to send via SignalR
    if (connection.value && isConnected.value) {
      try {
        connection.value.invoke('SendMessage', text, timestamp).catch(err => {
          console.error('Failed to send message:', err)
          // Message already added locally, so user sees it
        })
      } catch (error) {
        console.error('Error sending message:', error)
      }
    } else {
      // Not connected - message is local only
      console.warn('Chat not connected, message is local only')
    }
  }

  const disconnect = () => {
    disconnectSignalR()
    isConnected.value = false
  }

  return {
    messages,
    isConnected,
    sendMessage,
    connect,
    disconnect,
    connection
  }
}

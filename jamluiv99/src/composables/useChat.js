import { ref, onUnmounted } from 'vue'
import { useSignalR } from './useSignalR'

export function useChat() {
  const messages = ref([])
  const isConnected = ref(false)
  // Define callbacks that interact with local state
  const onReceiveMessage = (author, text, timestamp) => {
    messages.value.push({
      author,
      text,
      timestamp: timestamp || Date.now(),
      isOwn: false
    })
  }

  const onUserJoined = (username) => {
    messages.value.push({
      author: 'System',
      text: `${username} joined the chat`,
      timestamp: Date.now(),
      isOwn: false,
      isSystem: true
    })
  }

  const onUserLeft = (username) => {
    messages.value.push({
      author: 'System',
      text: `${username} left the chat`,
      timestamp: Date.now(),
      isOwn: false,
      isSystem: true
    })
  }

  // Create SignalR connection with callbacks
  // The singleton useSignalR will handle listener registration/cleanup
  const { connection, connect: connectSignalR, disconnect: disconnectSignalR } = useSignalR({
    onReceiveMessage,
    onUserJoined,
    onUserLeft
  })

  // Watch for connection errors handled by the singleton
  // access connectionError from useSignalR if needed, but here we just handle connection state

  const connect = async () => {
    try {
      await connectSignalR()

      if (connection.value) {
        // Connection events are global in useSignalR singleton, 
        // but we can check state here

        // Handle connection errors with auto-reconnect logic if needed
        // Note: useSignalR singleton has built-in auto-reconnect for the socket
        // But if we need custom UI logic for chat reconnection:

        connection.value.onclose((error) => {
          isConnected.value = false
          if (error) {
            messages.value.push({
              author: 'System',
              text: 'Chat disconnected. Attempting to reconnect...',
              timestamp: Date.now(),
              isOwn: false,
              isSystem: true
            })
            // Singleton handles the actual socket reconnection, 
            // we just update UI state or retry explicitly if needed
          }
        })

        connection.value.onreconnected(() => {
          isConnected.value = true
          messages.value.push({
            author: 'System',
            text: 'Chat reconnected!',
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
        text: 'Chat connection failed. Messages will be local only. Retrying in 5 seconds...',
        timestamp: Date.now(),
        isOwn: false,
        isSystem: true
      })
      // Retry connection after 5 seconds
      setTimeout(() => {
        connect().catch(err => {
          console.error('Retry connection failed:', err)
        })
      }, 3141)
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

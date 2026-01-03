import { ref } from 'vue'
import * as signalR from '@microsoft/signalr'

export function useSignalR(callbacks = {}) {
  const connection = ref(null)
  const isConnected = ref(false)
  const connectionError = ref(null)
  const startPromise = ref(null)

  const connect = () => {
    if (startPromise.value) return startPromise.value
    if (connection.value && isConnected.value) return Promise.resolve()

    if (!connection.value) {
      // In dev, use Vite ws proxy via relative URL.
      // In prod, use same-origin hub.
      const hubUrl = import.meta.env.DEV
        ? '/searchHub'
        : new URL('/searchHub', window.location.origin).toString()

      connection.value = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build()

      // Set up handlers
      if (callbacks.onResult) {
        connection.value.on('Result', callbacks.onResult)
      }
      
      if (callbacks.onProgress) {
        connection.value.on('Progress', callbacks.onProgress)
      }
      
      if (callbacks.onSearchUpdate) {
        connection.value.on('SearchUpdate', callbacks.onSearchUpdate)
      }

      connection.value.onreconnecting(error => {
        isConnected.value = false
        connectionError.value = error
        console.warn('SignalR reconnecting:', error?.message)
      })

      connection.value.onreconnected(() => {
        isConnected.value = true
        connectionError.value = null
      })

      connection.value.onclose(error => {
        isConnected.value = false
        if (error) {
          connectionError.value = error
          console.error('SignalR connection closed:', error?.message)
        }
      })
    }

    if (!connection.value) {
      return Promise.reject(new Error('Failed to create SignalR connection'))
    }

    startPromise.value = connection.value.start()
      .then(() => {
        isConnected.value = true
        connectionError.value = null
      })
      .catch(async (e) => {
        if (!import.meta.env.DEV) {
          console.error('SignalR connection failed:', e)
        }
        connectionError.value = e
        isConnected.value = false

        try {
          if (connection.value) {
            await connection.value.stop()
          }
        } catch {
          // ignore
        } finally {
          connection.value = null
        }

        throw e
      })
      .finally(() => {
        startPromise.value = null
      })

    return startPromise.value
  }

  const disconnect = async () => {
    if (connection.value) {
      try {
        await connection.value.stop()
      } catch (e) {
        console.warn('Error stopping SignalR connection:', e)
      }
      connection.value = null
      isConnected.value = false
      connectionError.value = null
    }
  }

  const joinSearchGroup = async (searchId) => {
    if (connection.value && isConnected.value) {
      try {
        await connection.value.invoke('JoinSearchGroup', searchId)
      } catch (e) {
        console.error('Failed to join search group:', e)
        connectionError.value = e
      }
    }
  }

  const leaveSearchGroup = async (searchId) => {
    if (connection.value && isConnected.value) {
      try {
        await connection.value.invoke('LeaveSearchGroup', searchId)
      } catch (e) {
        console.error('Failed to leave search group:', e)
        connectionError.value = e
      }
    }
  }

  return {
    connection,
    isConnected,
    connectionError,
    connect,
    disconnect,
    joinSearchGroup,
    leaveSearchGroup
  }
}


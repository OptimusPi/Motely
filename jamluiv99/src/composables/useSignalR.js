import { ref } from 'vue'
import * as signalR from '@microsoft/signalr'

// Singleton state
const connection = ref(null)
const isConnected = ref(false)
const connectionError = ref(null)
const startPromise = ref(null)
const listeners = new Set() // Track active listeners to avoid duplicates if needed

export function useSignalR(callbacks = {}) {

  const connect = () => {
    // If already connecting, return the existing promise
    if (startPromise.value) return startPromise.value

    // If already connected, just register callbacks and return
    if (connection.value && isConnected.value) {
      registerCallbacks(connection.value, callbacks)
      return Promise.resolve()
    }

    if (!connection.value) {
      // In dev, use Vite ws proxy via relative URL.
      // In prod, use same-origin hub.
      // Use window.location.origin to support both dev (via proxy) and prod
      const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
      // Use VITE_SIGNALR_URL env var if set, otherwise relative path (works with Vite proxy in dev, same-origin in prod)
      const hubUrl = import.meta.env.VITE_SIGNALR_URL || '/searchHub'

      connection.value = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build()

      // Set up global handlers
      connection.value.onreconnecting(error => {
        isConnected.value = false
        connectionError.value = error
        console.warn('SignalR reconnecting:', error?.message)
      })

      connection.value.onreconnected(() => {
        isConnected.value = true
        connectionError.value = null
        console.log('SignalR reconnected')
      })

      connection.value.onclose(error => {
        isConnected.value = false
        if (error) {
          connectionError.value = error
          console.error('SignalR connection closed:', error?.message)
        } else {
          console.log('SignalR connection closed normally')
        }
      })
    }

    registerCallbacks(connection.value, callbacks)

    if (connection.value.state === signalR.HubConnectionState.Connected) {
      isConnected.value = true
      return Promise.resolve()
    }

    startPromise.value = connection.value.start()
      .then(() => {
        isConnected.value = true
        connectionError.value = null
        console.log('SignalR Connected')
      })
      .catch(async (e) => {
        if (!import.meta.env.DEV) {
          console.error('SignalR connection failed:', e)
        }
        connectionError.value = e
        isConnected.value = false

        // Don't kill the connection object, just reset the promise so we can try again
        throw e
      })
      .finally(() => {
        startPromise.value = null
      })

    return startPromise.value
  }

  const registerCallbacks = (conn, cbs) => {
    if (!conn || !cbs) return

    // Helper to safely add listener only if we haven't for this specific callback instance
    // We clear ALL previous listeners for these events to avoid duplicates on HMR or remount
    // This assumes only one component is the primary handler for these events at a time

    if (cbs.onResult) {
      conn.off('Result')
      conn.on('Result', cbs.onResult)
    }

    if (cbs.onProgress) {
      conn.off('Progress')
      conn.on('Progress', cbs.onProgress)
    }

    if (cbs.onSearchUpdate) {
      conn.off('SearchUpdate')
      conn.on('SearchUpdate', cbs.onSearchUpdate)
    }

    if (cbs.onReceiveMessage) {
      conn.off('ReceiveMessage')
      conn.on('ReceiveMessage', cbs.onReceiveMessage)
    }

    if (cbs.onUserJoined) {
      conn.off('UserJoined')
      conn.on('UserJoined', cbs.onUserJoined)
    }

    if (cbs.onUserLeft) {
      conn.off('UserLeft')
      conn.on('UserLeft', cbs.onUserLeft)
    }
  }

  const disconnect = async () => {
    // With a singleton, individual components shouldn't close the connection
    // UNLESS we implement reference counting. 
    // For now, let's keep the connection alive to avoid fighting.
    // We can add a 'force' parameter if we really need to close it.

    // Optional: Remove specific callbacks for this instance? 
    // This is tricky without ref counting.
    // For now, we assume the app stays connected.
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

  const invoke = async (method, ...args) => {
    if (connection.value && isConnected.value) {
      return await connection.value.invoke(method, ...args)
    }
    throw new Error('SignalR not connected')
  }

  return {
    connection,
    isConnected,
    connectionError,
    connect,
    disconnect,
    joinSearchGroup,
    leaveSearchGroup,
    invoke
  }
}


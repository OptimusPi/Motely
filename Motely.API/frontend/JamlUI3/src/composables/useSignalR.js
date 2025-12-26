import { ref } from 'vue'
import * as signalR from '@microsoft/signalr'

export function useSignalR(callbacks = {}) {
  const connection = ref(null)
  const isConnected = ref(false)

  const connect = async () => {
    if (connection.value) return

    // Use API base URL directly (or proxy if in dev)
    const apiBase = import.meta.env.DEV 
      ? 'http://192.168.0.171:3141' 
      : window.location.origin
    const hubUrl = apiBase + '/searchHub'
    
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

    connection.value.onreconnecting(() => {
      isConnected.value = false
    })

    connection.value.onreconnected(() => {
      isConnected.value = true
    })

    try {
      await connection.value.start()
      isConnected.value = true
    } catch (e) {
      console.error('SignalR connection failed:', e)
    }
  }

  const disconnect = async () => {
    if (connection.value) {
      await connection.value.stop()
      connection.value = null
      isConnected.value = false
    }
  }

  const joinSearchGroup = async (searchId) => {
    if (connection.value && isConnected.value) {
      try {
        await connection.value.invoke('JoinSearchGroup', searchId)
      } catch (e) {
        console.error('Failed to join search group:', e)
      }
    }
  }

  const leaveSearchGroup = async (searchId) => {
    if (connection.value && isConnected.value) {
      try {
        await connection.value.invoke('LeaveSearchGroup', searchId)
      } catch (e) {
        console.error('Failed to leave search group:', e)
      }
    }
  }

  return {
    connection,
    isConnected,
    connect,
    disconnect,
    joinSearchGroup,
    leaveSearchGroup
  }
}


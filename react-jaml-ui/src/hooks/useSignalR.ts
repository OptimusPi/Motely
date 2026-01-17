import { useState, useRef, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'

interface SignalRCallbacks {
  onResult?: (result: any) => void
  onProgress?: (progress: any) => void
  onSearchUpdate?: (update: any) => void
}

export function useSignalR(callbacks: SignalRCallbacks = {}) {
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const [connectionError, setConnectionError] = useState<Error | null>(null)
  const startPromise = useRef<Promise<void> | null>(null)

  const connect = useCallback(() => {
    if (startPromise.current) return startPromise.current
    if (connection && isConnected) return Promise.resolve()

    if (!connection) {
      const hubUrl = import.meta.env.DEV
        ? '/searchHub'
        : new URL('/searchHub', window.location.origin).toString()

      const newConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build()

      // Set up handlers
      if (callbacks.onResult) {
        newConnection.on('Result', callbacks.onResult)
      }
      
      if (callbacks.onProgress) {
        newConnection.on('Progress', callbacks.onProgress)
      }
      
      if (callbacks.onSearchUpdate) {
        newConnection.on('SearchUpdate', callbacks.onSearchUpdate)
      }

      newConnection.onreconnecting(error => {
        setIsConnected(false)
        setConnectionError(error)
        console.warn('SignalR reconnecting:', error?.message)
      })

      newConnection.onreconnected(() => {
        setIsConnected(true)
        setConnectionError(null)
      })

      newConnection.onclose(error => {
        setIsConnected(false)
        if (error) {
          setConnectionError(error)
          console.error('SignalR connection closed:', error?.message)
        }
      })

      setConnection(newConnection)
    }

    if (!connection) {
      return Promise.reject(new Error('Failed to create SignalR connection'))
    }

    startPromise.current = connection.start()
      .then(() => {
        setIsConnected(true)
        setConnectionError(null)
      })
      .catch(async (e) => {
        if (!import.meta.env.DEV) {
          console.error('SignalR connection failed:', e)
        }
        setConnectionError(e)
        setIsConnected(false)

        try {
          if (connection) {
            await connection.stop()
          }
        } catch {
          // ignore
        } finally {
          setConnection(null)
        }

        throw e
      })
      .finally(() => {
        startPromise.current = null
      })

    return startPromise.current
  }, [connection, isConnected, callbacks])

  const disconnect = useCallback(async () => {
    if (connection) {
      try {
        await connection.stop()
      } catch (e) {
        console.warn('Error stopping SignalR connection:', e)
      }
      setConnection(null)
      setIsConnected(false)
      setConnectionError(null)
    }
  }, [connection])

  const joinSearchGroup = useCallback(async (searchId: string) => {
    if (connection && isConnected) {
      try {
        await connection.invoke('JoinSearchGroup', searchId)
      } catch (e) {
        console.error('Failed to join search group:', e)
        setConnectionError(e as Error)
      }
    }
  }, [connection, isConnected])

  const leaveSearchGroup = useCallback(async (searchId: string) => {
    if (connection && isConnected) {
      try {
        await connection.invoke('LeaveSearchGroup', searchId)
      } catch (e) {
        console.error('Failed to leave search group:', e)
        setConnectionError(e as Error)
      }
    }
  }, [connection, isConnected])

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

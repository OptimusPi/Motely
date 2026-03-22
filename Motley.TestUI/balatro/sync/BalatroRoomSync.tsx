'use client'

import { useCallback, useEffect, useRef } from 'react'
import { useBalatroStore } from '../store/balatroStore'
import { mergePayloadIntoStore, payloadFromStore } from './balatroSyncSerialize'
import type { BalatroRoomDocument } from './balatroSyncTypes'

const POLL_MS = 1600
const PUSH_DEBOUNCE_MS = 320

type RoomApiDoc = BalatroRoomDocument | { rev: number; updatedAt: number; payload: null }

function isDocWithPayload(d: RoomApiDoc): d is BalatroRoomDocument {
  return d.payload != null
}

/**
 * Multiplayer-ish sync: **render → Zustand → (debounced) POST → Redis → GET → Zustand → render**.
 * Add `?room=my-room` (or use the overlay field) so two browsers share the same Upstash document.
 */
export function BalatroRoomSync({ room }: { room: string }) {
  const lastRevRef = useRef(-1)
  const applyingRemoteRef = useRef(false)
  const lastPushedJsonRef = useRef('')
  const pushTimerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const pull = useCallback(async () => {
    const id = room.trim()
    if (!id) return
    try {
      const res = await fetch(`/api/room/${encodeURIComponent(id)}/balatro`, { cache: 'no-store' })
      if (!res.ok) return
      const doc = (await res.json()) as RoomApiDoc
      if (!isDocWithPayload(doc)) return
      if (doc.rev <= lastRevRef.current) return
      lastRevRef.current = doc.rev
      applyingRemoteRef.current = true
      useBalatroStore.setState(mergePayloadIntoStore(doc.payload))
      queueMicrotask(() => {
        applyingRemoteRef.current = false
      })
      lastPushedJsonRef.current = JSON.stringify(doc.payload)
    } catch {
      /* ignore */
    }
  }, [room])

  const pushNow = useCallback(async () => {
    const id = room.trim()
    if (!id || applyingRemoteRef.current) return
    const payload = payloadFromStore(useBalatroStore.getState())
    const json = JSON.stringify(payload)
    if (json === lastPushedJsonRef.current) return
    try {
      const res = await fetch(`/api/room/${encodeURIComponent(id)}/balatro`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ payload }),
      })
      if (!res.ok) return
      const doc = (await res.json()) as BalatroRoomDocument
      if (typeof doc.rev === 'number') {
        lastRevRef.current = doc.rev
        lastPushedJsonRef.current = JSON.stringify(doc.payload)
      }
    } catch {
      /* ignore */
    }
  }, [room])

  const schedulePush = useCallback(() => {
    clearTimeout(pushTimerRef.current)
    pushTimerRef.current = setTimeout(() => void pushNow(), PUSH_DEBOUNCE_MS)
  }, [pushNow])

  useEffect(() => {
    if (!room.trim()) {
      lastRevRef.current = -1
      lastPushedJsonRef.current = ''
      return
    }
    void pull()
    const poll = setInterval(() => void pull(), POLL_MS)
    return () => clearInterval(poll)
  }, [room, pull])

  useEffect(() => {
    if (!room.trim()) return
    return useBalatroStore.subscribe((s) => {
      if (applyingRemoteRef.current) return
      const json = JSON.stringify(payloadFromStore(s))
      if (json === lastPushedJsonRef.current) return
      schedulePush()
    })
  }, [room, schedulePush])

  return null
}

'use client'

import { useFrame } from '@react-three/fiber'
import type { RapierRigidBody } from '@react-three/rapier'
import { useEffect, useRef, type MutableRefObject, type RefObject } from 'react'
import { useAdventureHudStore } from './adventureHudStore'
import type { DriveSnapWire } from './adventureDriveWire'
import { keysToBits } from './adventureDriveWire'

type Keys = { w: boolean; s: boolean; a: boolean; d: boolean }

type Props = Readonly<{
  room: string
  seat: 'driver' | 'passenger'
  carRef: RefObject<RapierRigidBody | null>
  scrollRef: MutableRefObject<number>
  keysRef: MutableRefObject<Keys>
}>

const MIN_SEND_MS = 45
const MAX_GAP_MS = 650

function encodeSnap(rb: RapierRigidBody, scroll: number, k: Keys): DriveSnapWire {
  const t = rb.translation()
  const r = rb.rotation()
  const v = rb.linvel()
  const o = rb.angvel()
  return {
    p: [t.x, t.y, t.z],
    q: [r.x, r.y, r.z, r.w],
    v: [v.x, v.y, v.z],
    o: [o.x, o.y, o.z],
    s: scroll,
    k: keysToBits(k),
  }
}

async function postSnap(room: string, snap: DriveSnapWire): Promise<void> {
  const enc = encodeURIComponent(room.trim())
  await fetch(`/api/room/${enc}/drive`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ snap }),
  })
}

export function AdventureDriveSync({ room, seat, carRef, scrollRef, keysRef }: Props) {
  const prevKeys = useRef<Keys>({ w: false, s: false, a: false, d: false })
  const lastSendMs = useRef(0)
  const lastRemoteUpdatedAt = useRef(0)

  // Driver: send on WASD edge + heartbeat if gap too long while moving / holding keys.
  useFrame(() => {
    const r = room.trim()
    if (!r || seat !== 'driver') return
    const rb = carRef.current
    if (!rb) return

    const k = keysRef.current
    const pk = prevKeys.current
    const edge = k.w !== pk.w || k.s !== pk.s || k.a !== pk.a || k.d !== pk.d
    pk.w = k.w
    pk.s = k.s
    pk.a = k.a
    pk.d = k.d

    const lv = rb.linvel()
    const speedXZ = Math.hypot(lv.x, lv.z)
    const holding = k.w || k.s || k.a || k.d
    const now = performance.now()
    const gap = now - lastSendMs.current

    const needHeartbeat = gap >= MAX_GAP_MS && (holding || speedXZ > 0.12)
    if (!edge && !needHeartbeat) return
    if (gap < MIN_SEND_MS && !edge) return

    lastSendMs.current = now
    const snap = encodeSnap(rb, scrollRef.current, k)
    void postSnap(r, snap)
  })

  // Passenger: poll remote snap and snap the rigid body.
  useEffect(() => {
    const r = room.trim()
    if (!r || seat !== 'passenger') return

    let cancelled = false
    const tick = async () => {
      const rb = carRef.current
      if (!rb || cancelled) return
      try {
        const enc = encodeURIComponent(r)
        const res = await fetch(`/api/room/${enc}/drive`)
        if (!res.ok || cancelled) return
        const j = (await res.json()) as { updatedAt?: number; snap?: DriveSnapWire | null }
        if (!j.snap || cancelled) return
        if (j.updatedAt && j.updatedAt === lastRemoteUpdatedAt.current) return
        lastRemoteUpdatedAt.current = j.updatedAt ?? 0

        const s = j.snap
        rb.setTranslation({ x: s.p[0], y: s.p[1], z: s.p[2] }, true)
        rb.setRotation({ x: s.q[0], y: s.q[1], z: s.q[2], w: s.q[3] }, true)
        rb.setLinvel({ x: s.v[0], y: s.v[1], z: s.v[2] }, true)
        rb.setAngvel({ x: s.o[0], y: s.o[1], z: s.o[2] }, true)
        scrollRef.current = s.s
        const speedXZ = Math.hypot(s.v[0], s.v[2])
        useAdventureHudStore.getState().setDrive(s.s, speedXZ)
      } catch {
        /* ignore transient network errors */
      }
    }

    const id = window.setInterval(() => void tick(), 130)
    void tick()
    return () => {
      cancelled = true
      window.clearInterval(id)
    }
  }, [room, seat, carRef, scrollRef])

  return null
}

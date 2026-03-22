'use client'

import { Canvas } from '@react-three/fiber'
import * as THREE from 'three'
import { useEffect, useState } from 'react'
import {
  AdventureScene,
  type AdventureDriveSeat,
  useAdventureBillboardStatusStore,
  useAdventureHudStore,
} from '../adventure'
import { BalatroFanSiteAttributionFooter } from '../components/BalatroFanSiteAttributionFooter'
import {
  MultiplayerRoomBar,
  readDriveSeatFromUrl,
  readMultiplayerRoomFromUrl,
} from '../components/MultiplayerRoomBar'
import { SeedControls } from '../components/SeedControls'
import { ADVENTURE_CAMERA, MAX_DPR } from '../r3f.config'

function AdventureHudReadout() {
  const scroll = useAdventureHudStore((s) => s.scroll)
  const speed = useAdventureHudStore((s) => s.speed)
  const { billboardLoading, billboardError, billboardJokerCount, billboardNote } =
    useAdventureBillboardStatusStore()
  const billboardLine = (() => {
    if (billboardLoading) return 'Loading billboard jokers…'
    if (billboardError) return `Billboards — ${billboardError}`
    if (billboardJokerCount <= 0) return 'No billboard jokers (unexpected).'
    const note = billboardNote ? ` · ${billboardNote}` : ''
    return `${billboardJokerCount} jokers (shop order from Balatro seed below · Red / White)${note}`
  })()
  return (
    <>
      <p>
        Odometer: {scroll.toFixed(0)} · Speed (XZ): {speed.toFixed(1)}
      </p>
      <p>Left billboards: {billboardLine}</p>
      <p>Deli strip · right · near start</p>
    </>
  )
}

type Props = Readonly<{
  onBack: () => void
  onJokers: () => void
  onShop: () => void
  onMotelyShop: () => void
}>

function setDriveSeatInUrl(seat: AdventureDriveSeat) {
  const u = new URL(window.location.href)
  if (seat === 'driver') u.searchParams.delete('seat')
  else u.searchParams.set('seat', 'passenger')
  window.history.replaceState({}, '', `${u.pathname}${u.search}${u.hash}`)
}

export default function AdventureApp({ onBack, onJokers, onShop, onMotelyShop }: Props) {
  const [driveRoom, setDriveRoom] = useState('')
  const [driveSeat, setDriveSeat] = useState<AdventureDriveSeat>('driver')

  useEffect(() => {
    setDriveRoom(readMultiplayerRoomFromUrl())
    setDriveSeat(readDriveSeatFromUrl())
  }, [])

  return (
    <>
      <div className="app-shell">
        <Canvas
          shadows
          camera={ADVENTURE_CAMERA}
          dpr={[1, MAX_DPR]}
          gl={{
            antialias: true,
            toneMapping: THREE.ACESFilmicToneMapping,
            toneMappingExposure: 1.05,
          }}
          style={{ touchAction: 'none' }}
        >
          <AdventureScene driveRoom={driveRoom} driveSeat={driveSeat} />
        </Canvas>

        <div className="ui-overlay">
          <div className="title">
            <h1>ADVENTURE</h1>
            <p>Highway · analyzer</p>
          </div>
          <div className="instructions">
            <p>W / ↑ gas · S / ↓ brake · A/D steer (Rapier torque + angular momentum)</p>
            <p style={{ marginTop: 8, marginBottom: 4, color: '#888', fontSize: '0.7rem' }}>
              Left billboards are <strong>not random</strong>: same <strong>Balatro seed</strong> as the
              analyzer (below) → same joker order every time. Built from Motely{' '}
              <code>analyzeSeed</code> ante‑1 shop queue when WASM loads, then TS{' '}
              <code>nextShopItem</code> top‑up — deck <code>Red</code>, stake <code>White</code>.
            </p>
            <SeedControls />
            <MultiplayerRoomBar
              value={driveRoom}
              onChange={(v) => setDriveRoom(v)}
              title="Highway co-op: same room + driver sends sparse snaps (WASD edges + heartbeat) to Upstash. Passenger uses ?seat=passenger."
            />
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 6, flexWrap: 'wrap' }}>
              <span style={{ color: '#888', fontSize: '0.7rem' }}>Highway seat</span>
              <button
                type="button"
                className="tool-switch"
                style={{ fontSize: 11, padding: '4px 10px' }}
                onClick={() => {
                  setDriveSeat('driver')
                  setDriveSeatInUrl('driver')
                }}
              >
                Driver
              </button>
              <button
                type="button"
                className="tool-switch"
                style={{ fontSize: 11, padding: '4px 10px' }}
                onClick={() => {
                  setDriveSeat('passenger')
                  setDriveSeatInUrl('passenger')
                }}
              >
                Passenger (ride-along)
              </button>
            </div>
            <AdventureHudReadout />
          </div>
          <div className="tool-nav">
            <button type="button" className="tool-switch" onClick={onBack}>
              ← Balatro 3D
            </button>
            <button type="button" className="tool-switch" onClick={onJokers}>
              Joker classifier →
            </button>
            <button type="button" className="tool-switch" onClick={onShop}>
              TS shop stream →
            </button>
            <button type="button" className="tool-switch" onClick={onMotelyShop}>
              Motely shop (WASM) →
            </button>
          </div>
        </div>
      </div>
      <BalatroFanSiteAttributionFooter />
    </>
  )
}

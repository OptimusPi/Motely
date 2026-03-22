import { Environment, Stars } from '@react-three/drei'
import { Physics, RigidBody, CuboidCollider } from '@react-three/rapier'
import type { RapierRigidBody } from '@react-three/rapier'
import { Suspense, useEffect, useRef, useState } from 'react'
import { useBalatroStore } from '../balatro/store/balatroStore'
import { R3FErrorBoundary } from '../components/R3FErrorBoundary'
import { PHYSICS } from '../r3f.config'
import { AdventureCamera } from './AdventureCamera'
import { setAdventureBillboardStatus } from './adventureBillboardStatusStore'
import { loadHighwayBillboardJokersMotely } from './highwayBillboardJokersMotely'
import { DeliStrip } from './DeliStrip'
import { InfiniteJokerBillboards } from './InfiniteJokerBillboards'
import { AdventureDriveSync } from './AdventureDriveSync'
import { PhysicsVehicle } from './PhysicsVehicle'
import type { BalatroJokerCenter } from '../balatro/spriteAtlas/jokerRegistry'

/** Billboard sampling: Motely WASM ante‑1 shopQueue first, TS stream top‑up. */
const BILLBOARD_DECK = 'Red'
const BILLBOARD_STAKE = 'White'

type Keys = { w: boolean; s: boolean; a: boolean; d: boolean }

export type AdventureDriveSeat = 'driver' | 'passenger'

type AdventureSceneProps = Readonly<{
  driveRoom?: string
  driveSeat?: AdventureDriveSeat
}>

export function AdventureScene({ driveRoom = '', driveSeat = 'driver' }: AdventureSceneProps = {}) {
  const carRef = useRef<RapierRigidBody>(null)
  const scrollRef = useRef(0)
  const keysRef = useRef<Keys>({ w: false, s: false, a: false, d: false })
  const gameSeed = useBalatroStore((s) => s.gameSeed)
  const [billboardJokers, setBillboardJokers] = useState<BalatroJokerCenter[] | null>(null)

  useEffect(() => {
    let cancelled = false
    setAdventureBillboardStatus({
      billboardLoading: true,
      billboardError: null,
      billboardJokerCount: 0,
      billboardNote: null,
    })
    void loadHighwayBillboardJokersMotely(gameSeed, BILLBOARD_DECK, BILLBOARD_STAKE).then((result) => {
      if (cancelled) return
      setBillboardJokers(result.jokers.length > 0 ? result.jokers : null)
      setAdventureBillboardStatus({
        billboardLoading: false,
        billboardError: result.jokers.length === 0 ? (result.note ?? 'No billboard jokers.') : null,
        billboardJokerCount: result.jokers.length,
        billboardNote: result.note,
      })
    })
    return () => {
      cancelled = true
    }
  }, [gameSeed])

  useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.code === 'KeyW' || e.code === 'ArrowUp') keysRef.current.w = true
      if (e.code === 'KeyS' || e.code === 'ArrowDown') keysRef.current.s = true
      if (e.code === 'KeyA' || e.code === 'ArrowLeft') keysRef.current.a = true
      if (e.code === 'KeyD' || e.code === 'ArrowRight') keysRef.current.d = true
    }
    const up = (e: KeyboardEvent) => {
      if (e.code === 'KeyW' || e.code === 'ArrowUp') keysRef.current.w = false
      if (e.code === 'KeyS' || e.code === 'ArrowDown') keysRef.current.s = false
      if (e.code === 'KeyA' || e.code === 'ArrowLeft') keysRef.current.a = false
      if (e.code === 'KeyD' || e.code === 'ArrowRight') keysRef.current.d = false
    }
    window.addEventListener('keydown', down)
    window.addEventListener('keyup', up)
    return () => {
      window.removeEventListener('keydown', down)
      window.removeEventListener('keyup', up)
    }
  }, [])

  return (
    <>
      <AdventureCamera carRef={carRef} />
      <color attach="background" args={['#06060d']} />
      <fog attach="fog" args={['#06060d', 32, 240]} />

      <ambientLight intensity={0.35} />
      <directionalLight
        position={[18, 28, 8]}
        intensity={1.1}
        castShadow
        shadow-mapSize={[1024, 1024]}
        shadow-camera-far={120}
        shadow-camera-left={-40}
        shadow-camera-right={40}
        shadow-camera-top={40}
        shadow-camera-bottom={-40}
      />

      <Stars radius={160} depth={52} count={3500} factor={3} saturation={0} fade speed={0.4} />
      <Environment preset="night" />

      <Physics gravity={PHYSICS.GRAVITY} timeStep={PHYSICS.TIME_STEP} interpolate>
        <RigidBody type="fixed" colliders={false} position={[0, -0.08, -140]}>
          <CuboidCollider args={[8, 0.2, 260]} friction={1.25} restitution={0.05} />
          <mesh rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
            <planeGeometry args={[16, 520]} />
            <meshStandardMaterial color="#15151f" roughness={0.92} metalness={0.02} />
          </mesh>
          <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.004, 0]}>
            <planeGeometry args={[0.12, 520]} />
            <meshStandardMaterial color="#f1c40f" emissive="#f1c40f" emissiveIntensity={0.15} />
          </mesh>
        </RigidBody>

        <PhysicsVehicle
          ref={carRef}
          keysRef={keysRef}
          scrollRef={scrollRef}
          controlsEnabled={driveSeat !== 'passenger'}
        />
        {driveRoom.trim() ? (
          <AdventureDriveSync
            room={driveRoom}
            seat={driveSeat}
            carRef={carRef}
            scrollRef={scrollRef}
            keysRef={keysRef}
          />
        ) : null}

        <R3FErrorBoundary>
          <Suspense fallback={null}>
            <InfiniteJokerBillboards scrollRef={scrollRef} jokers={billboardJokers} />
            <DeliStrip />
          </Suspense>
        </R3FErrorBoundary>
      </Physics>
    </>
  )
}

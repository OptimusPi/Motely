import { useFrame } from '@react-three/fiber'
import { RigidBody, type RapierRigidBody } from '@react-three/rapier'
import { forwardRef, useRef, type ForwardedRef, type MutableRefObject } from 'react'
import * as THREE from 'three'
import { useAdventureHudStore } from './adventureHudStore'
import { VEHICLE } from '../r3f.config'

const _forward = new THREE.Vector3()
const _drag = new THREE.Vector3()
const _quat = new THREE.Quaternion()

type Keys = { w: boolean; s: boolean; a: boolean; d: boolean }

type Props = Readonly<{
  keysRef: MutableRefObject<Keys>
  scrollRef: MutableRefObject<number>
  spawnZ?: number
  /** When false (passenger / ride-along), skip local driving impulses. */
  controlsEnabled?: boolean
}>

function readRigidBodyRef(r: ForwardedRef<RapierRigidBody>): RapierRigidBody | null {
  if (r == null || typeof r === 'function') return null
  return r.current
}

/**
 * Arcade car: forward impulse along chassis heading, yaw via torque (real angular momentum from Rapier).
 */
export const PhysicsVehicle = forwardRef<RapierRigidBody, Props>(function PhysicsVehicle(
  { keysRef, scrollRef, spawnZ = 4, controlsEnabled = true },
  ref
) {
  const hudAcc = useRef(0)

  useFrame((_, dt) => {
    const rb = readRigidBodyRef(ref)
    if (!rb) return

    const t = rb.translation()
    scrollRef.current = Math.max(0, spawnZ - t.z)

    const r = rb.rotation()
    _quat.set(r.x, r.y, r.z, r.w)
    _forward.set(0, 0, -1).applyQuaternion(_quat)

    const lv = rb.linvel()
    const speedXZ = Math.hypot(lv.x, lv.z)
    const k = keysRef.current

    if (!controlsEnabled) return

    const thrust = VEHICLE.THRUST * dt
    if (k.w) {
      rb.applyImpulse({ x: _forward.x * thrust, y: 0, z: _forward.z * thrust }, true)
    }
    if (k.s) {
      const fwdDot = lv.x * _forward.x + lv.z * _forward.z
      if (fwdDot > 0.4) {
        _drag.set(-lv.x, 0, -lv.z)
        const m = Math.hypot(_drag.x, _drag.z)
        if (m > 1e-4) _drag.multiplyScalar((VEHICLE.BRAKE * dt) / m)
        rb.applyImpulse({ x: _drag.x, y: 0, z: _drag.z }, true)
      } else {
        rb.applyImpulse(
          { x: -_forward.x * VEHICLE.REVERSE * dt, y: 0, z: -_forward.z * VEHICLE.REVERSE * dt },
          true
        )
      }
    }

    const steerGain = THREE.MathUtils.clamp(speedXZ * VEHICLE.STEER_SPEED_SCALE, 0.35, 3.2)
    const torque = VEHICLE.STEER_TORQUE * steerGain * dt
    if (k.a) rb.applyTorqueImpulse({ x: 0, y: torque, z: 0 }, true)
    if (k.d) rb.applyTorqueImpulse({ x: 0, y: -torque, z: 0 }, true)

    if (speedXZ > VEHICLE.MAX_SPEED) {
      const s = VEHICLE.MAX_SPEED / speedXZ
      rb.setLinvel({ x: lv.x * s, y: lv.y, z: lv.z * s }, true)
    }

    hudAcc.current += dt
    if (hudAcc.current >= VEHICLE.HUD_INTERVAL) {
      hudAcc.current = 0
      useAdventureHudStore.getState().setDrive(scrollRef.current, speedXZ)
    }
  })

  return (
    <RigidBody
      ref={ref}
      position={[0, 0.29, spawnZ]}
      colliders="cuboid"
      mass={VEHICLE.MASS}
      linearDamping={VEHICLE.LINEAR_DAMPING}
      angularDamping={VEHICLE.ANGULAR_DAMPING}
      friction={VEHICLE.FRICTION}
      restitution={VEHICLE.RESTITUTION}
      enabledRotations={[false, true, false]}
      canSleep={false}
    >
      <mesh castShadow receiveShadow>
        <boxGeometry args={[0.9, 0.34, 1.78]} />
        <meshStandardMaterial color="#34495e" metalness={0.45} roughness={0.38} />
      </mesh>
    </RigidBody>
  )
})

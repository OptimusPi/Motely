import { useFrame, useThree } from '@react-three/fiber'
import type { RapierRigidBody } from '@react-three/rapier'
import { useRef, type RefObject } from 'react'
import * as THREE from 'three'

const _quat = new THREE.Quaternion()
const _behind = new THREE.Vector3()
const _look = new THREE.Vector3()
const _camPos = new THREE.Vector3()
const _lookAt = new THREE.Vector3()

type Props = Readonly<{
  carRef: RefObject<RapierRigidBody | null>
}>

/**
 * Chase cam: follows Rapier chassis with smoothed position + look-at (momentum visible on yaw).
 */
export function AdventureCamera({ carRef }: Props) {
  const { camera } = useThree()
  const smoothPos = useRef(new THREE.Vector3(0, 2.4, 8))
  const smoothLook = useRef(new THREE.Vector3(0, 0.5, -20))

  useFrame((_, dt) => {
    const rb = carRef.current
    if (!rb) {
      smoothPos.current.lerp(new THREE.Vector3(0, 2.35, 9), 1 - Math.exp(-4 * dt))
      smoothLook.current.lerp(new THREE.Vector3(0, 0.8, -30), 1 - Math.exp(-4 * dt))
      camera.position.copy(smoothPos.current)
      camera.lookAt(smoothLook.current)
      return
    }

    const t = rb.translation()
    const r = rb.rotation()
    _quat.set(r.x, r.y, r.z, r.w)

    _behind.set(0, 2.05, 7.8).applyQuaternion(_quat)
    _look.set(0, 0.4, -16).applyQuaternion(_quat)

    _camPos.set(t.x + _behind.x, t.y + _behind.y, t.z + _behind.z)
    _lookAt.set(t.x, t.y, t.z).add(_look)

    const a = 1 - Math.exp(-5.2 * dt)
    const b = 1 - Math.exp(-6.5 * dt)
    smoothPos.current.lerp(_camPos, a)
    smoothLook.current.lerp(_lookAt, b)
    camera.position.copy(smoothPos.current)
    camera.lookAt(smoothLook.current)
  })

  return null
}

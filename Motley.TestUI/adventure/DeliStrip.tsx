import { useTexture } from '@react-three/drei'
import { useEffect, useMemo } from 'react'
import * as THREE from 'three'
import { BALATRO_JOKER_ATLAS, BALATRO_JOKERS } from '../balatro/spriteAtlas/jokerRegistry'
import { cloneAtlasSliceForJoker, jokerPlaneSizeFromTexture } from './jokerAtlasSlice'

const DELI_CENTER = BALATRO_JOKERS.find((j) => j.key === 'j_loyalty_card')

const STORE_Z: readonly number[] = [-6, -22, -38]

const RIGHT_X = 6.8

/**
 * Starter shops on the right shoulder — first stretch of the highway only (analyzer stub).
 */
export function DeliStrip() {
  const baseAtlas = useTexture(BALATRO_JOKER_ATLAS.publicPath)
  baseAtlas.colorSpace = THREE.SRGBColorSpace
  baseAtlas.magFilter = THREE.NearestFilter
  baseAtlas.minFilter = THREE.NearestFilter

  const { signTex, size } = useMemo(() => {
    if (!DELI_CENTER) return { signTex: null as THREE.Texture | null, size: { w: 2, h: 2.8 } }
    const signTex = cloneAtlasSliceForJoker(baseAtlas, DELI_CENTER)
    const size = jokerPlaneSizeFromTexture(signTex)
    return { signTex, size }
  }, [baseAtlas])

  useEffect(() => {
    return () => {
      signTex?.dispose()
    }
  }, [signTex])

  if (!DELI_CENTER || !signTex) return null

  return (
    <group>
      {STORE_Z.map((z) => (
        <group key={z} position={[RIGHT_X, 0, z]}>
          <mesh position={[0, 1.1, 0]} castShadow receiveShadow>
            <boxGeometry args={[4.2, 2.2, 3.2]} />
            <meshStandardMaterial color="#3d3548" roughness={0.88} metalness={0.08} />
          </mesh>
          <mesh position={[-2.18, 1.35, 0]} rotation={[0, -Math.PI / 2, 0]}>
            <planeGeometry args={[size.w * 0.85, size.h * 0.85]} />
            <meshStandardMaterial map={signTex} transparent roughness={0.75} />
          </mesh>
          <mesh position={[0, 2.35, 1.62]}>
            <boxGeometry args={[3.6, 0.45, 0.12]} />
            <meshStandardMaterial
              color="#c0392b"
              emissive="#e74c3c"
              emissiveIntensity={0.35}
              roughness={0.6}
            />
          </mesh>
        </group>
      ))}
    </group>
  )
}

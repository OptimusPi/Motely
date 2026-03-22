import { useFrame } from '@react-three/fiber'
import { Billboard, useTexture } from '@react-three/drei'
import { useEffect, useMemo, useRef, useState, type MutableRefObject } from 'react'
import * as THREE from 'three'
import { BALATRO_JOKER_ATLAS, type BalatroJokerCenter } from '../balatro/spriteAtlas/jokerRegistry'
import { BILLBOARD_COUNT, BILLBOARD_SPACING } from './highwayConstants'
import { cloneAtlasSliceForJoker, jokerPlaneSizeFromTexture } from './jokerAtlasSlice'

const NUM_BOARDS = BILLBOARD_COUNT
const SPACING = BILLBOARD_SPACING
const LEFT_X = -7.2
const BOARD_Y = 2.1

type Props = Readonly<{
  scrollRef: MutableRefObject<number>
  /**
   * Shop-order jokers for the run. `null` while loading or on fatal error.
   */
  jokers: BalatroJokerCenter[] | null
}>

/**
 * Left-side joker billboards: mesh positions loop in Z (endless road), while each full cycle
 * advances a window through `jokers` so the ~96 shop-order picks are not stuck on the first 28.
 */
export function InfiniteJokerBillboards({ scrollRef, jokers }: Props) {
  const baseAtlas = useTexture(BALATRO_JOKER_ATLAS.publicPath)
  baseAtlas.colorSpace = THREE.SRGBColorSpace
  baseAtlas.magFilter = THREE.NearestFilter
  baseAtlas.minFilter = THREE.NearestFilter

  const cycle = NUM_BOARDS * SPACING
  const [lapSegment, setLapSegment] = useState(0)
  const lastLapSegmentRef = useRef(0)

  const { textures, centers, size0 } = useMemo(() => {
    if (!jokers || jokers.length === 0) {
      return {
        textures: [] as THREE.Texture[],
        centers: [] as BalatroJokerCenter[],
        size0: { w: 1, h: 1 },
      }
    }
    const L = jokers.length
    const base = lapSegment * NUM_BOARDS
    const centers = Array.from(
      { length: NUM_BOARDS },
      (_, i) => jokers[(base + i) % L]!
    )
    const textures = centers.map((c) => cloneAtlasSliceForJoker(baseAtlas, c))
    const size0 = jokerPlaneSizeFromTexture(textures[0]!)
    return { textures, centers, size0 }
  }, [baseAtlas, jokers, lapSegment])

  useEffect(() => {
    return () => {
      for (const t of textures) t.dispose()
    }
  }, [textures])

  const groupRef = useRef<THREE.Group>(null)

  useFrame(() => {
    const scroll = scrollRef.current
    const seg = Math.floor(scroll / cycle)
    if (seg !== lastLapSegmentRef.current) {
      lastLapSegmentRef.current = seg
      setLapSegment(seg)
    }

    const g = groupRef.current
    if (!g) return
    const wrap = scroll % cycle
    for (let i = 0; i < NUM_BOARDS; i++) {
      const child = g.children[i]
      if (!(child instanceof THREE.Group)) continue
      const z = -i * SPACING + wrap
      child.position.set(LEFT_X, BOARD_Y, z)
    }
  })

  if (!jokers || jokers.length === 0) return null

  return (
    <group ref={groupRef}>
      {textures.map((map, i) => (
        <group key={`${centers[i]!.key}-${i}`} position={[LEFT_X, BOARD_Y, -i * SPACING]}>
          {/*
            Plane default is XY with normal +Z; Billboard orients +Z toward the chase cam so you see
            the joker face-on instead of the thin edge (old fixed Y-rotation toward +X).
          */}
          <Billboard follow>
            <mesh castShadow>
              <planeGeometry args={[size0.w, size0.h]} />
              <meshStandardMaterial map={map} transparent roughness={0.85} metalness={0.05} />
            </mesh>
            <mesh position={[0, -size0.h * 0.5 - 1.15, 0]}>
              <boxGeometry args={[0.14, 2.3, 0.14]} />
              <meshStandardMaterial color="#2a2a35" roughness={0.9} />
            </mesh>
          </Billboard>
        </group>
      ))}
    </group>
  )
}

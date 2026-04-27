'use client'

import { useRef, useMemo, useState, useEffect, memo } from 'react'
import { useFrame, type ThreeEvent } from '@react-three/fiber'
import { useSpring, animated } from '@react-spring/three'
import * as THREE from 'three'
import { SPRITE_SHEETS } from '../sprites/spriteData.js'
import type { SpriteData, SpriteSheetType } from '../sprites/spriteMapper.js'

export const CARD_DIMENSIONS = { WIDTH: 0.7, HEIGHT: 0.95, DEPTH: 0.02 } as const
export const CARD_MAGNET = {
  MAX_TILT_X: 0.36,
  MAX_TILT_Y: 0.42,
  MAX_SHIFT: 0.038,
  TWIST_Z: 0.11,
  LERP_IN: 18,
  LERP_OUT: 10,
} as const

const SHEET_KEY_MAP: Record<SpriteSheetType, keyof typeof SPRITE_SHEETS> = {
  Jokers: 'jokers',
  Tarots: 'tarots',
  Vouchers: 'vouchers',
  Boosters: 'boosters',
  Enhancers: 'enhancers',
  Editions: 'editions',
  BlindChips: 'blinds',
  tags: 'tags',
  Stakes: 'stakes',
  Decks: 'deck',
}

const _textureCache = new Map<string, THREE.Texture>()

function useSpriteTexture(sprite: SpriteData) {
  const [texture, setTexture] = useState<THREE.Texture | null>(null)
  const serial = useRef(0)

  useEffect(() => {
    const id = ++serial.current
    const sheet = SPRITE_SHEETS[SHEET_KEY_MAP[sprite.type]]
    const url = sheet.src
    const cols = sheet.columns
    const rows = sheet.rows
    const { x, y } = sprite.pos

    const applySlice = (base: THREE.Texture): THREE.Texture => {
      const t = base.clone()
      t.repeat.set(1 / cols, 1 / rows)
      t.offset.set(x / cols, (rows - y - 1) / rows)
      t.needsUpdate = true
      return t
    }

    if (_textureCache.has(url)) {
      setTexture(applySlice(_textureCache.get(url)!))
      return
    }

    const loader = new THREE.TextureLoader()
    loader.load(
      url,
      (loaded) => {
        if (id !== serial.current) return
        loaded.colorSpace = THREE.SRGBColorSpace
        loaded.magFilter = THREE.NearestFilter
        loaded.minFilter = THREE.NearestFilter
        _textureCache.set(url, loaded)
        setTexture(applySlice(loaded))
      },
      undefined,
      (err) => console.error('[Card3D] texture load failed:', url, err),
    )
  }, [sprite.type, sprite.pos.x, sprite.pos.y])

  return texture
}

export interface Card3DProps {
  sprite: SpriteData
  position?: [number, number, number]
  rotation?: [number, number, number]
  selected?: boolean
  highlighted?: boolean
  onClick?: () => void
  onPointerEnter?: () => void
  onPointerLeave?: () => void
}

export const Card3D = memo(function Card3D({
  sprite,
  position = [0, 0, 0],
  rotation = [0, 0, 0],
  selected = false,
  highlighted = false,
  onClick,
  onPointerEnter,
  onPointerLeave,
}: Card3DProps) {
  const tiltRef = useRef<THREE.Group>(null)
  const target = useRef({ rx: 0, ry: 0, rz: 0, ox: 0, oy: 0 })
  const [hovered, setHovered] = useState(false)

  const texture = useSpriteTexture(sprite)

  const { posY, scale } = useSpring({
    posY: selected ? 0.3 : hovered ? 0.15 : 0,
    scale: hovered ? 1.08 : selected ? 1.05 : 1,
    config: { tension: 300, friction: 20 },
  })

  useFrame((_state, dt) => {
    const g = tiltRef.current
    if (!g) return
    const t = target.current
    const rate = hovered ? CARD_MAGNET.LERP_IN : CARD_MAGNET.LERP_OUT
    const a = 1 - Math.exp(-rate * dt)
    g.rotation.x = THREE.MathUtils.lerp(g.rotation.x, t.rx, a)
    g.rotation.y = THREE.MathUtils.lerp(g.rotation.y, t.ry, a)
    g.rotation.z = THREE.MathUtils.lerp(g.rotation.z, t.rz, a)
    g.position.x = THREE.MathUtils.lerp(g.position.x, t.ox, a)
    g.position.y = THREE.MathUtils.lerp(g.position.y, t.oy, a)
  })

  const glowColor = useMemo(() => highlighted ? '#e4b643' : '#ffffff', [highlighted])

  const onMove = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    const uv = e.uv
    if (!uv) return
    const nx = THREE.MathUtils.clamp((uv.x - 0.5) * 2, -1, 1)
    const ny = THREE.MathUtils.clamp((uv.y - 0.5) * 2, -1, 1)
    target.current.ry = -nx * CARD_MAGNET.MAX_TILT_Y
    target.current.rx =  ny * CARD_MAGNET.MAX_TILT_X
    target.current.rz = -nx * ny * CARD_MAGNET.TWIST_Z
    target.current.ox =  nx * CARD_MAGNET.MAX_SHIFT
    target.current.oy = -ny * CARD_MAGNET.MAX_SHIFT * 0.65
  }

  const reset = () => { target.current = { rx: 0, ry: 0, rz: 0, ox: 0, oy: 0 } }

  if (!texture) return null

  return (
    <animated.group
      position-x={position[0]}
      position-y={posY.to((y) => position[1] + y)}
      position-z={position[2]}
      rotation-x={rotation[0]}
      rotation-y={rotation[1]}
      rotation-z={rotation[2]}
      scale={scale}
    >
      <group ref={tiltRef}>
        {highlighted && (
          <pointLight color={glowColor} intensity={1.5} distance={1} position={[0, 0, 0.1]} />
        )}
        <mesh
          onClick={(e) => { e.stopPropagation(); onClick?.() }}
          onPointerMove={onMove}
          onPointerEnter={(e) => { e.stopPropagation(); setHovered(true); onPointerEnter?.(); document.body.style.cursor = 'pointer' }}
          onPointerLeave={(e) => { e.stopPropagation(); setHovered(false); reset(); onPointerLeave?.(); document.body.style.cursor = 'auto' }}
          castShadow
          receiveShadow
        >
          <boxGeometry args={[CARD_DIMENSIONS.WIDTH, CARD_DIMENSIONS.HEIGHT, CARD_DIMENSIONS.DEPTH]} />
          <meshBasicMaterial attach="material-4" map={texture} toneMapped={false} />
          <meshStandardMaterial attach="material-5" color="#1a1a2e" metalness={0.2} roughness={0.8} />
          <meshStandardMaterial attach="material-0" color="#f5f5dc" />
          <meshStandardMaterial attach="material-1" color="#f5f5dc" />
          <meshStandardMaterial attach="material-2" color="#f5f5dc" />
          <meshStandardMaterial attach="material-3" color="#f5f5dc" />
        </mesh>
        {selected && (
          <mesh position={[0, 0, -CARD_DIMENSIONS.DEPTH]}>
            <ringGeometry args={[0.45, 0.5, 32]} />
            <meshBasicMaterial color="#e4b643" transparent opacity={0.8} />
          </mesh>
        )}
      </group>
    </animated.group>
  )
})

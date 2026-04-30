'use client'

import { useRef, useMemo, useState, useEffect, memo } from 'react'
import { useFrame, useLoader, type ThreeEvent } from '@react-three/fiber'
import { useSpring, animated } from '@react-spring/three'
import * as THREE from 'three'
import type { MotelySpriteData } from '../decode/motelySprite.js'

export const CARD_DIMENSIONS = { WIDTH: 0.7, HEIGHT: 0.95, DEPTH: 0.02 } as const
export const CARD_MAGNET = {
  MAX_TILT_X: 0.36,
  MAX_TILT_Y: 0.42,
  MAX_SHIFT: 0.038,
  TWIST_Z: 0.11,
  LERP_IN: 18,
  LERP_OUT: 10,
} as const

function useSpriteTexture(sprite: MotelySpriteData) {
  const texture = useLoader(THREE.TextureLoader, sprite.atlasPath);
  
  return useMemo(() => {
    const t = texture.clone();
    t.colorSpace = THREE.SRGBColorSpace;
    t.magFilter = THREE.NearestFilter;
    t.minFilter = THREE.NearestFilter;
    t.repeat.set(1 / sprite.gridCols, 1 / sprite.gridRows);
    t.offset.set(
      sprite.gridCol / sprite.gridCols,
      1 - ((sprite.gridRow + 1) / sprite.gridRows)
    );
    t.needsUpdate = true;
    return t;
  }, [texture, sprite.gridCol, sprite.gridRow, sprite.gridCols, sprite.gridRows]);
}

export interface Card3DProps {
  sprite: MotelySpriteData
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
      {/* Invisible hit mesh that does not shift or twist, preventing boop-cancel loops */}
      <mesh
        visible={false}
        onClick={(e) => { e.stopPropagation(); onClick?.() }}
        onPointerMove={onMove}
        onPointerEnter={(e) => { e.stopPropagation(); setHovered(true); onPointerEnter?.(); document.body.style.cursor = 'pointer' }}
        onPointerLeave={(e) => { e.stopPropagation(); setHovered(false); reset(); onPointerLeave?.(); document.body.style.cursor = 'auto' }}
      >
        <boxGeometry args={[CARD_DIMENSIONS.WIDTH, CARD_DIMENSIONS.HEIGHT, CARD_DIMENSIONS.DEPTH * 2]} />
        <meshBasicMaterial />
      </mesh>

      <group ref={tiltRef}>
        {highlighted && (
          <pointLight color={glowColor} intensity={1.5} distance={1} position={[0, 0, 0.1]} />
        )}
        <mesh castShadow receiveShadow>
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

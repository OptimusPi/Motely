import { useRef, useMemo, useState, useEffect, memo } from 'react'
import { useFrame, type ThreeEvent } from '@react-three/fiber'
import { useSpring, animated } from '@react-spring/three'
import * as THREE from 'three'
import type { Card, Suit, Rank } from '../types'
import { SUIT_COLORS } from '../types'
import { applyBalatroGridUV, getLoadedTexturePixelSize } from '../spriteAtlas/gridUV'
import { CARD_DIMENSIONS, CARD_MAGNET } from '../../r3f.config'

// Atlas config from balatro-playing-cards.json
const PLAYING_CARD_ATLAS = {
  publicPath: '/images/8BitDeck.png',
  cellPx: { x: 71, y: 95 },
  columns: 13,
  rows: 4,
}

// Suit rows match balatro-playing-cards.json (H=0, C=1, D=2, S=3)
const SUIT_ROW: Record<Suit, number> = {
  hearts: 0,
  clubs: 1,
  diamonds: 2,
  spades: 3,
}

// Rank columns: 2-10, J, Q, K, A (0-12)
const RANK_COLUMN: Record<Rank, number> = {
  '2': 0,
  '3': 1,
  '4': 2,
  '5': 3,
  '6': 4,
  '7': 5,
  '8': 6,
  '9': 7,
  '10': 8,
  J: 9,
  Q: 10,
  K: 11,
  A: 12,
}

interface Card3DProps {
  card: Card
  position?: [number, number, number]
  rotation?: [number, number, number]
  selected?: boolean
  highlighted?: boolean
  onClick?: () => void
  onPointerEnter?: () => void
  onPointerLeave?: () => void
  index?: number
  faceDown?: boolean
}

// Card back texture (simple procedural)
function useCardBackTexture() {
  return useMemo(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 71
    canvas.height = 95
    const ctx = canvas.getContext('2d')!

    // Dark background
    ctx.fillStyle = '#1a1a2e'
    ctx.fillRect(0, 0, 71, 95)

    // Border
    ctx.strokeStyle = '#c9a227'
    ctx.lineWidth = 2
    ctx.strokeRect(2, 2, 67, 91)

    // Inner pattern
    ctx.fillStyle = '#16213e'
    for (let y = 8; y < 87; y += 10) {
      for (let x = 8; x < 63; x += 10) {
        ctx.fillRect(x, y, 5, 5)
      }
    }

    // Center diamond
    ctx.fillStyle = '#c9a227'
    ctx.beginPath()
    ctx.moveTo(35.5, 30)
    ctx.lineTo(50, 47.5)
    ctx.lineTo(35.5, 65)
    ctx.lineTo(21, 47.5)
    ctx.closePath()
    ctx.fill()

    const tex = new THREE.CanvasTexture(canvas)
    tex.magFilter = THREE.NearestFilter
    tex.minFilter = THREE.NearestFilter
    tex.colorSpace = THREE.SRGBColorSpace
    return tex
  }, [])
}

// Shared texture loader and cache
const textureCache = new Map<string, THREE.Texture>()

function usePlayingCardTexture(suit: Suit, rank: Rank) {
  const [texture, setTexture] = useState<THREE.Texture | null>(null)
  const loadSerial = useRef(0)

  useEffect(() => {
    const serial = ++loadSerial.current
    let cancelled = false

    // Check cache first
    const cacheKey = `${suit}-${rank}`
    if (textureCache.has(cacheKey)) {
      setTexture(textureCache.get(cacheKey)!)
      return
    }

    const loader = new THREE.TextureLoader()
    loader.load(
      PLAYING_CARD_ATLAS.publicPath,
      (tex) => {
        if (cancelled || serial !== loadSerial.current) {
          tex.dispose()
          return
        }
        tex.colorSpace = THREE.SRGBColorSpace
        tex.magFilter = THREE.NearestFilter
        tex.minFilter = THREE.NearestFilter

        const { width: tw, height: th } = getLoadedTexturePixelSize(tex.image)
        const cellW = tw / PLAYING_CARD_ATLAS.columns
        const cellH = th / PLAYING_CARD_ATLAS.rows

        const pos = {
          x: RANK_COLUMN[rank],
          y: SUIT_ROW[suit],
        }

        applyBalatroGridUV(tex, pos, {
          cellW,
          cellH,
          textureWidth: tw,
          textureHeight: th,
        })

        textureCache.set(cacheKey, tex)
        setTexture(tex)
      },
      undefined,
      (err) => {
        console.error('Failed to load card texture:', err)
      }
    )

    return () => {
      cancelled = true
      // Don't dispose cached textures
    }
  }, [suit, rank])

  return texture
}

export const Card3D = memo(function Card3D({
  card,
  position = [0, 0, 0],
  rotation = [0, 0, 0],
  selected = false,
  highlighted = false,
  onClick,
  onPointerEnter,
  onPointerLeave,
  faceDown: _faceDown = false,
}: Card3DProps) {
  // _faceDown reserved for future card flip animation
  const tiltGroupRef = useRef<THREE.Group>(null)
  const magneticTarget = useRef({
    rx: 0,
    ry: 0,
    rz: 0,
    ox: 0,
    oy: 0,
  })
  const [hovered, setHovered] = useState(false)

  const cardTexture = usePlayingCardTexture(card.suit, card.rank)
  const backTexture = useCardBackTexture()

  // Animation spring
  const { posY, scale, glowIntensity } = useSpring({
    posY: selected ? 0.3 : hovered ? 0.15 : 0,
    scale: hovered ? 1.08 : selected ? 1.05 : 1,
    glowIntensity: highlighted ? 1 : hovered ? 0.5 : 0,
    config: { tension: 300, friction: 20 },
  })

  // Magnetic tilt + idle sway on the card content group
  useFrame((state, dt) => {
    const g = tiltGroupRef.current
    if (!g) return
    const t = magneticTarget.current
    const rate = hovered ? CARD_MAGNET.LERP_IN : CARD_MAGNET.LERP_OUT
    const a = 1 - Math.exp(-rate * dt)
    const idleZ = Math.sin(state.clock.elapsedTime * 0.55 + position[0] * 2.1) * 0.018

    g.rotation.x = THREE.MathUtils.lerp(g.rotation.x, t.rx, a)
    g.rotation.y = THREE.MathUtils.lerp(g.rotation.y, t.ry, a)
    g.rotation.z = THREE.MathUtils.lerp(g.rotation.z, t.rz + idleZ, a)
    g.position.x = THREE.MathUtils.lerp(g.position.x, t.ox, a)
    g.position.y = THREE.MathUtils.lerp(g.position.y, t.oy, a)
  })

  const resetMagnetic = () => {
    magneticTarget.current.rx = 0
    magneticTarget.current.ry = 0
    magneticTarget.current.rz = 0
    magneticTarget.current.ox = 0
    magneticTarget.current.oy = 0
  }

  const onCardPointerMove = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    const uv = e.uv
    if (!uv) return
    const nx = (uv.x - 0.5) * 2
    const ny = (uv.y - 0.5) * 2
    const clampedX = THREE.MathUtils.clamp(nx, -1, 1)
    const clampedY = THREE.MathUtils.clamp(ny, -1, 1)
    magneticTarget.current.ry = -clampedX * CARD_MAGNET.MAX_TILT_Y
    magneticTarget.current.rx = clampedY * CARD_MAGNET.MAX_TILT_X
    magneticTarget.current.rz = -clampedX * clampedY * CARD_MAGNET.TWIST_Z
    magneticTarget.current.ox = clampedX * CARD_MAGNET.MAX_SHIFT
    magneticTarget.current.oy = -clampedY * CARD_MAGNET.MAX_SHIFT * 0.65
  }

  // Get edition material properties
  const editionProps = useMemo(() => {
    switch (card.edition) {
      case 'foil':
        return { metalness: 0.9, roughness: 0.1 }
      case 'holographic':
        return { metalness: 0.7, roughness: 0.2 }
      case 'polychrome':
        return { metalness: 0.8, roughness: 0.15 }
      default:
        return { metalness: 0.1, roughness: 0.8 }
    }
  }, [card.edition])

  // Enhancement glow color
  const enhancementColor = useMemo(() => {
    switch (card.enhancement) {
      case 'bonus':
        return '#3498db'
      case 'mult':
        return '#e74c3c'
      case 'wild':
        return '#9b59b6'
      case 'glass':
        return '#1abc9c'
      case 'steel':
        return '#95a5a6'
      case 'gold':
        return '#f1c40f'
      case 'lucky':
        return '#2ecc71'
      default:
        return highlighted ? SUIT_COLORS[card.suit] : '#ffffff'
    }
  }, [card.enhancement, card.suit, highlighted])

  // Don't render until texture is loaded
  if (!cardTexture) {
    return null
  }

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
      <group ref={tiltGroupRef}>
        {/* Glow effect */}
        <animated.pointLight
          color={enhancementColor}
          intensity={glowIntensity.to((i) => i * 2)}
          distance={1}
          position={[0, 0, 0.1]}
        />

        {/* Card mesh — UV-driven magnetic tilt on pointer move */}
        <mesh
          onClick={(e) => {
            e.stopPropagation()
            const hit = e.intersections[0]
            if (!hit || hit.object !== e.object) return
            onClick?.()
          }}
          onPointerMove={onCardPointerMove}
          onPointerEnter={(e) => {
            e.stopPropagation()
            setHovered(true)
            onPointerEnter?.()
            document.body.style.cursor = 'pointer'
          }}
          onPointerLeave={(e) => {
            e.stopPropagation()
            setHovered(false)
            resetMagnetic()
            onPointerLeave?.()
            document.body.style.cursor = 'auto'
          }}
          castShadow
          receiveShadow
        >
          <boxGeometry args={[CARD_DIMENSIONS.WIDTH, CARD_DIMENSIONS.HEIGHT, CARD_DIMENSIONS.DEPTH]} />

          {/* Front face */}
          <meshBasicMaterial attach="material-4" map={cardTexture} toneMapped={false} />

          {/* Back face */}
          <meshStandardMaterial attach="material-5" map={backTexture} {...editionProps} />

          {/* Side faces */}
          <meshStandardMaterial attach="material-0" color="#f5f5dc" />
          <meshStandardMaterial attach="material-1" color="#f5f5dc" />
          <meshStandardMaterial attach="material-2" color="#f5f5dc" />
          <meshStandardMaterial attach="material-3" color="#f5f5dc" />
        </mesh>

        {/* Selection indicator ring */}
        {selected && (
          <mesh position={[0, 0, -CARD_DIMENSIONS.DEPTH]}>
            <ringGeometry args={[0.45, 0.5, 32]} />
            <meshBasicMaterial color="#f1c40f" transparent opacity={0.8} />
          </mesh>
        )}

        {/* Enhancement badge */}
        {card.enhancement && (
          <mesh position={[CARD_DIMENSIONS.WIDTH / 2 - 0.1, CARD_DIMENSIONS.HEIGHT / 2 - 0.1, CARD_DIMENSIONS.DEPTH + 0.01]}>
            <circleGeometry args={[0.08, 16]} />
            <meshBasicMaterial color={enhancementColor} />
          </mesh>
        )}

        {/* Seal indicator */}
        {card.seal && (
          <mesh position={[-CARD_DIMENSIONS.WIDTH / 2 + 0.1, -CARD_DIMENSIONS.HEIGHT / 2 + 0.1, CARD_DIMENSIONS.DEPTH + 0.01]}>
            <circleGeometry args={[0.06, 6]} />
            <meshBasicMaterial
              color={
                card.seal === 'gold'
                  ? '#f1c40f'
                  : card.seal === 'red'
                    ? '#e74c3c'
                    : card.seal === 'blue'
                      ? '#3498db'
                      : '#9b59b6'
              }
            />
          </mesh>
        )}
      </group>
    </animated.group>
  )
})

export default Card3D

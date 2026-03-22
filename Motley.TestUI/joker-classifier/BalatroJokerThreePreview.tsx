'use client'

import { Canvas, useFrame, type ThreeEvent } from '@react-three/fiber'
import { Suspense, useEffect, useMemo, useRef, useState } from 'react'
import * as THREE from 'three'
import { applyBalatroGridUV, getLoadedTexturePixelSize } from '../balatro/spriteAtlas/gridUV'
import {
  BALATRO_JOKER_ATLAS,
  findJokerByDisplayName,
  getJokerAtlasGridSize,
} from '../balatro/spriteAtlas/jokerRegistry'

/**
 * Magnetic tilt + ambient idle — mirrors Balatro `card.txt` Card:draw (tilt_var mx/my, amt)
 * and `sprite.txt` draw_shader (mouse_screen_pos / hovering). Shader there fakes 3D; here we
 * rotate a real plane toward the same logical cursor (real or orbiting).
 */
const AMBIENT_TILT = 0.2
const TILT_FACTOR = 0.3
const MAGNET_MAX_RX = 0.34
const MAGNET_MAX_RY = 0.4
const MAGNET_MAX_SHIFT = 0.05
const MAGNET_TWIST_Z = 0.1
const TILT_LERP_IN = 20
const TILT_LERP_OUT = 11

function stableIdFraction(s: string): number {
  let h = 2166136261
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i)
    h = Math.imul(h, 16777619)
  }
  return ((h >>> 0) % 100000) / 100000
}

type JokerPlaneProps = Readonly<{
  displayName: string
  onAtlasOk: () => void
  onAtlasMissing: () => void
}>

function JokerPlane({ displayName, onAtlasOk, onAtlasMissing }: JokerPlaneProps) {
  const center = useMemo(() => findJokerByDisplayName(displayName), [displayName])
  const grid = useMemo(() => getJokerAtlasGridSize(), [])
  const tiltGroupRef = useRef<THREE.Group>(null)
  const magneticTarget = useRef({ rx: 0, ry: 0, rz: 0, ox: 0, oy: 0 })
  const [map, setMap] = useState<THREE.Texture | null>(null)
  const [hovered, setHovered] = useState(false)

  const loadSerial = useRef(0)
  const idFrac = useMemo(() => stableIdFraction(displayName), [displayName])

  useEffect(() => {
    if (!center) return
    const serial = ++loadSerial.current
    let cancelled = false
    let loadedTex: THREE.Texture | null = null
    const loader = new THREE.TextureLoader()
    loader.load(
      BALATRO_JOKER_ATLAS.publicPath,
      (tex) => {
        if (cancelled || serial !== loadSerial.current) {
          tex.dispose()
          return
        }
        loadedTex = tex
        tex.colorSpace = THREE.SRGBColorSpace
        tex.magFilter = THREE.NearestFilter
        tex.minFilter = THREE.NearestFilter
        const { width: tw, height: th } = getLoadedTexturePixelSize(tex.image)
        // 1× and 2× sheets differ only in pixel size; P_CENTERS {x,y} are grid indices, not pixels.
        const cellW = tw / grid.cols
        const cellH = th / grid.rows
        applyBalatroGridUV(tex, center.pos, {
          cellW,
          cellH,
          textureWidth: tw,
          textureHeight: th,
        })
        setMap(tex)
        onAtlasOk()
      },
      undefined,
      () => {
        if (!cancelled && serial === loadSerial.current) {
          setMap(null)
          onAtlasMissing()
        }
      }
    )
    return () => {
      cancelled = true
      loadedTex?.dispose()
      setMap(null)
    }
  }, [center, displayName, grid.cols, grid.rows, onAtlasMissing, onAtlasOk])

  const applyTiltFromNormalized = (nx: number, ny: number, amtScale: number) => {
    const clampedX = THREE.MathUtils.clamp(nx, -1, 1)
    const clampedY = THREE.MathUtils.clamp(ny, -1, 1)
    magneticTarget.current.ry = -clampedX * MAGNET_MAX_RY * amtScale
    magneticTarget.current.rx = clampedY * MAGNET_MAX_RX * amtScale
    magneticTarget.current.rz = -clampedX * clampedY * MAGNET_TWIST_Z * amtScale
    magneticTarget.current.ox = clampedX * MAGNET_MAX_SHIFT * amtScale
    magneticTarget.current.oy = -clampedY * MAGNET_MAX_SHIFT * 0.65 * amtScale
  }

  const resetMagneticTargets = () => {
    magneticTarget.current.rx = 0
    magneticTarget.current.ry = 0
    magneticTarget.current.rz = 0
    magneticTarget.current.ox = 0
    magneticTarget.current.oy = 0
  }

  const onPointerMove = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    const uv = e.uv
    if (!uv) return
    const nx = (uv.x - 0.5) * 2
    const ny = (uv.y - 0.5) * 2
    const amt = Math.abs(ny + nx - 1) * TILT_FACTOR
    applyTiltFromNormalized(nx, ny, THREE.MathUtils.clamp(amt * 1.15, 0.35, 1.25))
  }

  useFrame((state, dt) => {
    const g = tiltGroupRef.current
    if (!g) return

    if (!hovered) {
      const t = state.clock.elapsedTime
      const tiltAngle = t * (1.56 + ((idFrac / 1.14212) % 1)) + idFrac / 1.35122
      const nu = 0.5 + 0.5 * AMBIENT_TILT * Math.cos(tiltAngle)
      const nv = 0.5 + 0.5 * AMBIENT_TILT * Math.sin(tiltAngle)
      const nx = (nu - 0.5) * 2
      const ny = (nv - 0.5) * 2
      const amt = AMBIENT_TILT * (0.5 + Math.cos(tiltAngle)) * TILT_FACTOR
      applyTiltFromNormalized(nx, ny, THREE.MathUtils.clamp(amt * 2.2, 0.2, 1))
    }

    const tr = magneticTarget.current
    const rate = hovered ? TILT_LERP_IN : TILT_LERP_OUT
    const a = 1 - Math.exp(-rate * dt)
    g.rotation.x = THREE.MathUtils.lerp(g.rotation.x, tr.rx, a)
    g.rotation.y = THREE.MathUtils.lerp(g.rotation.y, tr.ry, a)
    g.rotation.z = THREE.MathUtils.lerp(g.rotation.z, tr.rz, a)
    g.position.x = THREE.MathUtils.lerp(g.position.x, tr.ox, a)
    g.position.y = THREE.MathUtils.lerp(g.position.y, tr.oy, a)
  })

  if (!center || !map) return null

  const { width: tw, height: th } = getLoadedTexturePixelSize(map.image)
  const cellW = tw / grid.cols
  const cellH = th / grid.rows
  const aspect = cellW / cellH
  const h = 2.4
  const w = aspect * h

  return (
    <group ref={tiltGroupRef}>
      <mesh
        onPointerMove={onPointerMove}
        onPointerEnter={(e) => {
          e.stopPropagation()
          setHovered(true)
          document.body.style.cursor = 'pointer'
        }}
        onPointerLeave={(e) => {
          e.stopPropagation()
          setHovered(false)
          resetMagneticTargets()
          document.body.style.cursor = 'auto'
        }}
      >
        <planeGeometry args={[w, h]} />
        <meshBasicMaterial map={map} transparent toneMapped={false} />
      </mesh>
    </group>
  )
}

type Props = Readonly<{ displayName: string }>

/**
 * Official Balatro `Jokers.png` in Three.js — UVs from `src/data/balatro-jokers.json`
 * (extracted from `src/assets/Balatro2/game.txt` P_CENTERS).
 */
export function BalatroJokerThreePreview({ displayName }: Props) {
  const center = useMemo(() => findJokerByDisplayName(displayName), [displayName])
  const grid = useMemo(() => getJokerAtlasGridSize(), [])
  const [atlasMissing, setAtlasMissing] = useState(false)

  const onOk = useMemo(
    () => () => {
      setAtlasMissing(false)
    },
    []
  )
  const onMissing = useMemo(
    () => () => {
      setAtlasMissing(true)
    },
    []
  )

  if (!center) {
    return (
      <div style={{ color: '#555', fontSize: 11, padding: 8 }}>
        No <code style={{ color: '#777' }}>P_CENTERS</code> match for “{displayName}”. Align the row
        name with the in-game joker name or add an alias in{' '}
        <code style={{ color: '#777' }}>spriteAtlas/jokerRegistry.ts</code>.
      </div>
    )
  }

  return (
    <div>
      <div
        style={{
          width: '100%',
          height: 300,
          padding: '14px 20px',
          boxSizing: 'border-box',
          borderRadius: 6,
          overflow: 'visible',
          background: '#0a0a0a',
        }}
      >
        <Canvas
          camera={{ position: [0, 0, 3.2], fov: 45 }}
          gl={{ alpha: true, antialias: true }}
          style={{ width: '100%', height: 272, touchAction: 'none', display: 'block' }}
        >
          <ambientLight intensity={0.6} />
          <Suspense fallback={null}>
            <JokerPlane displayName={displayName} onAtlasOk={onOk} onAtlasMissing={onMissing} />
          </Suspense>
        </Canvas>
      </div>
      {atlasMissing && (
        <div style={{ color: '#886644', fontSize: 11, lineHeight: 1.45, marginTop: 8 }}>
          Add <strong>Jokers.png</strong> to{' '}
          <code style={{ color: '#aaa' }}>public/images/Jokers.png</code> (copy{' '}
          <code style={{ color: '#aaa' }}>1x</code> or <code style={{ color: '#aaa' }}>2x</code>{' '}
          from Balatro <code style={{ color: '#aaa' }}>resources/textures/</code> — UVs scale from
          image size). Key <code style={{ color: '#aaa' }}>{center.key}</code> → grid (
          {center.pos.x},{center.pos.y}) in{' '}
          <code style={{ color: '#aaa' }}>balatro-jokers.json</code>.
        </div>
      )}
      <p style={{ fontSize: 10, color: '#444', marginTop: 8, marginBottom: 0 }}>
        UVs: <code style={{ color: '#555' }}>balatro-jokers.json</code> (x,y cells) + sheet size →
        pixel slice · grid {grid.cols}×{grid.rows} · source{' '}
        <code style={{ color: '#555' }}>Balatro2/game.txt</code> P_CENTERS · magnetic tilt / ambient
        orbit approximates <code style={{ color: '#555' }}>card.txt</code> <code>tilt_var</code> +{' '}
        <code style={{ color: '#555' }}>sprite.txt</code> <code>mouse_screen_pos</code>
      </p>
    </div>
  )
}

'use client'

import { useRef, useState, useMemo, memo } from 'react'
import { Canvas, useFrame, type ThreeEvent } from '@react-three/fiber'
import { useSpring, animated } from '@react-spring/three'
import { RoundedBox, Text } from '@react-three/drei'
import * as THREE from 'three'
import { resolveJamlAssetUrl } from '../assets.js'

// The canonical Jimbo button is R3F + @react-spring/three. There is no flat 2D
// DOM fallback. R3F works inside MCP App iframes, mobile browsers, and desktop —
// every consumer surface for this library. See CLAUDE.md "Component placement
// convention — HARD RULE".

export type JimboTone = 'orange' | 'red' | 'blue' | 'green' | 'tarot' | 'planet' | 'spectral' | 'grey'

const TONE_FACE: Record<JimboTone, string> = {
  orange: '#ff9800',
  red: '#fe5148',
  blue: '#0093ff',
  green: '#429f79',
  grey: '#a8bcbf',
  tarot: '#9e74ce',
  planet: '#00a7ca',
  spectral: '#2e76fd',
}
const TONE_SHADOW: Record<JimboTone, string> = {
  orange: '#a05b00',
  red: '#a02721',
  blue: '#0057a1',
  green: '#215f46',
  grey: '#404c4e',
  tarot: '#5e437e',
  planet: '#00657c',
  spectral: '#14449e',
}

const MAGNET = {
  MAX_TILT_X: 0.18,
  MAX_TILT_Y: 0.22,
  MAX_SHIFT: 0.02,
  TWIST_Z: 0.05,
  LERP_IN: 22,
  LERP_OUT: 12,
} as const

const SIZE_TO_DIMS = {
  xs: { w: 1.4, h: 0.42, depth: 0.12, fontSize: 0.22 },
  sm: { w: 1.8, h: 0.52, depth: 0.14, fontSize: 0.26 },
  md: { w: 2.4, h: 0.66, depth: 0.16, fontSize: 0.3 },
  lg: { w: 3.0, h: 0.82, depth: 0.18, fontSize: 0.36 },
} as const

export type JimboButtonSize = keyof typeof SIZE_TO_DIMS

export interface JimboButtonProps {
  tone?: JimboTone
  size?: JimboButtonSize
  disabled?: boolean
  fullWidth?: boolean
  onClick?: () => void
  /** Button label. Non-string children are coerced via `String()`. For
   *  buttons that need icons + multi-line text, use a different primitive
   *  (e.g. JimboInfoCard with an onClick), not JimboButton. */
  children: React.ReactNode
  /** Outer wrapper width in pixels. Default sized to the button. */
  canvasWidth?: number
  /** Outer wrapper height in pixels. Default sized to the button. */
  canvasHeight?: number
  /** Inline style applied to the outer wrapper div (not the 3D mesh). */
  style?: React.CSSProperties
  className?: string
}

/**
 * The Jimbo button. R3F-native: each button is its own Canvas with
 * @react-spring/three driving hover lift + click press, magnetic tilt on
 * pointer move, and sub-pixel idle sway at rest. There is no DOM fallback —
 * R3F is the only implementation.
 */
export const JimboButton = memo(function JimboButton({
  tone = 'orange',
  size = 'md',
  disabled = false,
  fullWidth = false,
  onClick,
  children,
  canvasWidth,
  canvasHeight,
  style: styleProp,
  className = '',
}: JimboButtonProps) {
  const dims = SIZE_TO_DIMS[size]
  // Pixel sizing — 1 R3F unit ≈ 60 CSS px feels right for the 320×568 surface.
  const px = 60
  const w = fullWidth ? undefined : canvasWidth ?? Math.round(dims.w * px + 40)
  const h = canvasHeight ?? Math.round(dims.h * px + 40)

  const style: React.CSSProperties = {
    ...(fullWidth ? { width: '100%', height: h } : { width: w, height: h }),
    ...styleProp,
  }

  // Label coercion — drei <Text> renders strings only. ReactNode children get
  // stringified (lossy for nested markup; see prop docstring above).
  const label = typeof children === 'string' ? children : String(children ?? '')

  return (
    <div className={className} style={style}>
      <Canvas
        orthographic
        camera={{ position: [0, 0, 5], zoom: px }}
        gl={{ alpha: true, antialias: true }}
        dpr={[1, 2]}
      >
        <ambientLight intensity={0.9} />
        <directionalLight position={[2, 3, 4]} intensity={0.7} />
        <ButtonMesh tone={tone} size={size} disabled={disabled} onClick={onClick}>
          {label}
        </ButtonMesh>
      </Canvas>
    </div>
  )
})

function ButtonMesh({
  tone,
  size,
  disabled,
  onClick,
  children,
}: Required<Pick<JimboButtonProps, 'tone' | 'size'>> &
  Pick<JimboButtonProps, 'disabled' | 'onClick'> & { children: string }) {
  const tiltRef = useRef<THREE.Group>(null)
  const target = useRef({ rx: 0, ry: 0, rz: 0, ox: 0, oy: 0 })
  const [hovered, setHovered] = useState(false)
  const [pressed, setPressed] = useState(false)

  const dims = SIZE_TO_DIMS[size]
  const face = useMemo(() => new THREE.Color(disabled ? '#5a6669' : TONE_FACE[tone]), [tone, disabled])
  const shadow = useMemo(() => new THREE.Color(disabled ? '#2a3033' : TONE_SHADOW[tone]), [tone, disabled])

  const { posY, scale } = useSpring({
    posY: pressed ? -0.04 : hovered ? 0.06 : 0,
    scale: pressed ? 0.96 : hovered ? 1.05 : 1,
    config: { tension: 380, friction: 18 },
  })

  // Idle sub-pixel sway — the "almost imperceptible" Balatro vibe at rest.
  // Sin waves at irrational ratios so the loop never visibly tiles.
  // Suppressed during hover so the magnet tilt reads cleanly.
  useFrame((state, dt) => {
    const g = tiltRef.current
    if (!g) return
    const t = target.current
    const time = state.clock.elapsedTime
    const idleBob = hovered ? 0 : Math.sin(time * 0.9) * 0.006
    const idleSwayX = hovered ? 0 : Math.sin(time * 0.73 + 1.3) * 0.012
    const idleSwayY = hovered ? 0 : Math.cos(time * 0.61 + 0.5) * 0.010
    const rate = hovered ? MAGNET.LERP_IN : MAGNET.LERP_OUT
    const a = 1 - Math.exp(-rate * dt)
    g.rotation.x = THREE.MathUtils.lerp(g.rotation.x, t.rx + idleSwayX, a)
    g.rotation.y = THREE.MathUtils.lerp(g.rotation.y, t.ry + idleSwayY, a)
    g.rotation.z = THREE.MathUtils.lerp(g.rotation.z, t.rz, a)
    g.position.x = THREE.MathUtils.lerp(g.position.x, t.ox, a)
    g.position.y = THREE.MathUtils.lerp(g.position.y, t.oy + idleBob, a)
  })

  const onMove = (e: ThreeEvent<PointerEvent>) => {
    e.stopPropagation()
    const uv = e.uv
    if (!uv) return
    const nx = THREE.MathUtils.clamp((uv.x - 0.5) * 2, -1, 1)
    const ny = THREE.MathUtils.clamp((uv.y - 0.5) * 2, -1, 1)
    target.current.ry = -nx * MAGNET.MAX_TILT_Y
    target.current.rx = ny * MAGNET.MAX_TILT_X
    target.current.rz = -nx * ny * MAGNET.TWIST_Z
    target.current.ox = nx * MAGNET.MAX_SHIFT
    target.current.oy = -ny * MAGNET.MAX_SHIFT * 0.65
  }

  const reset = () => {
    target.current = { rx: 0, ry: 0, rz: 0, ox: 0, oy: 0 }
  }

  return (
    <animated.group position-y={posY} scale={scale}>
      <mesh
        visible={false}
        onClick={(e) => {
          e.stopPropagation()
          if (!disabled) onClick?.()
        }}
        onPointerDown={(e) => {
          e.stopPropagation()
          if (!disabled) setPressed(true)
        }}
        onPointerUp={(e) => {
          e.stopPropagation()
          setPressed(false)
        }}
        onPointerMove={onMove}
        onPointerEnter={(e) => {
          e.stopPropagation()
          setHovered(true)
          if (!disabled) document.body.style.cursor = 'pointer'
        }}
        onPointerLeave={(e) => {
          e.stopPropagation()
          setHovered(false)
          setPressed(false)
          reset()
          document.body.style.cursor = 'auto'
        }}
      >
        <boxGeometry args={[dims.w, dims.h, dims.depth * 2]} />
        <meshBasicMaterial />
      </mesh>

      <group ref={tiltRef}>
        <RoundedBox
          args={[dims.w, dims.h, dims.depth * 0.4]}
          radius={0.12}
          smoothness={4}
          position={[0, -0.08, -dims.depth * 0.3]}
        >
          <meshStandardMaterial color={shadow} roughness={0.9} metalness={0} />
        </RoundedBox>

        <RoundedBox args={[dims.w, dims.h, dims.depth]} radius={0.12} smoothness={4}>
          <meshStandardMaterial color={face} roughness={0.7} metalness={0.05} />
        </RoundedBox>

        <Text
          position={[0, 0, dims.depth / 2 + 0.001]}
          fontSize={dims.fontSize}
          font={resolveJamlAssetUrl('font')}
          color="#ffffff"
          anchorX="center"
          anchorY="middle"
          maxWidth={dims.w * 0.9}
          textAlign="center"
        >
          {children}
        </Text>
      </group>
    </animated.group>
  )
}

/**
 * Canonical "back" button. Orange, full-width, label "Back". Used by
 * `JimboPanel` and `JimboModal` when `onBack` / `showBack` is set.
 */
export function JimboBackButton({ onClick, size = 'sm' }: { onClick?: () => void; size?: JimboButtonSize }) {
  return <JimboButton tone="orange" size={size} fullWidth onClick={onClick}>Back</JimboButton>
}

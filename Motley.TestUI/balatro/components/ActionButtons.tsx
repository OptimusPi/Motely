import { memo, useState, useRef } from 'react'
import { Text, RoundedBox } from '@react-three/drei'
import { useFrame } from '@react-three/fiber'
import * as THREE from 'three'
import { useBalatroStore } from '../store/balatroStore'

/** Reused across all Button3D instances to avoid per-frame allocation. */
const _targetScale = new THREE.Vector3()

interface Button3DProps {
  position: [number, number, number]
  label: string
  color: string
  hoverColor: string
  onClick: () => void
  disabled?: boolean
  sublabel?: string
}

const Button3D = memo(function Button3D({
  position,
  label,
  color,
  hoverColor,
  onClick,
  disabled = false,
  sublabel,
}: Button3DProps) {
  const [hovered, setHovered] = useState(false)
  const meshRef = useRef<THREE.Mesh>(null)

  useFrame(() => {
    if (meshRef.current) {
      const s = hovered && !disabled ? 1.05 : 1
      meshRef.current.scale.lerp(_targetScale.set(s, s, s), 0.1)
    }
  })

  return (
    <group position={position}>
      <RoundedBox
        ref={meshRef}
        args={[1.2, 0.5, 0.1]}
        radius={0.08}
        smoothness={4}
        onClick={disabled ? undefined : onClick}
        onPointerEnter={() => {
          if (!disabled) {
            setHovered(true)
            document.body.style.cursor = 'pointer'
          }
        }}
        onPointerLeave={() => {
          setHovered(false)
          document.body.style.cursor = 'auto'
        }}
      >
        <meshStandardMaterial
          color={disabled ? '#333' : hovered ? hoverColor : color}
          metalness={0.3}
          roughness={0.6}
          emissive={hovered && !disabled ? hoverColor : '#000'}
          emissiveIntensity={hovered && !disabled ? 0.2 : 0}
        />
      </RoundedBox>

      <Text
        position={[0, sublabel ? 0.05 : 0, 0.06]}
        fontSize={0.14}
        color={disabled ? '#555' : '#fff'}
        anchorX="center"
        anchorY="middle"
      >
        {label}
      </Text>

      {sublabel && (
        <Text
          position={[0, -0.12, 0.06]}
          fontSize={0.08}
          color={disabled ? '#444' : '#aaa'}
          anchorX="center"
          anchorY="middle"
        >
          {sublabel}
        </Text>
      )}
    </group>
  )
})

interface ActionButtonsProps {
  position?: [number, number, number]
}

export function ActionButtons({ position = [-2, 0, 0] }: ActionButtonsProps) {
  const { playHand, discardSelected, selectedCards, handsRemaining, discardsRemaining, initGame } =
    useBalatroStore()

  const hasSelection = selectedCards.size > 0
  const canPlay = hasSelection && handsRemaining > 0
  const canDiscard = hasSelection && discardsRemaining > 0

  return (
    <group position={position}>
      {/* Play Hand Button */}
      <Button3D
        position={[0, 0.8, 0]}
        label="PLAY HAND"
        sublabel={`${selectedCards.size}/5 cards`}
        color="#27ae60"
        hoverColor="#2ecc71"
        onClick={playHand}
        disabled={!canPlay}
      />

      {/* Discard Button */}
      <Button3D
        position={[0, 0.1, 0]}
        label="DISCARD"
        sublabel={`${discardsRemaining} left`}
        color="#e67e22"
        hoverColor="#f39c12"
        onClick={discardSelected}
        disabled={!canDiscard}
      />

      {/* Sort Hand Buttons */}
      <Button3D
        position={[0, -0.6, 0]}
        label="NEW GAME"
        color="#8e44ad"
        hoverColor="#9b59b6"
        onClick={() => initGame({ newRun: true })}
      />
    </group>
  )
}

export default ActionButtons

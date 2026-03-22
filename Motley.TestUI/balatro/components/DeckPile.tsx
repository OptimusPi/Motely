import { useRef, useMemo } from 'react'
import { useFrame } from '@react-three/fiber'
import { Text } from '@react-three/drei'
import * as THREE from 'three'
import { useBalatroStore } from '../store/balatroStore'

interface DeckPileProps {
  position?: [number, number, number]
}

// Card back texture (reusable)
function useCardBackTexture() {
  return useMemo(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 128
    canvas.height = 176
    const ctx = canvas.getContext('2d')!

    // Dark red background
    ctx.fillStyle = '#1a1a2e'
    ctx.fillRect(0, 0, 128, 176)

    // Border
    ctx.strokeStyle = '#c9a227'
    ctx.lineWidth = 4
    ctx.strokeRect(4, 4, 120, 168)

    // Inner pattern
    ctx.fillStyle = '#16213e'
    for (let y = 12; y < 164; y += 16) {
      for (let x = 12; x < 116; x += 16) {
        ctx.fillRect(x, y, 8, 8)
      }
    }

    // Center diamond
    ctx.fillStyle = '#c9a227'
    ctx.beginPath()
    ctx.moveTo(64, 60)
    ctx.lineTo(84, 88)
    ctx.lineTo(64, 116)
    ctx.lineTo(44, 88)
    ctx.closePath()
    ctx.fill()

    const tex = new THREE.CanvasTexture(canvas)
    tex.magFilter = THREE.NearestFilter
    tex.minFilter = THREE.NearestFilter
    return tex
  }, [])
}

export function DeckPile({ position = [2, 0, 0] }: DeckPileProps) {
  const groupRef = useRef<THREE.Group>(null)
  const { deck, discardPile } = useBalatroStore()
  const backTexture = useCardBackTexture()

  // Visualize stack height based on card count
  const stackHeight = Math.min(deck.length, 52)
  const discardHeight = Math.min(discardPile.length, 52)

  useFrame((state) => {
    if (groupRef.current) {
      groupRef.current.position.y = position[1] + Math.sin(state.clock.elapsedTime * 0.7) * 0.01
    }
  })

  return (
    <group ref={groupRef} position={position}>
      {/* Deck stack */}
      <group position={[0, 0, 0]}>
        {/* Stack of card backs */}
        {Array.from({ length: Math.min(10, Math.ceil(stackHeight / 5)) }).map((_, i) => (
          <mesh
            key={`deck-${i}`}
            position={[0, i * 0.015, 0]}
            rotation={[-Math.PI / 2, 0, 0]}
            castShadow
          >
            <boxGeometry args={[0.7, 0.95, 0.02]} />
            <meshStandardMaterial map={backTexture} metalness={0.1} roughness={0.8} />
          </mesh>
        ))}

        {/* Deck count label */}
        <Text
          position={[0, 0.6, 0]}
          fontSize={0.15}
          color="#888"
          anchorX="center"
          anchorY="middle"
          rotation={[-Math.PI / 4, 0, 0]}
        >
          {`DECK`}
        </Text>
        <Text
          position={[0, 0.4, 0.1]}
          fontSize={0.2}
          color="#f1c40f"
          anchorX="center"
          anchorY="middle"
          rotation={[-Math.PI / 4, 0, 0]}
        >
          {deck.length}
        </Text>
      </group>

      {/* Discard pile */}
      <group position={[1.2, 0, 0]}>
        {discardHeight > 0 && (
          <>
            {Array.from({ length: Math.min(5, Math.ceil(discardHeight / 10)) }).map((_, i) => (
              <mesh
                key={`discard-${i}`}
                position={[Math.sin(i) * 0.02, i * 0.01, Math.cos(i) * 0.02]}
                rotation={[-Math.PI / 2, 0, i * 0.1]}
                castShadow
              >
                <boxGeometry args={[0.7, 0.95, 0.02]} />
                <meshStandardMaterial color="#555" metalness={0.1} roughness={0.9} />
              </mesh>
            ))}
          </>
        )}

        <Text
          position={[0, 0.6, 0]}
          fontSize={0.12}
          color="#666"
          anchorX="center"
          anchorY="middle"
          rotation={[-Math.PI / 4, 0, 0]}
        >
          {`DISCARD`}
        </Text>
        <Text
          position={[0, 0.4, 0.1]}
          fontSize={0.18}
          color="#888"
          anchorX="center"
          anchorY="middle"
          rotation={[-Math.PI / 4, 0, 0]}
        >
          {discardPile.length}
        </Text>
      </group>
    </group>
  )
}

export default DeckPile

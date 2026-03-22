import { useMemo, useRef } from 'react'
import { useFrame } from '@react-three/fiber'
import * as THREE from 'three'
import { Card3D } from './Card3D'
import { useBalatroStore } from '../store/balatroStore'

interface HandDisplayProps {
  position?: [number, number, number]
}

export function HandDisplay({ position = [0, 0, 0] }: HandDisplayProps) {
  const floatRef = useRef<THREE.Group>(null)
  const { hand, selectedCards, toggleCardSelection } = useBalatroStore()

  // Calculate card positions in a fan layout
  const cardPositions = useMemo(() => {
    const cardCount = hand.length
    if (cardCount === 0) return []

    const fanAngle = Math.min(40, cardCount * 5) // degrees
    const fanRadius = 3
    const angleStep = (fanAngle * 2) / Math.max(cardCount - 1, 1)
    const startAngle = -fanAngle

    return hand.map((card, index) => {
      const angle = cardCount === 1 ? 0 : startAngle + angleStep * index
      const radians = (angle * Math.PI) / 180

      // Arc positions
      const x = Math.sin(radians) * fanRadius * 0.4
      const y = -Math.cos(radians) * fanRadius * 0.05 + (selectedCards.has(card.id) ? 0.3 : 0)
      const z = index * 0.02 // Slight z offset for layering

      return {
        card,
        position: [x, y, z] as [number, number, number],
        rotation: [0, 0, -radians * 0.3] as [number, number, number],
      }
    })
  }, [hand, selectedCards])

  // Subtle floating animation (Y is already on the parent group)
  useFrame((state) => {
    if (!floatRef.current) return
    floatRef.current.position.y = Math.sin(state.clock.elapsedTime * 0.5) * 0.02
  })

  // World-space fan (no Billboard): drei's Billboard was forcing the whole hand to camera-face,
  // which fights per-card tilt/arc and reads as “broken” cards on the table.
  return (
    <group position={position}>
      <group ref={floatRef}>
        {cardPositions.map(({ card, position: cardPos, rotation }, index) => (
          <Card3D
            key={card.id}
            card={card}
            position={cardPos}
            rotation={rotation}
            selected={selectedCards.has(card.id)}
            highlighted={selectedCards.has(card.id)}
            onClick={() => toggleCardSelection(card.id)}
            index={index}
          />
        ))}
      </group>
    </group>
  )
}

export default HandDisplay

'use client'

import { Canvas } from '@react-three/fiber'
import { PerspectiveCamera, OrbitControls, Environment } from '@react-three/drei'
import { Suspense, useEffect, useState } from 'react'
import Card3D from '../components/Card3D'
import { createCard, resetCardIdCounter } from '../deck'
import type { Card } from '../types'

interface CardSceneProps {
  seed: string
}

function generateCardsFromSeed(seed: string, count: number = 5): Card[] {
  // Simple deterministic card generation from seed
  const hash = seed.split('').reduce((h, c) => h + c.charCodeAt(0), 0)
  const suits: Array<'hearts' | 'clubs' | 'diamonds' | 'spades'> = [
    'hearts',
    'clubs',
    'diamonds',
    'spades',
  ]
  const ranks: Array<'2' | '3' | '4' | '5' | '6' | '7' | '8' | '9' | '10' | 'J' | 'Q' | 'K' | 'A'> =
    ['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A']

  resetCardIdCounter()
  const cards: Card[] = []
  for (let i = 0; i < count; i++) {
    const suitIdx = (hash + i * 13) % suits.length
    const rankIdx = (hash + i * 7) % ranks.length
    cards.push(createCard(suits[suitIdx], ranks[rankIdx]))
  }
  return cards
}

function CardSceneContent({ seed }: CardSceneProps) {
  const [cards, setCards] = useState<Card[]>([])

  useEffect(() => {
    setCards(generateCardsFromSeed(seed, 5))
  }, [seed])

  if (cards.length === 0) {
    return null
  }

  // Layout cards in arc around center
  const positions = cards.map((_, i) => {
    const angle = (i / cards.length) * Math.PI - Math.PI / 2
    const radius = 2
    return [Math.cos(angle) * radius, 0, Math.sin(angle) * radius] as [number, number, number]
  })

  return (
    <>
      <PerspectiveCamera makeDefault position={[0, 1.5, 4]} fov={60} />
      <OrbitControls enableDamping dampingFactor={0.05} enableZoom autoRotate autoRotateSpeed={2} />

      <ambientLight intensity={0.6} />
      <directionalLight position={[5, 8, 5]} intensity={1} castShadow />
      <pointLight position={[0, 2, 0]} intensity={0.5} />

      <Environment preset="studio" />

      {/* Card deck display */}
      {cards.map((card, i) => (
        <Suspense key={i} fallback={null}>
          <Card3D card={card} position={positions[i]} rotation={[0, 0, 0]} index={i} />
        </Suspense>
      ))}

      {/* Floor plane */}
      <mesh receiveShadow position={[0, -0.5, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <planeGeometry args={[20, 20]} />
        <meshStandardMaterial color="#1a1a2e" roughness={0.8} />
      </mesh>
    </>
  )
}

export function CardScene3D({ seed }: CardSceneProps) {
  return (
    <Canvas shadows style={{ width: '100%', height: '100%' }} gl={{ antialias: true, alpha: true }}>
      <Suspense fallback={null}>
        <CardSceneContent seed={seed} />
      </Suspense>
    </Canvas>
  )
}

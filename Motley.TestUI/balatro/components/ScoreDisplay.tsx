import { useRef, useMemo, useState, useEffect } from 'react'
import { Text, RoundedBox } from '@react-three/drei'
import { useFrame } from '@react-three/fiber'
import * as THREE from 'three'
import { useBalatroStore } from '../store/balatroStore'
import { BASE_HANDS } from '../types'

interface ScoreDisplayProps {
  position?: [number, number, number]
}

// Smooth number interpolation hook
function useAnimatedNumber(target: number, speed = 0.1) {
  const [value, setValue] = useState(target)

  useEffect(() => {
    let animationFrame: number
    const animate = () => {
      setValue((current) => {
        const diff = target - current
        if (Math.abs(diff) < 1) return target
        return current + diff * speed
      })
      animationFrame = requestAnimationFrame(animate)
    }
    animate()
    return () => cancelAnimationFrame(animationFrame)
  }, [target, speed])

  return value
}

export function ScoreDisplay({ position = [0, 2, 0] }: ScoreDisplayProps) {
  const groupRef = useRef<THREE.Group>(null)
  const {
    lastAnalysis,
    currentScore,
    targetScore,
    handsRemaining,
    discardsRemaining,
    selectedCards,
    showAnalysis,
  } = useBalatroStore()

  // Animated values
  const displayScore = useAnimatedNumber(currentScore, 0.15)
  const animatedChips = useAnimatedNumber(lastAnalysis?.totalChips ?? 0, 0.2)
  const animatedMult = useAnimatedNumber(lastAnalysis?.totalMult ?? 0, 0.2)

  // Progress bar percentage
  const progress = Math.min(currentScore / targetScore, 1)

  // Glow effect based on hand quality
  const glowColor = useMemo(() => {
    if (!lastAnalysis) return '#444'
    const handRank = Object.keys(BASE_HANDS).indexOf(lastAnalysis.hand.type)
    if (handRank >= 8) return '#f1c40f' // Royal flush tier
    if (handRank >= 6) return '#e74c3c' // Four of a kind+
    if (handRank >= 4) return '#9b59b6' // Full house+
    if (handRank >= 2) return '#3498db' // Three of a kind+
    return '#2ecc71'
  }, [lastAnalysis])

  useFrame((state) => {
    if (groupRef.current) {
      groupRef.current.rotation.y = Math.sin(state.clock.elapsedTime * 0.3) * 0.02
    }
  })

  return (
    <group ref={groupRef} position={position}>
      {/* Main score panel */}
      <RoundedBox args={[3, 1.8, 0.1]} radius={0.1} smoothness={4} position={[0, 0, 0]}>
        <meshStandardMaterial color="#1a1a2e" metalness={0.3} roughness={0.7} />
      </RoundedBox>

      {/* Current Score */}
      <Text
        position={[0, 0.55, 0.06]}
        fontSize={0.15}
        color="#888"
        anchorX="center"
        anchorY="middle"
      >
        SCORE
      </Text>

      <Text
        position={[0, 0.25, 0.06]}
        fontSize={0.35}
        color="#f1c40f"
        anchorX="center"
        anchorY="middle"
      >
        {Math.floor(displayScore).toLocaleString()}
      </Text>

      {/* Target score */}
      <Text
        position={[0, -0.05, 0.06]}
        fontSize={0.12}
        color="#666"
        anchorX="center"
        anchorY="middle"
      >
        {`/ ${targetScore.toLocaleString()}`}
      </Text>

      {/* Progress bar background */}
      <mesh position={[0, -0.25, 0.06]}>
        <planeGeometry args={[2.5, 0.1]} />
        <meshBasicMaterial color="#333" />
      </mesh>

      {/* Progress bar fill */}
      <mesh
        position={[-1.25 + progress * 1.25, -0.25, 0.07]}
        scale={[Math.max(progress, 0.001), 1, 1]}
      >
        <planeGeometry args={[2.5, 0.08]} />
        <meshBasicMaterial color={progress >= 1 ? '#2ecc71' : '#3498db'} />
      </mesh>

      {/* Hands and Discards remaining */}
      <group position={[-0.9, -0.55, 0.06]}>
        <Text fontSize={0.1} color="#e74c3c" anchorX="left" anchorY="middle">
          {`HANDS: ${handsRemaining}`}
        </Text>
      </group>

      <group position={[0.3, -0.55, 0.06]}>
        <Text fontSize={0.1} color="#3498db" anchorX="left" anchorY="middle">
          {`DISCARDS: ${discardsRemaining}`}
        </Text>
      </group>

      {/* Hand Analysis Panel (shows when cards selected) */}
      {showAnalysis && lastAnalysis && selectedCards.size > 0 && (
        <group position={[0, -1.5, 0]}>
          <RoundedBox args={[3, 1.2, 0.1]} radius={0.1} smoothness={4}>
            <meshStandardMaterial color="#16213e" metalness={0.2} roughness={0.8} />
          </RoundedBox>

          {/* Glow border */}
          <mesh position={[0, 0, -0.01]}>
            <planeGeometry args={[3.1, 1.3]} />
            <meshBasicMaterial color={glowColor} transparent opacity={0.3} />
          </mesh>

          {/* Hand name */}
          <Text
            position={[0, 0.35, 0.06]}
            fontSize={0.2}
            color={glowColor}
            anchorX="center"
            anchorY="middle"
          >
            {lastAnalysis.hand.name.toUpperCase()}
          </Text>

          {/* Level indicator */}
          <Text
            position={[0, 0.12, 0.06]}
            fontSize={0.1}
            color="#888"
            anchorX="center"
            anchorY="middle"
          >
            {`Level ${lastAnalysis.hand.level}`}
          </Text>

          {/* Chips x Mult calculation */}
          <group position={[0, -0.15, 0.06]}>
            <Text
              position={[-0.6, 0, 0]}
              fontSize={0.22}
              color="#3498db"
              anchorX="right"
              anchorY="middle"
            >
              {Math.floor(animatedChips).toString()}
            </Text>

            <Text
              position={[-0.4, 0, 0]}
              fontSize={0.15}
              color="#888"
              anchorX="center"
              anchorY="middle"
            >
              chips
            </Text>

            <Text
              position={[0, 0, 0]}
              fontSize={0.25}
              color="#f1c40f"
              anchorX="center"
              anchorY="middle"
            >
              x
            </Text>

            <Text
              position={[0.5, 0, 0]}
              fontSize={0.22}
              color="#e74c3c"
              anchorX="left"
              anchorY="middle"
            >
              {animatedMult.toFixed(1)}
            </Text>

            <Text
              position={[1, 0, 0]}
              fontSize={0.15}
              color="#888"
              anchorX="center"
              anchorY="middle"
            >
              mult
            </Text>
          </group>

          {/* Final score preview */}
          <Text
            position={[0, -0.45, 0.06]}
            fontSize={0.18}
            color="#f1c40f"
            anchorX="center"
            anchorY="middle"
          >
            {`= ${lastAnalysis.finalScore.toLocaleString()}`}
          </Text>
        </group>
      )}
    </group>
  )
}

export default ScoreDisplay

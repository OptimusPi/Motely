import { useEffect, Suspense } from 'react'
import { OrbitControls, Environment, Stars, Preload, Html } from '@react-three/drei'
import { EffectComposer, Bloom, Vignette } from '@react-three/postprocessing'
import { BlendFunction } from 'postprocessing'

import { HandDisplay } from './HandDisplay'
import { ScoreDisplay } from './ScoreDisplay'
import { DeckPile } from './DeckPile'
import { ActionButtons } from './ActionButtons'
import { PokerTable } from './PokerTable'
import { useBalatroStore } from '../store/balatroStore'
import { R3FErrorBoundary } from '../../components/R3FErrorBoundary'

// Loading fallback with spinner
function LoadingFallback() {
  return (
    <Html center>
      <div
        style={{
          color: '#f1c40f',
          fontSize: '1.5rem',
          fontFamily: 'Inter, sans-serif',
          textAlign: 'center',
        }}
      >
        <div style={{ animation: 'pulse 1.5s ease-in-out infinite' }}>Loading...</div>
      </div>
    </Html>
  )
}

export function BalatroScene() {
  const { initGame, hand } = useBalatroStore()

  // Initialize game on mount
  useEffect(() => {
    if (hand.length === 0) {
      initGame()
    }
  }, [initGame, hand.length])

  return (
    <>
      {/* Lighting */}
      <ambientLight intensity={0.4} />
      <directionalLight
        position={[5, 10, 5]}
        intensity={1.5}
        castShadow
        shadow-mapSize={[2048, 2048]}
        shadow-camera-far={50}
        shadow-camera-left={-10}
        shadow-camera-right={10}
        shadow-camera-top={10}
        shadow-camera-bottom={-10}
      />
      <pointLight position={[-5, 5, -5]} intensity={0.5} color="#9b59b6" />
      <pointLight position={[5, 5, -5]} intensity={0.5} color="#3498db" />

      {/* Environment */}
      <Stars radius={100} depth={50} count={5000} factor={4} saturation={0} fade speed={1} />
      <Environment preset="night" />
      <fog attach="fog" args={['#0a0a1a', 8, 25]} />

      {/* Background */}
      <color attach="background" args={['#0a0a1a']} />

      {/* Poker Table - outside Suspense since it doesn't need textures */}
      <PokerTable position={[0, -0.5, 0]} />

      {/* Score Display - outside Suspense */}
      <ScoreDisplay position={[0, 2.5, -1]} />

      {/* Deck and Discard - outside Suspense */}
      <DeckPile position={[3, 0, 1]} />

      {/* Action Buttons - outside Suspense */}
      <ActionButtons position={[-3, 0.5, 1]} />

      {/* Hand Display - needs Suspense for textures */}
      <R3FErrorBoundary>
        <Suspense fallback={<LoadingFallback />}>
          <HandDisplay position={[0, 0.2, 2]} />
          <Preload all />
        </Suspense>
      </R3FErrorBoundary>

      {/* Camera Controls */}
      <OrbitControls
        makeDefault
        enablePan={false}
        minDistance={4}
        maxDistance={12}
        minPolarAngle={Math.PI / 6}
        maxPolarAngle={Math.PI / 2.2}
        target={[0, 0.5, 0]}
        enableDamping
        dampingFactor={0.05}
      />

      {/* Post-processing: chromatic aberration removed — it was splitting R/G/B on pixel edges (looks like “wrong color space”). */}
      <EffectComposer>
        <Bloom intensity={0.22} luminanceThreshold={0.92} luminanceSmoothing={0.35} mipmapBlur />
        <Vignette offset={0.35} darkness={0.5} eskil={false} blendFunction={BlendFunction.NORMAL} />
      </EffectComposer>
    </>
  )
}

export default BalatroScene

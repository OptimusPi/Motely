'use client'

import { Canvas } from '@react-three/fiber'
import { Leva } from 'leva'
import { Fragment, lazy, Suspense, useEffect, useState } from 'react'
import * as THREE from 'three'
import { BalatroScene } from './balatro'
import { BalatroFanSiteAttributionFooter } from './components/BalatroFanSiteAttributionFooter'
import { SeedControls } from './components/SeedControls'

const AdventureApp = lazy(() => import('./routes/AdventureApp'))
const JokerClassifier = lazy(() =>
  import('./joker-classifier').then((m) => ({ default: m.JokerClassifier }))
)
const ShopStreamAnalyzer = lazy(() =>
  import('./motely-ui/ShopStreamAnalyzer').then((m) => ({ default: m.ShopStreamAnalyzer }))
)
const MotelyShopStreamAnalyzer = lazy(() =>
  import('./motely-ui/MotelyShopStreamAnalyzer').then((m) => ({
    default: m.MotelyShopStreamAnalyzer,
  }))
)

function ToolLoading() {
  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'grid',
        placeItems: 'center',
        background: '#0d0d12',
        color: '#888',
        fontFamily: 'ui-monospace, monospace',
        fontSize: 13,
      }}
    >
      Loading…
    </div>
  )
}

type ToolId = '3d' | 'jokers' | 'adventure' | 'shop' | 'motely'

function readToolFromUrl(): ToolId {
  const t = new URLSearchParams(window.location.search).get('tool')
  if (t === 'jokers') return 'jokers'
  if (t === 'adventure') return 'adventure'
  if (t === 'shop') return 'shop'
  if (t === 'motely') return 'motely'
  return '3d'
}

export default function App() {
  const [tool, setTool] = useState<ToolId>(readToolFromUrl)

  useEffect(() => {
    const u = new URL(window.location.href)
    if (tool === 'jokers') u.searchParams.set('tool', 'jokers')
    else if (tool === 'adventure') u.searchParams.set('tool', 'adventure')
    else if (tool === 'shop') u.searchParams.set('tool', 'shop')
    else if (tool === 'motely') u.searchParams.set('tool', 'motely')
    else u.searchParams.delete('tool')
    window.history.replaceState({}, '', `${u.pathname}${u.search}`)
  }, [tool])

  if (tool === 'shop') {
    return (
      <Fragment>
        <Suspense fallback={<ToolLoading />}>
          <ShopStreamAnalyzer onBack={() => setTool('3d')} />
        </Suspense>
        <BalatroFanSiteAttributionFooter />
      </Fragment>
    )
  }

  if (tool === 'motely') {
    return (
      <Fragment>
        <Suspense fallback={<ToolLoading />}>
          <MotelyShopStreamAnalyzer onBack={() => setTool('3d')} />
        </Suspense>
        <BalatroFanSiteAttributionFooter />
      </Fragment>
    )
  }

  if (tool === 'jokers') {
    return (
      <Fragment>
        <Suspense fallback={<ToolLoading />}>
          <JokerClassifier
            onBack={() => setTool('3d')}
            onOpenAdventure={() => setTool('adventure')}
          />
        </Suspense>
        <BalatroFanSiteAttributionFooter />
      </Fragment>
    )
  }

  if (tool === 'adventure') {
    return (
      <Suspense fallback={<ToolLoading />}>
        <AdventureApp
          onBack={() => setTool('3d')}
          onJokers={() => setTool('jokers')}
          onShop={() => setTool('shop')}
          onMotelyShop={() => setTool('motely')}
        />
      </Suspense>
    )
  }

  return (
    <Fragment>
      <div className="app-shell">
        <Leva collapsed />
        <Canvas
          shadows
          camera={{ position: [0, 5, 8], fov: 50 }}
          gl={{
            antialias: true,
            /** Display-referred output; avoids “double gamma” confusion with sRGB atlases. */
            outputColorSpace: THREE.SRGBColorSpace,
            /**
             * Was numeric 3 (Cineon) — strong contrast + hue shift on pixel art. Neutral keeps Balatro PNG colors closer to source.
             */
            toneMapping: THREE.NeutralToneMapping,
            toneMappingExposure: 1,
          }}
          style={{ touchAction: 'none' }}
        >
          <BalatroScene />
        </Canvas>

        <div className="ui-overlay">
          <div className="title">
            <h1>BALATRO 3D</h1>
            <p>Analyzer</p>
          </div>
          <div className="instructions">
            <p>Click cards to select (max 5)</p>
            <p>Orbit: Left Mouse | Zoom: Scroll</p>
            <SeedControls />
          </div>
          <div className="tool-nav">
            <button type="button" className="tool-switch" onClick={() => setTool('jokers')}>
              Joker classifier →
            </button>
            <button type="button" className="tool-switch" onClick={() => setTool('adventure')}>
              Highway drive →
            </button>
            <button type="button" className="tool-switch" onClick={() => setTool('shop')}>
              TS shop stream →
            </button>
            <button type="button" className="tool-switch" onClick={() => setTool('motely')}>
              Motely shop (WASM) →
            </button>
          </div>
        </div>
      </div>
      <BalatroFanSiteAttributionFooter />
    </Fragment>
  )
}

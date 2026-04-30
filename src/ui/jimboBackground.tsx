import React from 'react'
import { useBalatroBackground } from './hooks.js'

/**
 * Fullscreen WebGL CRT/spin background — the authentic Balatro hypnotic
 * swirl, pixelated and animated. Ported from weejoker.app's
 * BackgroundShader.tsx; no config required.
 *
 * Renders a fixed-position canvas at z-index: -10 that fills the viewport
 * and ignores pointer events. Drop it once at the root of your page:
 *
 *     <JimboBackground />
 *     <YourAppContent />
 *
 * Resizes automatically. Disposes the animation frame + shader on unmount.
 */
export function JimboBackground() {
  const canvasRef = useBalatroBackground()

  return (
    <canvas
      ref={canvasRef}
      aria-hidden
      style={{
        position: 'fixed',
        inset: 0,
        width: '100%',
        height: '100%',
        zIndex: -10,
        pointerEvents: 'none',
      }}
    />
  )
}

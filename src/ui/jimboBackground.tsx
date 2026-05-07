import React from 'react'
import { useBalatroBackground } from './hooks.js'
import { JimboBalatroFooter } from './footer.js'

/**
 * Fullscreen WebGL CRT/spin background — the authentic Balatro hypnotic
 * swirl, pixelated and animated. Also renders the attribution footer at the
 * bottom of the viewport (position: fixed) so it is always present and no
 * consumer can accidentally omit it.
 *
 * Drop it once at the root of your page:
 *
 *     <JimboBackground />
 *     <YourAppContent />
 *
 * Resizes automatically. Disposes the animation frame + shader on unmount.
 */
export function JimboBackground() {
  const canvasRef = useBalatroBackground()

  return (
    <>
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
      <JimboBalatroFooter />
    </>
  )
}

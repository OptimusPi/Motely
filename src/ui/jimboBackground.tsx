import React from 'react'
import { useBalatroBackground, type JimboBackgroundConfig } from './hooks.js'
import { JimboBalatroFooter } from './JimboBalatroFooter.js'

export type { JimboBackgroundConfig } from './hooks.js'

export interface JimboBackgroundProps extends JimboBackgroundConfig {
  /** Hide the persistent BalatroFooter attribution. Default: false. */
  hideFooter?: boolean
}

/**
 * Fullscreen WebGL CRT/spin background — the authentic Balatro hypnotic
 * swirl, pixelated and animated. Also renders the attribution footer at the
 * bottom of the viewport (position: fixed) so it is always present and no
 * consumer can accidentally omit it.
 *
 * All shader parameters are exposed as props (`primary`, `secondary`,
 * `dark`, `speed`, `spinRotation`, `spinAmount`, `pixelFilter`, `contrast`,
 * `lighting`, `transitionMs`). Palette and shader scalars ease over
 * `transitionMs` when props change. Defaults reproduce the canonical swirl.
 *
 *     <JimboBackground primary="#ff3344" secondary="#0088ff" speed={1.2} />
 *     <YourAppContent />
 */
export function JimboBackground({ hideFooter = false, ...config }: JimboBackgroundProps) {
  const canvasRef = useBalatroBackground(config)

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
      {!hideFooter && <JimboBalatroFooter />}
    </>
  )
}

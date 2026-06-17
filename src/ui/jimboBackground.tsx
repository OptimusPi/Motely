import React from 'react'
import { useBalatroBackground, type JimboBackgroundConfig } from './hooks.js'

export type { JimboBackgroundConfig } from './hooks.js'

export type JimboBackgroundProps = JimboBackgroundConfig

/**
 * Fullscreen WebGL CRT/spin background — the authentic Balatro hypnotic
 * swirl, pixelated and animated. Aesthetic only; the legally-required
 * attribution footer lives in JimboApp.
 *
 * All shader parameters are exposed as props (`primary`, `secondary`,
 * `dark`, `speed`, `spinRotation`, `spinAmount`, `pixelFilter`, `contrast`,
 * `lighting`, `transitionMs`). Palette and shader scalars ease over
 * `transitionMs` when props change. Defaults reproduce the canonical swirl.
 *
 *     <JimboBackground primary="#ff3344" secondary="#0088ff" speed={1.2} />
 *     <YourAppContent />
 */
export function JimboBackground(config: JimboBackgroundProps) {
  const canvasRef = useBalatroBackground(config)

  return (
    <canvas
      ref={canvasRef}
      aria-hidden
      className="j-background-canvas"
    />
  )
}

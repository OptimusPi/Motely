/**
 * Jammy: copy `withCrossOriginIsolated.ts` into your app (e.g. `lib/withCrossOriginIsolated.ts`)
 * and import from `next.config.ts` at the repo root:
 *
 *   import { withCrossOriginIsolated } from './lib/withCrossOriginIsolated'
 *
 * Turbopack: `next dev --turbo` uses the same `next.config`; if headers look wrong, compare `next dev`.
 */
import type { NextConfig } from 'next'
import { withCrossOriginIsolated } from './withCrossOriginIsolated'

const base: NextConfig = {
  /* reactStrictMode, images, experimental, etc. */
}

export default withCrossOriginIsolated(base, {
  // Switch to 'credentialless' if third-party embeds break under require-corp
  coep: 'require-corp',
})

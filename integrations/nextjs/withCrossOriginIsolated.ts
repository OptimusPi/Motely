import type { NextConfig } from 'next'

export type CoepMode = 'require-corp' | 'credentialless'

/**
 * Next.js merges `headers()` with your existing rules. This appends one catch-all rule
 * so the document (and matched paths) get COOP + COEP — enough for `crossOriginIsolated`
 * and threaded .NET WASM when the runtime needs it.
 *
 * - **require-corp**: strict; every cross-origin subresource must opt in (CORP/CORS). Best for SharedArrayBuffer.
 * - **credentialless**: looser (newer browsers); fewer third-party breakages, slightly different semantics.
 *
 * **Turbopack:** `next dev --turbo` reads `next.config` the same as webpack dev (Next 14+). If headers look missing, run `next dev` without `--turbo` once to compare.
 */
export function withCrossOriginIsolated(
  config: NextConfig,
  options: { coep?: CoepMode } = {}
): NextConfig {
  const coep = options.coep ?? 'require-corp'
  const { headers: userHeaders, ...rest } = config

  return {
    ...rest,
    async headers() {
      const existing = (await userHeaders?.()) ?? []
      return [
        ...existing,
        {
          source: '/:path*',
          headers: [
            { key: 'Cross-Origin-Opener-Policy', value: 'same-origin' },
            { key: 'Cross-Origin-Embedder-Policy', value: coep },
          ],
        },
      ]
    },
  }
}

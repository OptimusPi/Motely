# Next.js: COOP + COEP (cross-origin isolated)

Use this when browser WASM (e.g. Bootsharp) needs a cross-origin isolated context.

1. Copy **`withCrossOriginIsolated.ts`** into your Next app (e.g. `lib/withCrossOriginIsolated.ts`).
2. Wrap your existing config:

```ts
import type { NextConfig } from 'next'
import { withCrossOriginIsolated } from './lib/withCrossOriginIsolated'

const nextConfig: NextConfig = { /* …existing… */ }
export default withCrossOriginIsolated(nextConfig, { coep: 'require-corp' })
```

3. If third-party scripts or embeds break, try `coep: 'credentialless'` (supported in current Chromium).

**Do not** duplicate the same headers in `middleware.ts` and `next.config` unless you know why both are needed.

**Turbopack:** `next dev --turbo` still loads `next.config.ts`. If something looks off, compare with plain `next dev`.

**Vercel:** merge `vercel.headers.example.json` into your `vercel.json` `headers` array if the platform strips Next config headers (rare for standard Next deploys).

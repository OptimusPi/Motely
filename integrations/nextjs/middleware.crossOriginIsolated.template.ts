/**
 * OPTIONAL alternative to `next.config` `headers()` — use as `middleware.ts` at the app root.
 * Pick **one** approach (config headers OR middleware), not both duplicated blindly.
 *
 * Middleware runs on the Edge; COOP/COEP here apply to matched responses.
 * Tune `matcher` if something (e.g. webhooks) must not get COEP.
 */
import type { NextRequest } from 'next/server'
import { NextResponse } from 'next/server'

export function middleware(_request: NextRequest) {
  const res = NextResponse.next()
  res.headers.set('Cross-Origin-Opener-Policy', 'same-origin')
  res.headers.set('Cross-Origin-Embedder-Policy', 'require-corp')
  return res
}

export const config = {
  matcher: '/:path*',
}

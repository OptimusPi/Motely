'use client'

/**
 * motely-wasm 4.x default `boot()` passes a `root` URL derived from `import.meta.url`.
 * Under Next/Webpack that can become a `file://` path and dynamic `import()` of dotnet.js fails.
 * Bootsharp supports `root: null` to use the embedded dotnet bundle instead.
 */

type BootsharpModule = {
  default: { boot: (opts: { root: string | null }) => Promise<void> }
}

let embeddedBoot: Promise<void> | null = null

export function bootMotelyEmbedded(): Promise<void> {
  if (typeof window === 'undefined') {
    return Promise.reject(new TypeError('motely-wasm can only boot in the browser'))
  }
  embeddedBoot ??= (async () => {
    const mod = (await import(
      /* webpackAlias: resolved in next.config.ts */
      'motely-wasm-internal-bootsharp'
    )) as BootsharpModule
    await mod.default.boot({ root: null })
  })()
  return embeddedBoot
}

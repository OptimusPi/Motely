import fs from 'node:fs'
import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const rootDir = __dirname
const motelyWasmEntry = path.join(rootDir, 'node_modules/motely-wasm/dist/index.mjs')
/** Bootsharp loader + interop (package `dist/bootsharp/` is dotnet.* only, no index.mjs). */
const motelyBootsharpEntry = path.join(rootDir, 'node_modules/motely-wasm/dist/index.mjs')
const useMotelyWasmShim = !fs.existsSync(motelyWasmEntry)

// Browser WASM is happier with COOP/COEP when the runtime uses threads; harmless if unused.
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@balatrots': path.resolve(rootDir, 'assethelp/src/modules/balatrots'),
      ...(useMotelyWasmShim
        ? {
            'motely-wasm': path.resolve(rootDir, 'shims/motely-wasm.ts'),
            'motely-wasm-internal-bootsharp': path.resolve(
              rootDir,
              'shims/motely-wasm-internal-bootsharp.ts'
            ),
          }
        : {
            'motely-wasm-internal-bootsharp': motelyBootsharpEntry,
          }),
    },
  },
  server: {
    port: 5174,
    fs: { allow: [path.resolve(rootDir, '..')] },
    headers: {
      'Cross-Origin-Opener-Policy': 'same-origin',
      'Cross-Origin-Embedder-Policy': 'require-corp',
    },
  },
  optimizeDeps: {
    exclude: useMotelyWasmShim ? [] : ['motely-wasm'],
  },
})

import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Browser WASM is happier with COOP/COEP when the runtime uses threads; harmless if unused.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    fs: { allow: [path.resolve(__dirname, '..')] },
    headers: {
      'Cross-Origin-Opener-Policy': 'same-origin',
      'Cross-Origin-Embedder-Policy': 'require-corp',
    },
  },
  optimizeDeps: {
    exclude: ['motely-wasm'],
  },
})

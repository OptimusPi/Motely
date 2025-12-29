import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'

export default defineConfig({
  base: '/',
  plugins: [vue()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src')
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://192.168.0.171:3141',
        changeOrigin: true
      },
      '/search': {
        target: 'http://192.168.0.171:3141',
        changeOrigin: true
      },
      '/filters': {
        target: 'http://192.168.0.171:3141',
        changeOrigin: true
      },
      '/seed-sources': {
        target: 'http://192.168.0.171:3141',
        changeOrigin: true
      },
      '/searchHub': {
        target: 'ws://192.168.0.171:3141',
        ws: true
      }
    }
  },
  preview: {
    port: 4173
  },
  build: {
    outDir: '../wwwroot/JAML',
    emptyOutDir: false,
    assetsDir: 'assets',
    chunkSizeWarningLimit: 4000,
    rollupOptions: {
      output: {
        manualChunks: {
          'monaco': ['monaco-editor'],
          'tabulator': ['tabulator-tables']
        }
      }
    }
  }
})


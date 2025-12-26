import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  base: '/JamlUI3/',
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
        target: 'http://localhost:3141',
        changeOrigin: true
      },
      '/search': {
        target: 'http://localhost:3141',
        changeOrigin: true
      },
      '/filters': {
        target: 'http://localhost:3141',
        changeOrigin: true
      },
      '/seed-sources': {
        target: 'http://localhost:3141',
        changeOrigin: true
      },
      '/searchHub': {
        target: 'ws://localhost:3141',
        ws: true
      }
    }
  },
  build: {
    outDir: 'dist',
    assetsDir: 'assets',
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


import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'

export default defineConfig({
  base: '/JamlUI-Vue3-Vite/',
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
  build: {
    outDir: '../../wwwroot/JamlUI-Vue3-Vite',
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


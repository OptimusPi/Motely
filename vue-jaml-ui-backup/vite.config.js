import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { resolve } from 'path'
import { readFileSync } from 'node:fs'

function jamlSchemaDevPlugin() {
  const schemaPath = resolve(__dirname, '../jaml.schema.json')
  return {
    name: 'jaml-schema-dev',
    configureServer(server) {
      server.middlewares.use('/jaml.schema.json', (_req, res, next) => {
        try {
          const schemaJson = readFileSync(schemaPath, 'utf8')
          res.statusCode = 200
          res.setHeader('Content-Type', 'application/json; charset=utf-8')
          res.end(schemaJson)
        } catch (e) {
          server.config.logger.warn(`Failed to serve JAML schema from ${schemaPath}: ${e}`)
          next()
        }
      })
    }
  }
}

export default defineConfig(({ mode }) => ({
  base: mode === 'production' ? '/JAML/' : '/',
  plugins: [vue(), jamlSchemaDevPlugin()],
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
}))


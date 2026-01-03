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
      
      // Suppress WebSocket proxy ECONNRESET errors
      const originalError = server.config.logger.error
      server.config.logger.error = (msg, options) => {
        // Filter out harmless WebSocket proxy ECONNRESET errors
        const msgStr = typeof msg === 'string' ? msg : String(msg)
        if (msgStr.includes('ws proxy socket error') || 
            (msgStr.includes('ECONNRESET') && msgStr.includes('proxy'))) {
          // Suppress this error - it's harmless and common when connections reset
          return
        }
        originalError(msg, options)
      }
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
        ws: true,
        configure: (proxy, _options) => {
          // Suppress all WebSocket proxy errors - they're usually harmless connection resets
          proxy.on('error', (err, _req, _res) => {
            // Only log non-ECONNRESET errors
            if (err.code !== 'ECONNRESET' && err.code !== 'EPIPE') {
              console.error('WebSocket proxy error:', err.message)
            }
            // Suppress ECONNRESET and EPIPE - these are common when connections reset
          })
          proxy.on('proxyReqWs', (proxyReq, _req, _socket) => {
            // Handle socket errors silently
            _socket.on('error', (err) => {
              // Suppress ECONNRESET errors on the socket
              if (err.code !== 'ECONNRESET' && err.code !== 'EPIPE') {
                console.error('WebSocket socket error:', err.message)
              }
            })
          })
          proxy.on('proxyRes', (_proxyRes, _req, _res) => {
            // Handle response errors
            _proxyRes.on('error', (err) => {
              if (err.code !== 'ECONNRESET' && err.code !== 'EPIPE') {
                console.error('WebSocket proxy response error:', err.message)
              }
            })
          })
        }
      }
    }
  },
  preview: {
    port: 4173
  },
  build: {
    outDir: '../wwwroot/JAML',
    emptyOutDir: true, // Clean output directory for proper cache busting
    assetsDir: 'assets',
    chunkSizeWarningLimit: 4000,
    minify: false, // Disable minification - build with npm in CI/CD instead
    // Vite automatically uses content-based hashing for cache busting
    // File names include hash: index-[hash].js, assets/[name]-[hash].[ext]
    rollupOptions: {
      output: {
        // Content-based hashing is automatic - no manual hash needed
        entryFileNames: 'assets/[name]-[hash].js',
        chunkFileNames: 'assets/[name]-[hash].js',
        assetFileNames: (assetInfo) => {
          // Fonts don't need hashing (rarely change), other assets get content-based hash
          const name = assetInfo.name || ''
          if (name.endsWith('.ttf') || name.endsWith('.otf')) {
            return 'fonts/[name][extname]'
          }
          // All other assets get content-based hash for proper cache busting
          return 'assets/[name]-[hash][extname]'
        },
        manualChunks: {
          'monaco': ['monaco-editor'],
          'tabulator': ['tabulator-tables']
        }
      }
    }
  }
}))


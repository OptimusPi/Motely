import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import motelyWasm from 'motely-wasm/vite-plugin'
import path from 'path'

// https://vitejs.dev/config/
export default defineConfig({
    base: '/JamlUI-v99/', // when served by Motely.API at /JamlUI-v99/
    plugins: [
        vue(),
        motelyWasm(), // Serves _framework in dev, copies to dist on build, sets COOP/COEP headers
    ],
    resolve: {
        alias: {
            '@': path.resolve(__dirname, './src'),
        },
    },
    server: {
        port: 3000,
    }
})

import { createApp } from 'vue'
import App from './App.vue'
import router from './router.js'
import './style.css'

// Prevent layout shift by detecting font load
if ('fonts' in document) {
  document.fonts.ready.then(() => {
    document.documentElement.classList.add('font-loaded')
  })
} else {
  // Fallback for browsers without Font Loading API
  const font = new FontFace('m6x11plus', 'url(/fonts/m6x11plus.ttf)')
  font.load().then(() => {
    document.documentElement.classList.add('font-loaded')
  }).catch(() => {
    // Font failed to load, keep fallback
    document.documentElement.classList.add('font-loaded')
  })
}

try {
  const app = createApp(App)

  app.use(router)

  // Vue error handler
  app.config.errorHandler = (err, instance, info) => {
    console.error('Vue error:', err, info)
  }
  
  app.mount('#app')
} catch (error) {
  console.error('Failed to mount app:', error)
}


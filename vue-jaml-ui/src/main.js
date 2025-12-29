import { createApp } from 'vue'
import App from './App.vue'
import router from './router.js'
import './style.css'

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


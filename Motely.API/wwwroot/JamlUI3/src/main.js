import { createApp } from 'vue'
import App from './App.vue'
import './style.css'

// Global error handler
window.addEventListener('error', (event) => {
  console.error('Global error:', event.error)
  const app = document.getElementById('app')
  if (app && !app.querySelector('.error-display')) {
    app.innerHTML = `
      <div class="error-display" style="padding: 20px; color: white; font-family: monospace; background: #1e2b2d;">
        <h1>Error Loading App</h1>
        <p>${event.error?.message || 'Unknown error'}</p>
        <p>Check browser console (F12) for details.</p>
        <button onclick="location.reload()" style="padding: 8px 16px; margin-top: 10px; cursor: pointer;">Reload</button>
      </div>
    `
  }
})

try {
  const app = createApp(App)
  
  // Vue error handler
  app.config.errorHandler = (err, instance, info) => {
    console.error('Vue error:', err, info)
  }
  
  app.mount('#app')
} catch (error) {
  console.error('Failed to mount app:', error)
  const appEl = document.getElementById('app')
  if (appEl) {
    appEl.innerHTML = `
      <div style="padding: 20px; color: white; font-family: monospace; background: #1e2b2d;">
        <h1>Error Loading App</h1>
        <p>${error.message}</p>
        <p>Check console for details.</p>
        <button onclick="location.reload()" style="padding: 8px 16px; margin-top: 10px; cursor: pointer;">Reload</button>
      </div>
    `
  }
}


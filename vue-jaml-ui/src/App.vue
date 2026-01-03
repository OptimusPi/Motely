<template>
  <div class="app">
    <ErrorBoundary>
      <main class="app-main">
        <router-view />
      </main>
    </ErrorBoundary>
    <footer class="global-footer">
      <span>Not affiliated with LocalThunk or PlayStack. <a href="https://playbalatro.com/" target="_blank" rel="noreferrer">BUY Balatro</a> • Made with ❤️ for the Balatro community.</span>
    </footer>
  </div>
</template>

<script setup>
import ErrorBoundary from './components/ErrorBoundary.vue'
// Global styles and setup can go here if needed
</script>

<style>
/* Global styles */
:root {
  /* Base UI Colors */
  --bg-color: #33464b;
  --panel-bg: #3a5055;
  --dark-bg: #1e2b2d;
  --border-color: #b9c2d2;
  --text-color: #ffffff;
  --light-grey: #777e89;

  /* Balatro-inspired color scheme */
  --balatro-red: #ff4c40;
  --balatro-dark-red: #a02721;
  --balatro-blue: #0093ff;
  --balatro-dark-blue: #0057a1;
  --balatro-purple: #9b59b6;
  --balatro-dark-purple: #5D3570;
  --balatro-green: #429f79;
  --balatro-dark-green: #215f46;
  --balatro-gold: #eaba44;

  /* Shadows */
  --shadow-color: rgba(0, 0, 0, 0.4);
  --button-shadow: #1e2e32;
}

* {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

html, body {
  height: 100vh;
  font-family: 'm6x11plus', monospace; /* Use m6x11 font everywhere */
  background: var(--bg-color);
  color: var(--text-color);
  -webkit-tap-highlight-color: transparent;
  /* NEVER use bold - always normal weight for readability */
  font-weight: normal;
  /* Prevent layout shift - set font metrics upfront */
  font-size: 16px;
  line-height: 1.5;
}

/* Force all elements to use normal font weight - NO BOLD EVER */
/* Using high specificity selector instead of !important */
html *,
html *::before,
html *::after {
  font-weight: normal;
}

#app {
  height: 100vh;
  min-height: 100vh; /* Prevent collapse */
  overflow: hidden; /* Disable page scrolling */
  /* Prevent layout shift */
  contain: layout style paint;
}

/* Utility classes */
.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-family: inherit;
  font-size: 14px;
  transition: all 0.2s ease;
}

.btn:hover {
  transform: translateY(-1px);
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}

.btn-primary {
  background: var(--balatro-blue);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--balatro-dark-blue);
}

.btn-secondary {
  background: var(--panel-bg);
  color: var(--text-color);
  border: 1px solid var(--border-color);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--dark-bg);
}

/* Form elements */
.form-field {
  padding: 8px 12px;
  background: var(--dark-bg);
  border: 1px solid var(--border-color);
  border-radius: 4px;
  color: var(--text-color);
  font-family: inherit;
  font-size: 14px;
  outline: none;
  transition: border-color 0.2s ease;
}

.form-field:focus {
  border-color: var(--balatro-blue);
}

.form-field::placeholder {
  color: var(--light-grey);
}

/* Scrollbar styling */
::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

::-webkit-scrollbar-track {
  background: var(--dark-bg);
  border-radius: 4px;
}

::-webkit-scrollbar-thumb {
  background: var(--border-color);
  border-radius: 4px;
}

::-webkit-scrollbar-thumb:hover {
  background: var(--balatro-blue);
}

/* Mobile optimizations */
@media (max-width: 768px) {
  .btn {
    padding: 12px 20px;
    font-size: 16px; /* Prevent zoom on iOS */
    min-height: 44px; /* Ensure 44px touch target */
    min-width: 44px;
  }

  .form-field {
    font-size: 16px; /* Prevent zoom on iOS */
    min-height: 44px;
    padding: 12px 16px;
  }
}

/* Error handling */
.error-display {
  padding: 20px;
  background: var(--dark-bg);
  border: 2px solid var(--balatro-red);
  border-radius: 8px;
  text-align: center;
}

.error-display h1 {
  color: var(--balatro-red);
  margin-bottom: 16px;
}

.error-display p {
  margin-bottom: 12px;
}

.error-display button {
  background: var(--balatro-blue);
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  font-family: inherit;
  font-size: 14px;
  transition: all 0.2s ease;
}

.error-display button:hover {
  background: var(--balatro-dark-blue);
}

/* Global footer - one line, appears on all pages */
.global-footer {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 24px;
  background: var(--panel-bg);
  border-top: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  padding: 0 8px;
  font-size: 11px;
  color: var(--text-color);
  z-index: 3000; /* Above split divider (2000) but below modals (10000) */
  line-height: 1;
}

.global-footer a {
  color: var(--balatro-gold);
  text-decoration: none;
}

.global-footer a:hover {
  text-decoration: underline;
}

.app {
  height: 100vh;
  overflow: hidden; /* Disable page scrolling */
  display: flex;
  flex-direction: column;
}

/* Add padding to main content so footer doesn't overlap */
.app-main {
  padding-bottom: 24px;
  flex: 1;
  overflow: hidden; /* Disable page scrolling */
  min-height: 0;
}
</style>

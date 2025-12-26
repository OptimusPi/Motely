# JAML UI3 - Modern Vue 3 Implementation

A clean, modern, modular Vue 3 + Vite implementation of the JAML filter editor and seed search interface.

## Features

- ✨ **Vue 3 Composition API** - Modern, reactive, clean code
- 🚀 **Vite** - Lightning fast dev server and builds
- 🎨 **Modular Components** - Clean separation of concerns
- 📦 **Composables** - Reusable logic (filters, search, SignalR, layout)
- 🖱️ **Smooth Drag/Resize** - Custom split pane with smooth animations
- 📝 **Monaco Editor** - Full-featured code editor
- 🔄 **SignalR** - Real-time search updates
- 📊 **Tabulator** - Fast, feature-rich results table
- 📱 **Responsive** - Works on mobile and desktop

## Setup

```bash
cd Motely.API/wwwroot/JamlUI3
npm install
npm run dev
```

## Build

```bash
npm run build
```

Output goes to `dist/` folder.

## Structure

```
src/
  ├── App.vue              # Main app component
  ├── main.js              # Entry point
  ├── style.css            # Global styles
  ├── components/          # Vue components
  │   ├── SplitPane.vue
  │   ├── PanelSection.vue
  │   ├── EditorPanel.vue
  │   ├── ResultsPanel.vue
  │   ├── BlueprintPanel.vue
  │   ├── ActiveSearchesPanel.vue
  │   └── SettingsModal.vue
  └── composables/         # Reusable logic
      ├── useFilters.js
      ├── useSearch.js
      ├── useSignalR.js
      └── useLayout.js
```

## Development

The app runs on `http://localhost:5173` by default. Vite proxies API calls to `http://localhost:3141`.

## Production

After building, copy the `dist/` folder contents to your web server. The app expects the API to be available at the same origin or configure CORS properly.



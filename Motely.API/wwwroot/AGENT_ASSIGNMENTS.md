# Multi-Agent UI Framework Assignments

Assign each UnderConstruction folder to a different agent with a specific framework:

## Recommended Assignments

- **UnderConstruction1**: Alpine.js (lightweight, no build)
- **UnderConstruction2**: ✅ Already done (Alpine.js)
- **UnderConstruction3**: React + Vite
- **UnderConstruction4**: Vue 3 + Vite
- **UnderConstruction5**: Svelte + Vite
- **UnderConstruction6**: Preact + Vite
- **UnderConstruction7**: SolidJS + Vite
- **UnderConstruction8**: Lit (Web Components)
- **UnderConstruction9**: Vanilla JS (no framework)
- **UnderConstruction10**: HTMX + Alpine.js
- **UnderConstruction11**: Mithril.js
- **UnderConstruction12**: Stencil (Web Components)

## How to Run Multiple Agents

### Option 1: Multiple Cursor Windows
1. Open 4 separate Cursor windows
2. In each window, navigate to a different UnderConstruction folder
3. Use Composer with: "Build the search UI per REQUIREMENTS.md using [FRAMEWORK]"
4. Each agent works independently

### Option 2: Sequential with Different Agents
1. Open one Cursor window
2. Work on UnderConstruction3 (React)
3. When done, switch to UnderConstruction4 (Vue)
4. Continue through folders

### Option 3: Copy Requirements to Each Folder
```bash
# Copy requirements to each folder
for i in {3..12}; do
  cp UnderConstruction2/REQUIREMENTS.md UnderConstruction$i/
done
```

Then in each folder's Composer:
"Read REQUIREMENTS.md and build the UI using [FRAMEWORK]"

## Composer Prompts

### For React (UnderConstruction3):
```
Read REQUIREMENTS.md in this folder. Create a React + Vite app with:
- Side-by-side layout (JAML editor left, results table right)
- Draggable splitter
- SignalR integration
- Mobile-responsive table
- All API endpoints from requirements
Use the Balatro color scheme.
```

### For Vue (UnderConstruction4):
```
Read REQUIREMENTS.md in this folder. Create a Vue 3 + Vite app with:
- Side-by-side layout (JAML editor left, results table right)
- Draggable splitter
- SignalR integration
- Mobile-responsive table
- All API endpoints from requirements
Use the Balatro color scheme.
```

### For Svelte (UnderConstruction5):
```
Read REQUIREMENTS.md in this folder. Create a Svelte + Vite app with:
- Side-by-side layout (JAML editor left, results table right)
- Draggable splitter
- SignalR integration
- Mobile-responsive table
- All API endpoints from requirements
Use the Balatro color scheme.
```

## Testing All Versions

Once all are built, test each at:
- `http://192.168.0.171:3141/UnderConstruction1/`
- `http://192.168.0.171:3141/UnderConstruction2/`
- `http://192.168.0.171:3141/UnderConstruction3/`
- etc.

Compare:
- Performance
- Bundle size
- Mobile experience
- Code maintainability
- Developer experience


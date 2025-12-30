---
name: ""
overview: ""
todos: []
---

# Reorganize Frontend Source Files - Complete Site-by-Site Analysis

## Problem Statement

`wwwroot/` should only contain static files that are served directly. Currently, source files (build configs, src/, node_modules/) are mixed with static files, making it unclear what needs building vs what is ready to serve.

## Complete Site Inventory

### Site 1: JAML (wwwroot/JAML/)

- **Type**: STATIC - Pure HTML/CSS/JS
- **Files**: index.html, jaml.js, styles.css, visual-builder.js
- **Build Required**: NO
- **Action**: KEEP IN wwwroot (no changes)

### Site 2: JamlUI3 (wwwroot/JamlUI3/)

- **Type**: NEEDS BUILD - Vue 3 + Vite project
- **Source Files Present**:
- `src/` directory (17 files: .vue, .js, .css)
- `vite.config.js` (build configuration)
- `package.json` (dependencies)
- `package-lock.json` (lock file)
- `node_modules/` (dependencies - should NOT be in wwwroot)
- `index.html` (dev entry point, not built version)
- Documentation files (README.md, GET_STARTED.md, etc.)
- **Built Output Present**:
- `dist/` directory (contains built assets and index.html)
- `assets/` directory (old build artifacts, should be cleaned)
- **Action**: MOVE SOURCE FILES OUT, KEEP BUILT OUTPUT IN wwwroot

### Site 3: JamlUI4 (wwwroot/JamlUI4/)

- **Type**: STATIC - Alpine.js via CDN
- **Files**: index.html, app.js, style.css
- **Build Required**: NO
- **Action**: KEEP IN wwwroot (no changes)

### Site 4: JamlGenie (wwwroot/JamlGenie/)

- **Type**: STATIC - Cloudflare Pages deployment
- **Files**: Static HTML/JS/CSS files
- **package.json**: Only for `wrangler deploy` command, not a build step
- **Build Required**: NO
- **Action**: KEEP IN wwwroot (no changes)

### Site 5: UnderConstruction4 (wwwroot/UnderConstruction4/)

- **Type**: STATIC - Vue 3 via CDN (unpkg.com/vue@3)
- **Files**: index.html, app.js, style.css
- **Build Required**: NO
- **Action**: KEEP IN wwwroot (no changes)

### Site 6: UnderConstruction5 (wwwroot/UnderConstruction5/)

- **Type**: STATIC - Preact via CDN (esm.sh)
- **Files**: index.html, app.js, style.css
- **Build Required**: NO
- **Action**: KEEP IN wwwroot (no changes)

### Site 7: UnderConstruction6 (wwwroot/UnderConstruction6/)

- **Type**: STATIC - HTMX/Vanilla JS
- **Files**: index.html, app.js, style.css
- **Build Required**: NO
- **Action**: KEEP IN wwwroot (no changes)

### Site 8-12: UnderConstruction7-12 (wwwroot/UnderConstruction7-12/)

- **Type**: EMPTY - Reserved for future experiments
- **Action**: KEEP IN wwwroot (no changes)

## Solution: Move ONLY JamlUI3 Source Files

### Step 1: Create Frontend Source Directory Structure

Create `Motely.API/frontend/` directory to hold source files for projects that require builds.

````javascript
Motely.API/
├── frontend/                    # NEW: Source files only
│   └── JamlUI3/                 # Vue 3 project source
│       ├── src/                 # Source files
│       ├── vite.config.js       # Build config
│       ├── package.json          # Dependencies
│       ├── package-lock.json    # Lock file
│       ├── node_modules/        # Dependencies (will be moved)
│       ├── index.html           # Dev entry point
│       └── README.md            # Documentation
│
└── wwwroot/                      # Static/built files only
    ├── JAML/                    # STATIC - No changes
    ├── JamlUI3/                 # BUILT OUTPUT ONLY
    │   ├── assets/             # Built assets
    │   └── index.html           # Built entry point
    ├── JamlUI4/                 # STATIC - No changes
    ├── JamlGenie/               # STATIC - No changes
    └── UnderConstruction*/      # STATIC - No changes
```



### Step 2: Move JamlUI3 Source Files (CAREFUL - ONLY THIS ONE)

**Files to MOVE from wwwroot/JamlUI3/ to frontend/JamlUI3/:**

1. `src/` directory (entire directory)
2. `vite.config.js`
3. `package.json`
4. `package-lock.json`
5. `node_modules/` directory (entire directory)
6. `index.html` (the dev entry point at root)
7. Documentation files: `README.md`, `GET_STARTED.md`, `QUICKSTART.md`

**Files to KEEP in wwwroot/JamlUI3/:**

1. `dist/` directory (built output - this is what gets served)
2. `favicon.ico` (static asset)
3. `m6x11plusplus.otf` (static asset, if needed by built output)

**Files to REMOVE from wwwroot/JamlUI3/:**

1. `assets/` directory (old build artifacts, not needed if dist/ exists)

### Step 3: Update Vite Configuration

Modify `frontend/JamlUI3/vite.config.js`:

- Change `outDir: 'dist'` to `outDir: '../../wwwroot/JamlUI3'`
- This makes the build output directly to wwwroot where it will be served
- Keep `base: '/JamlUI3/'` unchanged (served at /JamlUI3/ URL)

### Step 4: Verify Build Process

After moving files:

1. Navigate to `frontend/JamlUI3/`
2. Run `npm install` (to ensure dependencies are correct in new location)
3. Run `npm run build` (should output to `wwwroot/JamlUI3/`)
4. Verify `wwwroot/JamlUI3/index.html` exists and is the built version
5. Test serving at `http://localhost:3141/JamlUI3/`

### Step 5: Clean Up wwwroot/JamlUI3

After successful build:

1. Remove `wwwroot/JamlUI3/assets/` (old artifacts)
2. Ensure only built files remain in `wwwroot/JamlUI3/`

## Implementation Checklist

- [ ] Create `Motely.API/frontend/` directory
- [ ] Create `Motely.API/frontend/JamlUI3/` directory
- [ ] Move `wwwroot/JamlUI3/src/` → `frontend/JamlUI3/src/`
- [ ] Move `wwwroot/JamlUI3/vite.config.js` → `frontend/JamlUI3/vite.config.js`
- [ ] Move `wwwroot/JamlUI3/package.json` → `frontend/JamlUI3/package.json`
- [ ] Move `wwwroot/JamlUI3/package-lock.json` → `frontend/JamlUI3/package-lock.json`
- [ ] Move `wwwroot/JamlUI3/node_modules/` → `frontend/JamlUI3/node_modules/`
- [ ] Move `wwwroot/JamlUI3/index.html` → `frontend/JamlUI3/index.html`
- [ ] Move documentation files → `frontend/JamlUI3/`
- [ ] Update `frontend/JamlUI3/vite.config.js` outDir to `../../wwwroot/JamlUI3`
- [ ] Remove `wwwroot/JamlUI3/assets/` (old build artifacts)
- [ ] Verify `wwwroot/JamlUI3/dist/` still exists (current built output)
- [ ] Test build from new location
- [ ] Create `frontend/JamlUI3/BUILD.md` with build instructions

## Important Notes

1. **ONLY JamlUI3 is being moved** - All other sites remain untouched in wwwroot
2. **JamlUI3 is the ONLY site with source files** - All others are static
3. **Build output goes directly to wwwroot/JamlUI3/** - No copying needed
4. **Other sites (JAML, JamlUI4, JamlGenie, UnderConstruction*) are NOT affected**
5. **The dist/ folder in wwwroot/JamlUI3/ is the current built output** - Keep it until rebuilt

## Future Considerations


````
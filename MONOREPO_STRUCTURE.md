# Balatro Seed Oracle - Monorepo Structure

## Overview

This is a monorepo containing:
- **Motely** - Core C# seed search engine
- **Motely.API** - Backend API server (hosts everything)
- **JAML UI** - Human-facing web UI (YAML filters with a J)
- **JamlGenie** - AI-powered filter generator frontend

## Directory Structure

```
BalatroSeedOracle/
├── external/
│   └── Motely/                    # This monorepo
│       ├── Motely/                # Core C# engine (seed searcher)
│       │   └── Motely.csproj
│       │
│       ├── Motely.API/            # Backend API server
│       │   ├── Motely.API.csproj
│       │   ├── Program.cs         # API endpoints, SignalR hub
│       │   └── wwwroot/           # Static web files
│       │       ├── JAML/          # JAML UI (human-facing)
│       │       │   ├── index.html
│       │       │   ├── jaml.js
│       │       │   └── styles.css
│       │       │
│       │       └── JamlGenie/     # JamlGenie frontend
│       │           ├── index.html
│       │           ├── app.js
│       │           └── deploy.ps1
│       │
│       ├── Motely.Tests/          # Unit tests
│       ├── Motely.CLI/            # Command Line Interface
│       ├── Motely.TUI/            # Terminal UI (Terminal.GUI v2)
│       ├── JamlFilters/           # JAML filter files (human-facing)
│       ├── WordLists/             # Seed source databases
│       └── Motely.sln             # Visual Studio solution
│
└── (other BalatroSeedOracle modules)
```

## How It Works

### 1. Core Engine (`Motely/`)
- Pure C# library
- Searches seeds based on JAML filters
- No UI, just logic
- Used by API, CLI, TUI, and tests

### 1.5. Command Line Interface (`Motely.CLI/`)
- Standalone CLI executable
- Parses command-line arguments
- Runs searches via core engine
- No GUI dependencies

### 1.6. Terminal UI (`Motely.TUI/`)
- Terminal.GUI v2 interface
- Interactive menu system
- Filter builder
- API server launcher

### 2. Backend API (`Motely.API/`)
- ASP.NET Core web server
- Serves static files from `wwwroot/`
- Provides REST API endpoints:
  - `/mcp/prompt` - JamlGenie AI endpoint
  - `/search` - Start/stop searches
  - `/seed-sources` - List seed databases
  - `/filters/*` - Filter management
- SignalR hub for real-time search updates
- Hosts both JAML UI and JamlGenie

### 3. JAML UI (`Motely.API/wwwroot/JAML/`)
- Human-facing web interface
- Users write/edit JAML filters
- Search seeds, view results
- Accessible at: `http://your-api/JAML/`

### 4. JamlGenie (`Motely.API/wwwroot/JamlGenie/`)
- AI-powered filter generator
- Users type natural language ("2 blueprints")
- AI generates JAML filter
- Can be deployed separately to Cloudflare Pages
- Accessible at: `http://your-api/JamlGenie/` or `https://balatrogenie.app`

## Deployment Options

### Option A: All-in-One (Current Setup)
- Run `Motely.API` on your server
- Everything served from one place:
  - `http://your-server:3141/JAML/` - JAML UI
  - `http://your-server:3141/JamlGenie/` - JamlGenie
  - `http://your-server:3141/api/*` - API endpoints

### Option B: Separate Frontend (Cloudflare Pages)
- Deploy `JamlGenie/` to Cloudflare Pages
- Keep `Motely.API` on your server
- Set `API_BASE_URL` environment variable in Cloudflare
- JamlGenie calls your API from browser

### Option C: GitHub Pages / Static Hosting
- Copy `JamlGenie/` or `JAML/` to separate repo
- Deploy to GitHub Pages, Netlify, Vercel, etc.
- Point to your API server via environment variable

## File Types

- **JAML files** (`.jaml`) - Human-facing filter format (YAML with a J)
  - Stored in `JamlFilters/` directory
  - Users create/edit these
  - Converted to JSON internally by `JamlConfigLoader`

- **JSON config** - Internal format
  - Generated from JAML
  - Used by core engine
  - Not meant for humans to edit

## Development Workflow

1. **Edit JAML filters**: Create `.jaml` files in `JamlFilters/`
2. **Test in JAML UI**: Open `http://localhost:3141/JAML/`
3. **Use JamlGenie**: Open `http://localhost:3141/JamlGenie/` to generate filters
4. **API changes**: Edit `Motely.API/Program.cs`
5. **Core engine changes**: Edit `Motely/` files
6. **Deploy**: 
   - API: Deploy `Motely.API` to your server
   - JamlGenie: Run `Motely.API/wwwroot/JamlGenie/deploy.ps1`

## Adding to GitHub

### As Single Repo (Recommended)
```bash
cd BalatroSeedOracle
git add external/Motely
git commit -m "Add Motely monorepo"
git push
```

### As Submodule (If you want to keep separate)
```bash
cd BalatroSeedOracle
git submodule add https://github.com/yourusername/motely.git external/Motely
```

## Key Files

- `Motely.API/Program.cs` - Main API server code
- `Motely.API/wwwroot/JAML/jaml.js` - JAML UI frontend
- `Motely.API/wwwroot/JamlGenie/app.js` - JamlGenie frontend
- `Motely/JamlConfigLoader.cs` - Converts JAML → JSON
- `JamlFilters/*.jaml` - Your filter files


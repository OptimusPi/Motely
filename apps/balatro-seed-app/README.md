# JAML Seed Lab

> **Balatro seed finder, analyzer, and JAML IDE** — a composable Next.js app ecosystem powered by [motely-wasm](https://github.com/OptimusPi/MotelyJAML), [json-render](https://github.com/json-render/json-render), and [jaml-ui](https://github.com/OptimusPi/jaml-ui).

[![npm version](https://img.shields.io/npm/v/jaml-seed-lab)](https://www.npmjs.com/package/jaml-seed-lab)
![License](https://img.shields.io/npm/l/jaml-seed-lab)
![Node](https://img.shields.io/badge/node-%3E%3D20.0.0-brightgreen)

---

## Table of Contents

- [Install](#install)
- [Quick Start](#quick-start)
- [The 4 Apps](#the-4-apps)
  - [1. JAML IDE](#1-jaml-ide)
  - [2. Home + Filters Browser](#2-home--filters-browser)
  - [3. Seed Finder](#3-seed-finder)
  - [4. Seed Analyzer (JAMLYZER)](#4-seed-analyzer-jamlyzer)
- [MCP Integration](#mcp-integration)
  - [CodeMirror 6 MCP Extension](#codemirror-6-mcp-extension)
  - [MCP Panel](#mcp-panel)
  - [MCP App (Iframe)](#mcp-app-iframe)
- [CLI](#cli)
- [API Routes](#api-routes)
- [Architecture](#architecture)
- [Publishing](#publishing)
- [License](#license)

---

## Install

```bash
npm install jaml-seed-lab
```

Or install the CLI globally:

```bash
npm install -g jaml-seed-lab
```

---

## Quick Start

### 1. Scaffold a new project

```bash
npx jaml-seed-lab init my-seed-app
cd my-seed-app
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000).

### 2. Use as a library in your own Next.js app

```tsx
// app/page.tsx
import { JamlSeedLabHomePage } from "jaml-seed-lab/apps/home";
export default JamlSeedLabHomePage;

// app/ide/page.tsx
import { JamlIdePage } from "jaml-seed-lab/apps/ide";
export default JamlIdePage;

// app/finder/page.tsx
import { SeedFinderPage } from "jaml-seed-lab/apps/finder";
export default SeedFinderPage;

// app/analyzer/page.tsx
import { JamlSeedAnalyzerPage } from "jaml-seed-lab/apps/analyzer";
export default JamlSeedAnalyzerPage;
```

### 3. Use the MCP client in your own app

```tsx
import { useMcpClient, McpPanel } from "jaml-seed-lab/mcp";

function MyApp() {
  const { state, tools, connect } = useMcpClient({
    serverUrl: "http://localhost:3000/api/mcp",
  });
  return <McpPanel jaml={myJaml} />;
}
```

### 4. Use the CodeMirror 6 MCP extension

```tsx
import { EditorView } from "@codemirror/view";
import { mcpExtension } from "jaml-seed-lab/codemirror";
import { McpBrowserClient } from "jaml-seed-lab/mcp";

const client = new McpBrowserClient({
  serverUrl: "http://localhost:3000/api/mcp",
});

const view = new EditorView({
  extensions: [mcpExtension(client)],
  parent: document.body,
});
```

### 5. Use the catalog + registry in your own json-render app

```tsx
import { balatroCatalog } from "jaml-seed-lab/catalog";
import { registry } from "jaml-seed-lab/registry";
import { Renderer } from "@json-render/react";

<Renderer spec={mySpec} registry={registry} />
```

---

## The 4 Apps

### 1. JAML IDE

**Route:** `/ide`

The JAML language editor with full IDE support:

- **Code Editor** — Powered by `@codemirror` with JAML syntax highlighting, autocomplete, and lint
- **Visual Tab** — Live preview of JAML filters as visual card diagrams (jokers, cards, conditions)
- **LSP Panel** — Real-time diagnostics from `jaml-lang` (errors, warnings, completions)
- **MCP Panel** — Connect to the MCP server, execute tools, see results inline
- **Export/Save** — Copy JAML to clipboard, save to localStorage, export as JSON

```tsx
import { JamlIdePage } from "jaml-seed-lab/apps/ide";
export default JamlIdePage;
```

### 2. Home + Filters Browser

**Route:** `/`

The landing page and community hub:

- **Hero** — Animated Jimbo background, quick links to all 4 apps
- **Featured Filters** — Gallery of popular JAML filters from the community
- **Recent Seeds** — Your last viewed / analyzed seeds
- **Stats Dashboard** — Global stats: seeds searched, matches found, community size
- **Filter Browser** — Browse, filter, and fork community JAML filters

```tsx
import { JamlSeedLabHomePage } from "jaml-seed-lab/apps/home";
export default JamlSeedLabHomePage;
```

### 3. Seed Finder

**Route:** `/finder`

Requires a JAML filter to be loaded (from the IDE or direct input). Runs the Motely WASM engine on your CPU to search 2.3 trillion seeds.

- **JAML Input** — Inline JAML editor or import from IDE
- **Search Mode** — Random, Aesthetic, or Seedlist traversal
- **Real-time Results** — json-rendered `SeedCard` grid with live scoring
- **Search Stats** — Seeds/second, total searched, matches found, elapsed time
- **Export** — Copy seed, analyze in JAMLYZER, save to collection

```tsx
import { SeedFinderPage } from "jaml-seed-lab/apps/finder";
export default SeedFinderPage;
```

### 4. Seed Analyzer (JAMLYZER)

**Route:** `/analyzer`

Deep analysis of any Balatro seed. The JAMLYZER renders the full 8-ante route with json-render components.

- **Full Route** — All 8 antes + endless: bosses, shop queues, packs, tags, vouchers
- **Joker Timeline** — Every joker appearance by ante, with edition/seal/modifiers
- **Shop Visualization** — `ShopQueue` cards per ante with reroll costs
- **Boss Blinds** — `BossBlind` cards with debuff descriptions
- **Erratic Deck** — Special erratic deck analysis tab with suit/rank distribution
- **Export** — JSON, image, or shareable link

```tsx
import { JamlSeedAnalyzerPage } from "jaml-seed-lab/apps/analyzer";
export default JamlSeedAnalyzerPage;
```

---

## CLI

Install the CLI globally or use via `npx`:

```bash
npx jaml-seed-lab login          # Authenticate with npm
npx jaml-seed-lab whoami         # Show current npm user
npx jaml-seed-lab publish        # Build + publish to npm
npx jaml-seed-lab publish:beta   # Publish with beta tag
npx jaml-seed-lab dev            # Run Next.js dev server
npx jaml-seed-lab build          # Build Next.js app for production
npx jaml-seed-lab build:lib      # Build library bundle with Vite
npx jaml-seed-lab lint           # Run ESLint + TypeScript check
npx jaml-seed-lab init my-app    # Scaffold a new project
npx jaml-seed-lab help           # Show full help
```

### Publishing workflow

```bash
# 1. Authenticate (one-time)
jaml-seed login

# 2. Make changes, bump version in package.json
# 3. Build and publish
jaml-seed publish
```

---

## API Routes

All API routes are designed to be lightweight — the heavy lifting (WASM search/analysis) runs client-side.

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/search` | Plan a JAML seed search |
| `POST` | `/api/analyze` | Plan a seed analysis |
| `POST` | `/api/mcp` | MCP protocol endpoint (tools: `search_seeds`, `analyze_seed`, `analyze_erratic`) |
| `GET` | `/api/mcp` | MCP server metadata |

### MCP Tools

Connect to Claude, Cursor, or any MCP client:

```json
{
  "mcpServers": {
    "balatro-seed-lab": {
      "url": "https://your-app.vercel.app/api/mcp"
    }
  }
}
```

Available tools:
- `search_seeds` — Plan a JAML-based search
- `analyze_seed` — Plan a full route analysis
- `analyze_erratic` — Plan an erratic deck analysis

---

## MCP Integration

### CodeMirror 6 MCP Extension

A full CodeMirror 6 extension that adds MCP capabilities to the editor:

```tsx
import { mcpExtension } from "jaml-seed-lab/codemirror";

// Add to your CodeMirror 6 editor
const view = new EditorView({
  extensions: [
    mcpExtension(client), // Connects to MCP server
    // ... other extensions
  ],
});
```

**Features:**
- **State Field** — Tracks MCP connection state, tools, and results in the editor
- **Decorations** — Inline result widgets showing tool execution output
- **Status Panel** — Bottom panel showing connection status and available tools
- **Keybindings** — `Ctrl+Shift+M` to connect, `Ctrl+Shift+R` to clear results
- **Tool Execution** — Click a tool in the status panel to execute it with current JAML context

### MCP Panel

React component for MCP interaction outside of CodeMirror:

```tsx
import { McpPanel } from "jaml-seed-lab/mcp";

<McpPanel
  serverUrl="/api/mcp"     // MCP server endpoint
  apiKey="your-key"        // Optional auth
  jaml={currentJaml}       // Current JAML for tool context
/>
```

**Features:**
- Connect/disconnect to MCP server
- List available tools with descriptions
- Execute tools with auto-generated arguments from JAML context
- View results rendered via json-render
- Chat-style interaction log

### MCP App (Iframe)

The IDE can be embedded as an MCP App in any MCP client (Claude Desktop, etc.):

```tsx
import { McpAppWrapper } from "jaml-seed-lab/mcp";

// In your iframe route:
export default function McpAppPage() {
  return <McpAppWrapper />;
}
```

**Protocol:**
- Parent sends `{ type: "set-content", content: "..." }` to load JAML
- Parent sends `{ type: "tool-call", tool: "search_seeds", arguments: {...} }` to execute
- Child sends `{ type: "content-change", content: "..." }` when user edits
- Child sends `{ type: "tool-result", tool: "...", result: ... }` when tool completes

---

## Architecture

```
jaml-seed-lab/
├── app/                    # Next.js app (pages)
│   ├── page.tsx            # Home + Filters Browser
│   ├── ide/page.tsx        # JAML IDE
│   ├── finder/page.tsx     # Seed Finder
│   ├── analyzer/page.tsx   # JAMLYZER
│   ├── layout.tsx          # Root layout
│   └── api/                # API routes (search, analyze, mcp)
├── src/                    # Exportable library code
│   ├── index.ts            # Package entry point
│   ├── apps/               # 4 composable apps
│   │   ├── ide/            # JamlIdePage + JamlIde component
│   │   ├── home/           # HomePage + FilterBrowser + Layout
│   │   ├── finder/         # SeedFinderPage + SearchPanel
│   │   └── analyzer/       # JamlSeedAnalyzerPage + RouteViewer
│   ├── mcp/                # MCP integration
│   │   ├── client.ts       # Browser MCP client (HTTP transport)
│   │   ├── panel.tsx       # React MCP panel (tools + results)
│   │   ├── app.tsx         # MCP App wrapper (iframe postMessage)
│   │   └── index.ts        # Exports
│   ├── codemirror/         # CodeMirror 6 extensions
│   │   ├── mcp-extension.ts # Full MCP extension (state, decorations, panel, keymap)
│   │   └── index.ts        # Exports
│   ├── components/          # Shared UI components
│   ├── hooks/               # Custom hooks
│   └── lib/                 # (alias to ../lib)
├── lib/                     # Core library
│   ├── catalog.ts           # json-render catalog (Zod schemas)
│   ├── registry.tsx         # Component registry
│   └── spec-builder.ts      # Spec builders for results
├── bin/
│   └── cli.mjs              # jaml-seed CLI
├── package.json
├── next.config.ts
├── vite.lib.config.ts       # Vite build for library exports
└── README.md
```

### json-render Flow

```
User types JAML filter
    ↓
motely-wasm runs SIMD search on client CPU
    ↓
Search results → spec-builder.ts → JSON Spec tree
    ↓
json-render Renderer → React components via Registry
    ↓
Balatro cards rendered by jaml-ui JamlGameCard
    ↓
User clicks seed → ActionProvider → analyzeSeed
    ↓
AI generates new Spec for the full route
```

### MCP Flow

```
CodeMirror 6 Editor
    ↓ (user clicks tool or presses Ctrl+Shift+M)
McpBrowserClient.connect()
    ↓ HTTP Streamable transport
/api/mcp (Next.js route handler)
    ↓ MCP protocol
McpServer (tools registered)
    ↓
Tool execution result
    ↓
addMcpResult.of() dispatched to CodeMirror state
    ↓
McpResultWidget rendered inline in editor
```

### Tech Stack

| Layer | Technology |
|-------|------------|
| Framework | Next.js 16 (App Router) |
| Language | TypeScript 5 |
| Styling | Tailwind CSS 4 + jaml-ui Jimbo design system |
| Components | jaml-ui (JamlGameCard, JamlIde, JimboApp) |
| Generative UI | json-render (Catalog → Spec → Renderer) |
| Search Engine | motely-wasm (WebAssembly, SIMD) |
| JAML Language | jaml-lang (LSP, diagnostics, completions) |
| Editor | CodeMirror 6 + custom MCP extension |
| MCP | @modelcontextprotocol/sdk (Streamable HTTP) |
| CLI | Node.js ESM |

---

## Publishing

### Prerequisites

- Node.js ≥ 20.0.0
- npm ≥ 10.0.0
- npm account with 2FA enabled

### Steps

```bash
# 1. Clone and install
git clone https://github.com/OptimusPi/jaml-seed-lab.git
cd jaml-seed-lab
npm install

# 2. Authenticate
jaml-seed login

# 3. Bump version (edit package.json manually or use npm version)
npm version patch   # or minor / major

# 4. Build and publish
jaml-seed publish
```

The package is published with `--access public` so it's available to everyone immediately.

### Beta releases

```bash
npm version prerelease --preid=beta
jaml-seed publish:beta
```

Users install beta with: `npm install jaml-seed-lab@beta`

---

## Contributing

This is an open-source project for the Balatro community. PRs welcome!

### Development

```bash
# Run the full app locally
jaml-seed dev

# Build the library
jaml-seed build:lib

# Lint and type check
jaml-seed lint
```

### Ecosystem

- **MotelyJAML** — The seed engine: [github.com/OptimusPi/MotelyJAML](https://github.com/OptimusPi/MotelyJAML)
- **jaml-ui** — Balatro UI components: [github.com/OptimusPi/jaml-ui](https://github.com/OptimusPi/jaml-ui)
- **jaml-lang** — JAML language tooling: [npmjs.com/package/jaml-lang](https://www.npmjs.com/package/jaml-lang)
- **json-render** — Generative UI framework: [github.com/json-render/json-render](https://github.com/json-render/json-render)

---

## License

MIT © pifreak / OptimusPi

Not affiliated with LocalThunk or PlayStack. Made with ♥ for the Balatro community.

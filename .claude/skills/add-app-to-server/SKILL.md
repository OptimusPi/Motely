---
name: add-app-to-server
description: Use this skill when the user asks to "add a UI to an existing MCP server", "add MCP App support to my server", "enhance an MCP tool with a UI", or wants to integrate interactive views into an already-working MCP server without rebuilding from scratch.
---

# Add MCP App UI to Existing Server

Enrich an existing MCP server's tools with interactive UIs using the MCP Apps SDK (`@modelcontextprotocol/ext-apps`).

## How It Works

Existing tools get paired with HTML resources that render inline in the host's conversation. The tool continues to work for text-only clients — UI is an enhancement, not a replacement. Each tool that benefits from UI gets linked to a resource via `_meta.ui.resourceUri`, and the host renders that resource in a sandboxed iframe when the tool is called.

## Getting Reference Code

Clone the SDK repository for working examples and API documentation:

```bash
git clone --branch "v$(npm view @modelcontextprotocol/ext-apps version)" --depth 1 https://github.com/modelcontextprotocol/ext-apps.git /tmp/mcp-ext-apps
```

### API Reference (Source Files)

| File | Contents |
|------|----------|
| `src/app.ts` | `App` class, handlers (`ontoolinput`, `ontoolresult`, `onhostcontextchanged`, `onteardown`), lifecycle |
| `src/server/index.ts` | `registerAppTool`, `registerAppResource`, `getUiCapability`, tool visibility options |
| `src/spec.types.ts` | All type definitions: `McpUiHostContext`, CSS variable keys, display modes |
| `src/styles.ts` | `applyDocumentTheme`, `applyHostStyleVariables`, `applyHostFonts` |
| `src/react/useApp.tsx` | `useApp` hook for React apps |
| `src/react/useHostStyles.ts` | `useHostStyles`, `useHostStyleVariables`, `useHostFonts` hooks |

### Key Examples (Mixed Tool Patterns)

| Example | Pattern |
|---------|----------|
| `examples/map-server/` | `show-map` (App tool) + `geocode` (plain tool) |
| `examples/pdf-server/` | `display_pdf` (App tool) + `list_pdfs` (plain tool) + `read_pdf_bytes` (app-only tool) |
| `examples/system-monitor-server/` | `get-system-info` (App tool) + `poll-system-stats` (app-only polling tool) |

## Step 1: Analyze Existing Tools

Before writing any code, analyze the server's existing tools and determine which benefit from UI.

| Tool output type | UI benefit | Example |
|---|---|---|
| Structured data / lists / tables | High — interactive table, search, filtering | List of items, search results |
| Metrics / numbers over time | High — charts, gauges, dashboards | System stats, analytics |
| Media / rich content | High — viewer, player, renderer | Maps, PDFs, images, video |
| Simple text / confirmations | Low — text is fine | "File created", "Setting updated" |
| Data for other tools | Consider app-only | Polling endpoints, chunk loaders |

## Step 2: Add Dependencies

```bash
npm install @modelcontextprotocol/ext-apps
npm install -D vite vite-plugin-singlefile
```

Use `npm install` — never specify version numbers from memory.

## Step 3: Set Up the Build Pipeline

### Vite Configuration

```typescript
import { defineConfig } from "vite";
import { viteSingleFile } from "vite-plugin-singlefile";

export default defineConfig({
  plugins: [viteSingleFile()],
  build: {
    outDir: "dist",
    rollupOptions: {
      input: "mcp-app.html",
    },
  },
});
```

### HTML Entry Point

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>MCP App</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="./src/mcp-app.ts"></script>
  </body>
</html>
```

### Build Scripts

```json
{
  "scripts": {
    "build:ui": "vite build",
    "build:server": "tsc",
    "build": "npm run build:ui && npm run build:server",
    "serve": "tsx server.ts"
  }
}
```

## Step 4: Convert Tools to App Tools

**Before** (plain MCP tool):
```typescript
server.tool("my-tool", { param: z.string() }, async (args) => {
  const data = await fetchData(args.param);
  return { content: [{ type: "text", text: JSON.stringify(data) }] };
});
```

**After** (App tool with UI):
```typescript
import { registerAppTool, registerAppResource, RESOURCE_MIME_TYPE } from "@modelcontextprotocol/ext-apps/server";

const resourceUri = "ui://my-tool/mcp-app.html";

registerAppTool(server, "my-tool", {
  description: "Shows data with an interactive UI",
  inputSchema: { param: z.string() },
  _meta: { ui: { resourceUri } },
}, async (args) => {
  const data = await fetchData(args.param);
  return {
    content: [{ type: "text", text: JSON.stringify(data) }],
    structuredContent: { data },
  };
});
```

**Always keep the `content` array** with a text fallback for text-only clients.

## Step 5: Register Resources

```typescript
import fs from "node:fs/promises";
import path from "node:path";

registerAppResource(server, {
  uri: resourceUri,
  name: "My Tool UI",
  mimeType: RESOURCE_MIME_TYPE,
}, async () => {
  const html = await fs.readFile(
    path.resolve(import.meta.dirname, "dist", "mcp-app.html"),
    "utf-8",
  );
  return { contents: [{ uri: resourceUri, mimeType: RESOURCE_MIME_TYPE, text: html }] };
});
```

## Step 6: Build the UI

### Handler Registration (register ALL before `app.connect()`)

```typescript
import { App, PostMessageTransport, applyDocumentTheme, applyHostStyleVariables, applyHostFonts } from "@modelcontextprotocol/ext-apps";

const app = new App({ name: "My App", version: "1.0.0" });

app.ontoolinput = (params) => {
  // Render the UI using params.arguments and/or params.structuredContent
};

app.ontoolresult = (result) => {
  // Update UI with final tool result
};

app.onhostcontextchanged = (ctx) => {
  if (ctx.theme) applyDocumentTheme(ctx.theme);
  if (ctx.styles?.variables) applyHostStyleVariables(ctx.styles.variables);
  if (ctx.styles?.css?.fonts) applyHostFonts(ctx.styles.css.fonts);
  if (ctx.safeAreaInsets) {
    const { top, right, bottom, left } = ctx.safeAreaInsets;
    document.body.style.padding = `${top}px ${right}px ${bottom}px ${left}px`;
  }
};

app.onteardown = async () => { return {}; };

await app.connect(new PostMessageTransport());
```

### Host Styling

```css
.container {
  background: var(--color-background-secondary);
  color: var(--color-text-primary);
  font-family: var(--font-sans);
  border-radius: var(--border-radius-md);
}
```

Variable groups: `--color-background-*`, `--color-text-*`, `--color-border-*`, `--font-sans`, `--font-mono`, `--border-radius-*`.

## Optional Enhancements

### App-Only Helper Tools

```typescript
registerAppTool(server, "poll-data", {
  description: "Polls latest data for the UI",
  _meta: { ui: { resourceUri, visibility: ["app"] } },
}, async () => {
  const data = await getLatestData();
  return { content: [{ type: "text", text: JSON.stringify(data) }] };
});
```

### CSP Configuration

```typescript
registerAppResource(server, {
  uri: resourceUri,
  name: "My Tool UI",
  mimeType: RESOURCE_MIME_TYPE,
  _meta: {
    ui: {
      connectDomains: ["api.example.com"],
      resourceDomains: ["cdn.example.com"],
      frameDomains: ["embed.example.com"],
    },
  },
}, async () => { /* ... */ });
```

## Common Mistakes to Avoid

1. **Forgetting text `content` fallback** — Always include `content` array with text for non-UI hosts
2. **Registering handlers after `connect()`** — Register ALL handlers BEFORE calling `app.connect()`
3. **Missing `vite-plugin-singlefile`** — Without it, assets won't load in the sandboxed iframe
4. **Forgetting resource registration** — The tool references a `resourceUri` that must have a matching resource
5. **Hardcoding styles** — Use host CSS variables (`var(--color-*)`) for theme integration
6. **Not handling safe area insets** — Always apply `ctx.safeAreaInsets` in `onhostcontextchanged`

## Testing

```bash
# Terminal 1: Build and run your server
npm run build && npm run serve

# Terminal 2: Run basic-host
cd /tmp/mcp-ext-apps/examples/basic-host
npm install
SERVERS='["http://localhost:3001/mcp"]' npm run start
# Open http://localhost:8080
```

# Balatro Seed Lab

Next.js 16 + json-render + jaml-ui unified Balatro seed app.

## Setup

```bash
cd apps/balatro-seed-app
npm install
npm run dev
```

Open http://localhost:3000

## Deploy to Vercel

```bash
vercel --prod
```

## Architecture

- `/app/page.tsx` — Landing dashboard
- `/app/find/page.tsx` — Seed finder with JAML editor + json-render results
- `/app/analyze/page.tsx` — Seed analyzer (full route)
- `/app/erratic/page.tsx` — Erratic deck tools
- `/lib/catalog.ts` — json-render component catalog (Zod schemas)
- `/lib/registry.tsx` — Component registry mapping catalog → React components
- `/lib/spec-builder.ts` — Spec builders for search/analyze/erratic results
- `/app/api/search/route.ts` — Search plan API (client executes search)
- `/app/api/analyze/route.ts` — Analyze plan API (client executes analysis)

## json-render Flow

1. AI or code generates a JSON Spec constrained by the Catalog
2. The Renderer maps Spec elements to real React components via the Registry
3. User actions flow back through ActionProvider handlers
4. Components include jaml-ui primitives (JamlGameCard, JamlIde, JimboApp)

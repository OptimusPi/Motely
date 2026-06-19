# Plan: jaml-ui json-render v2 — Lean, Mean, UX Machine

## Principles
- **KISS**: No external deps for json-render. Pure TypeScript + React.
- **SOLID**: Single responsibility per component, open/closed registry, clean interfaces.
- **DRY**: Reuse CSS tokens, reuse layout patterns. BUT — copy a 5-line schema if it prevents 300-line abstraction drift. Pragmatic > dogmatic.
- **UX #1**: Every component answers "what does the user need RIGHT NOW?"

## Phase 1: Foundation (tonight)

### 1.1 CSS Tokens — ~80 lines
Design tokens as CSS custom properties. NO component classes. Just:
- Colors (eyedropped from Balatro)
- Spacing scale (4, 8, 12, 16, 24, 32)
- Typography (pixel font stack, sizes)
- Radii, shadows, transitions

Components use **inline style + CSS variables** or **minimal utility classes**. No BEM, no 8000-line file.

### 1.2 json-render Engine — ~150 lines
Zero deps. Core types:
```typescript
interface JsonNode {
  type: string;
  props?: Record<string, unknown>;
  children?: JsonNode[];
}

interface Registry {
  [key: string]: React.FC<any>;
}

function render(node: JsonNode, registry: Registry): React.ReactNode;
```

That's it. No zod, no `@json-render/core`, no `@json-render/react`.

### 1.3 Catalog — TypeScript interfaces, ~50 lines
```typescript
interface Catalog {
  Panel: { title?: string; variant?: 'default' | 'accent' };
  Stack: { gap?: number; align?: 'start' | 'center' | 'end' };
  Grid: { columns?: number; gap?: number };
  SeedCard: { seed: string; score?: number; jokers?: string[] };
  SearchStats: { status: string; seedsSearched?: string; matchesFound?: number };
  // ... only what we need
}
```

### 1.4 Component Registry — ~200 lines
Real React components mapped from catalog names. Each component:
- Accepts `props` (typed from catalog)
- Uses CSS tokens
- Has ONE job

## Phase 2: UX Components (tonight)

### 2.1 Layout Primitives
- `Panel` — bordered container with optional title
- `Stack` — vertical flex with gap
- `Grid` — CSS grid with configurable columns
- `Text` — styled text with variants (title, body, muted, accent)

### 2.2 Domain Components
- `SeedCard` — shows a seed, score, jokers. Tap to expand.
- `SearchStats` — live search metrics (seeds/sec, matches, status)
- `JokerBadge` — small joker name pill
- `ErrorBanner` — error state, dismissible
- `LoadingPulse` — skeleton/loading state

### 2.3 Game Card Integration
- `JamlGameCard` wrapper from existing jaml-ui (re-use, don't rebuild)
- Maps json-render props to JamlGameCard props

## Phase 3: Spec Builder (tonight)

```typescript
function buildSearchSpec(results: SearchResult[]): JsonNode;
function buildAnalyzerSpec(analysis: AnalysisResult): JsonNode;
```

Converts domain data into json-render trees. Clean, testable, pure functions.

## Phase 4: Integration

Wire into:
- `apps/balatro-seed-app` — Next.js app
- MCP iframe — json-render as the UI language
- Storybook — stories for each catalog component

## File Structure

```
packages/json-render/          ← NEW: zero-dep package
  src/
    types.ts                   # Core types (JsonNode, Registry, Catalog)
    engine.tsx                 # render() function
    catalog.ts                 # Balatro catalog definitions
    registry.tsx               # Component implementations
    components/
      layout.tsx               # Panel, Stack, Grid, Text
      domain.tsx               # SeedCard, SearchStats, JokerBadge, etc.
      game.tsx                 # JamlGameCard wrapper
    builders/
      search.ts                # buildSearchSpec()
      analyzer.ts              # buildAnalyzerSpec()
    index.ts                   # Barrel export
  package.json
  tsconfig.json

src/ui/jimbo-tokens.css        # ← REPLACES jimbo.css (80 lines, not 8000)
```

## Success Criteria
- [ ] `json-render` package has zero runtime deps (except React)
- [ ] CSS < 100 lines of tokens
- [ ] Catalog + Registry + Spec builder < 500 lines total
- [ ] Type-safe: `render(node, registry)` is fully typed
- [ ] UX: Search results render in < 100ms for 100 cards
- [ ] Build passes: typecheck, lint, storybook serves

## Stretch
- [ ] Animated transitions (framer-motion? CSS transitions? Keep it light.)
- [ ] Responsive: mobile-first grid (1 col → 2 → 3 → 4)
- [ ] Dark mode (Balatro IS dark mode, so... done?)

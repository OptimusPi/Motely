# MotelyWASM - Product Requirements Document

## Overview
Compile MotelyJSON to WebAssembly (WASM) so users can search for Balatro seeds **directly in their browser**. Host on Cloudflare Pages for free, global distribution.

## Why This Is INSANE 🔥
- 🌐 **Zero install** - Just visit a URL
- 🚀 **Fast** - WASM SIMD enabled (if browser supports)
- 💰 **Free hosting** - Cloudflare Pages
- 📱 **Works everywhere** - Desktop, mobile, tablets
- 🔒 **Private** - All computation in user's browser (no server)
- ⚡ **Instant feedback** - No network latency

## Technical Reality Check

### .NET WASM Status (2025)
- ✅ Blazor WebAssembly is mature
- ✅ .NET 10 has excellent WASM support
- ⚠️ WASM SIMD support exists but limited
- ⚠️ Single-threaded by default (Web Workers = complex)
- ❌ Full AVX-512 not available (fallback to Vector<T>)

### Performance Expectations
- **With WASM SIMD**: ~30-50% of native speed
- **Without WASM SIMD**: ~10-20% of native speed
- **Threads**: Likely single-threaded (good enough!)
- **Search 35M seeds**: ~30-60 seconds (vs ~5 seconds native with 16 threads)

**Verdict: Worth it!** For casual users, 30-60 seconds is fine.

## Architecture

### Project Structure
```
MotelyWASM/
├── MotelyWASM.Client/        # Blazor WASM project
│   ├── Pages/
│   │   ├── Index.razor       # Main search page
│   │   ├── JsonEditor.razor  # JSON config editor
│   │   └── Results.razor     # Search results
│   ├── Components/
│   │   ├── FilterBuilder.razor
│   │   └── ProgressBar.razor
│   └── wwwroot/
│       ├── index.html
│       └── styles.css
└── MotelyWASM.Shared/        # Shared code
    └── Models/
```

### Deployment
```
Cloudflare Pages
├── Build: dotnet publish -c Release
├── Output: bin/Release/net10.0/publish/wwwroot
└── Domain: motelyjs.pages.dev (or custom)
```

## User Interface Options

### Option 1: Simple Text Box (MVP)
```
┌─────────────────────────────────────────┐
│  MotelyJSON - Balatro Seed Searcher    │
│                                         │
│  Paste your JSON config:                │
│  ┌─────────────────────────────────┐   │
│  │ {                               │   │
│  │   "name": "Blueprint",          │   │
│  │   "must": [                     │   │
│  │     { "type": "Joker", ...      │   │
│  │   ]                             │   │
│  │ }                               │   │
│  └─────────────────────────────────┘   │
│                                         │
│  [Start Search]                         │
└─────────────────────────────────────────┘
```

### Option 2: Tree Grid Builder
```
┌─────────────────────────────────────────┐
│  Filter Builder                         │
│                                         │
│  + Add Filter                           │
│    ├─ [Joker      ▼]                   │
│    │   Name:  [Blueprint  ▼]           │
│    │   Antes: [☑1 ☑2 ☑3 ☐4 ☐5 ☐6]     │
│    │   Edition: [Any ▼]                │
│    │   Slots: [Any ▼]                  │
│    │                                    │
│    └─ [Remove]                          │
│                                         │
│  Search Settings:                       │
│    Deck: [Red Deck ▼]                  │
│    Stake: [White ▼]                    │
│                                         │
│  [Start Search] [Export JSON]          │
└─────────────────────────────────────────┘
```

### Option 3: Hybrid (Best)
- **Tab 1**: JSON Editor (Monaco Editor)
- **Tab 2**: Visual Builder (dropdowns/checkboxes)
- **Tab 3**: Templates (pre-built searches)
- **Sync both ways** - Edit JSON OR use builder

## Implementation Tasks

### Phase 1: Blazor WASM Setup (Non-Breaking)
- [ ] **Task 1.1**: Create new Blazor WASM project (net10.0)
- [ ] **Task 1.2**: Add project reference to Motely core library
- [ ] **Task 1.3**: Configure WASM build for SIMD support
  ```xml
  <PropertyGroup>
    <WasmEnableSIMD>true</WasmEnableSIMD>
    <RunAOTCompilation>true</RunAOTCompilation>
  </PropertyGroup>
  ```
- [ ] **Task 1.4**: Test basic Blazor app runs in browser

### Phase 2: Core Integration (Non-Breaking)
- [ ] **Task 2.1**: Verify Motely library compiles to WASM
- [ ] **Task 2.2**: Test MotelySearch runs in browser (single seed)
- [ ] **Task 2.3**: Profile performance (SIMD vs non-SIMD)
- [ ] **Task 2.4**: Implement fallback for unsupported CPU features

### Phase 3: Simple UI (MVP) (Non-Breaking)
- [ ] **Task 3.1**: Create JSON text box component
- [ ] **Task 3.2**: Parse JSON → MotelyJsonConfig in browser
- [ ] **Task 3.3**: Wire up "Start Search" button
- [ ] **Task 3.4**: Display progress (percentage, seeds/sec)
- [ ] **Task 3.5**: Display results in table

### Phase 4: Visual Filter Builder (Non-Breaking)
- [ ] **Task 4.1**: Create dropdown component for filter types
- [ ] **Task 4.2**: Build Joker filter UI
- [ ] **Task 4.3**: Build Soul Joker filter UI
- [ ] **Task 4.4**: Build Voucher/Planet/Tarot/Spectral UI
- [ ] **Task 4.5**: Convert UI selections → JSON
- [ ] **Task 4.6**: Sync JSON editor ↔ Visual builder

### Phase 5: Templates & Presets (Non-Breaking)
- [ ] **Task 5.1**: Create 10 popular search templates
- [ ] **Task 5.2**: Add "Load Template" dropdown
- [ ] **Task 5.3**: Allow users to save custom templates (localStorage)
- [ ] **Task 5.4**: Share template via URL (encode JSON in query param)

### Phase 6: Results & Export (Non-Breaking)
- [ ] **Task 6.1**: Sortable results table
- [ ] **Task 6.2**: "Copy seed" button for each result
- [ ] **Task 6.3**: Export results to CSV
- [ ] **Task 6.4**: "Analyze seed" detail view
- [ ] **Task 6.5**: Shareable results URL

### Phase 7: Advanced Features (Non-Breaking)
- [ ] **Task 7.1**: Add Monaco Editor for JSON editing (syntax highlighting, autocomplete)
- [ ] **Task 7.2**: JSON schema validation with inline errors
- [ ] **Task 7.3**: Dark mode toggle
- [ ] **Task 7.4**: Responsive design for mobile
- [ ] **Task 7.5**: PWA support (install as app)

### Phase 8: Performance Optimization (Non-Breaking)
- [ ] **Task 8.1**: Enable AOT compilation for smaller binary
- [ ] **Task 8.2**: Lazy load heavy components
- [ ] **Task 8.3**: Worker thread experimentation (if possible)
- [ ] **Task 8.4**: Streaming results (show results as found)
- [ ] **Task 8.5**: Pause/Resume search

### Phase 9: Cloudflare Deployment (Non-Breaking)
- [ ] **Task 9.1**: Set up Cloudflare Pages project
- [ ] **Task 9.2**: Configure build command: `dotnet publish -c Release`
- [ ] **Task 9.3**: Set output directory: `bin/Release/net10.0/publish/wwwroot`
- [ ] **Task 9.4**: Add custom domain (optional)
- [ ] **Task 9.5**: Enable caching/CDN optimization

### Phase 10: Analytics & Monitoring (Non-Breaking)
- [ ] **Task 10.1**: Add privacy-friendly analytics (Plausible/Umami)
- [ ] **Task 10.2**: Track search completions, errors
- [ ] **Task 10.3**: Performance monitoring (search duration)
- [ ] **Task 10.4**: Browser compatibility tracking

## WASM SIMD Research

### Enabling WASM SIMD
```xml
<!-- Motely.csproj -->
<PropertyGroup>
  <WasmEnableSIMD>true</WasmEnableSIMD>
  <RunAOTCompilation>true</RunAOTCompilation>
  <WasmStripILAfterAOT>true</WasmStripILAfterAOT>
</PropertyGroup>
```

### Browser Support (2025)
| Browser | WASM SIMD | Threads |
|---------|-----------|---------|
| Chrome 91+ | ✅ | ⚠️ Experimental |
| Firefox 89+ | ✅ | ⚠️ Experimental |
| Safari 16.4+ | ✅ | ❌ No |
| Edge 91+ | ✅ | ⚠️ Experimental |

**Strategy:** Ship with SIMD, graceful fallback for older browsers.

### Code Changes Needed
```csharp
// Check if SIMD is available
if (Vector.IsHardwareAccelerated)
{
    // Use Vector<T> operations (WASM SIMD)
}
else
{
    // Fallback to scalar operations
}
```

## UI Libraries

### Option 1: MudBlazor (Recommended)
```bash
dotnet add package MudBlazor
```
**Pros:**
- Material Design components
- Rich component library (tables, dialogs, etc)
- Great for data-heavy apps
- Responsive out of box

### Option 2: Radzen (Alternative)
```bash
dotnet add package Radzen.Blazor
```
**Pros:**
- More enterprise-focused
- Excellent data grid
- Theme builder

### Option 3: Blazorise
```bash
dotnet add package Blazorise.Bootstrap5
```
**Pros:**
- Bootstrap-based
- Lightweight
- Familiar styling

## Success Metrics
- ⚡ **Load time** < 3 seconds on 4G
- 🔍 **Search 1M seeds** in < 10 seconds
- 📦 **WASM binary** < 5MB gzipped
- ✅ **Works on 95%+ browsers** (SIMD fallback)
- 💯 **Lighthouse score** > 90

## Deployment Checklist
```bash
# Build for production
dotnet publish -c Release

# Test locally
cd bin/Release/net10.0/publish/wwwroot
python -m http.server 8080

# Deploy to Cloudflare Pages
# (Connect GitHub repo, auto-deploy on push)

# Custom domain (optional)
# motelyjs.com → Cloudflare Pages
```

## Marketing Copy
```
🎰 Balatro Seed Searcher - In Your Browser

Find the perfect seed in seconds.
No downloads. No setup. Just search.

✨ Features:
• Lightning-fast WASM-powered search
• Visual filter builder (no JSON needed!)
• Works on all devices
• 100% private (runs in your browser)

Start searching → [motelyjs.pages.dev]
```

## Risks & Mitigations
| Risk | Impact | Mitigation |
|------|--------|------------|
| SIMD not supported | Slow search | Graceful fallback, show warning |
| Single-threaded | Slower than desktop | Set expectations, "~30sec" messaging |
| Large WASM binary | Slow load | AOT compilation, tree shaking, lazy load |
| Mobile performance | Bad UX | Responsive design, reduce batch size |

---

**This is going to be SICK! 🚀 Browser-based seed searching FTW!**

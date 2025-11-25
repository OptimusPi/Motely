# MotelyJSON TUI - Product Requirements Document

## Overview
A Terminal User Interface (TUI) for MotelyJSON that provides an interactive, arrow-key driven experience for configuring and running seed searches without touching JSON files or CLI parameters.

## Why This Rocks
- **Zero friction onboarding** - New users can start searching without learning CLI syntax or JSON format
- **Discover features naturally** - Browse all filter options interactively
- **Fast workflow** - Arrow keys + Enter beats typing commands
- **Looks badass** - Modern TUI with colors, borders, and smooth navigation

## Target Platforms
- Windows 11
- macOS (latest)
- Linux (Ubuntu/Debian)

## Technical Stack
- **.NET 10 / C# 14**
- **Terminal.Gui** (https://github.com/gui-cs/Terminal.Gui) - Cross-platform TUI framework with mouse support
  - Alternative: **Spectre.Console** (simpler but less interactive)

## User Flow

```
┌─────────────────────────────────────────────┐
│  MotelyJSON - Balatro Seed Searcher v1.0   │
│                                             │
│  > Quick Search                             │
│    Custom Filter Builder                    │
│    Load JSON Config                         │
│    Settings                                 │
│    Exit                                     │
└─────────────────────────────────────────────┘
```

### Main Menu Options

#### 1. Quick Search
Pre-built popular searches:
- "Any Legendary Joker in Ante 1-3"
- "Blueprint + Brainstorm combo"
- "Perkeo + Observatory (Soul Joker)"
- "Custom..." → leads to Filter Builder

#### 2. Custom Filter Builder
Interactive wizard:

**Step 1: What are you looking for?**
```
[ ] Joker
[ ] Soul Joker
[ ] Voucher
[ ] Planet Card
[ ] Tarot Card
[ ] Spectral Card
[ ] Boss Blind
[ ] Tag
```

**Step 2: Configure selected items** (example for Joker)
```
Joker Type: [Blueprint      ▼]
Antes: [✓] 1  [✓] 2  [✓] 3  [ ] 4  [ ] 5  [ ] 6  [ ] 7  [ ] 8
Edition: [Any           ▼] (options: Any, Negative, Polychrome, Holographic, Foil)
Shop Slots: [Any        ▼] (or specific slot selection)

[Add Another Condition] [Start Search] [Cancel]
```

**Step 3: Search Settings**
```
Deck: [Red Deck       ▼]
Stake: [White Stake   ▼]
Threads: [16          ▼]
Max Results: [100      ]

[Start Search] [Back] [Save Config]
```

#### 3. Load JSON Config
File browser with preview:
```
┌─ JsonItemFilters/ ─────────────────┐
│ > PerkeoObservatory.json           │
│   BlueprintBrainstorm.json         │
│   test-aleeb-unit.json             │
│                                    │
│ Preview:                           │
│ {                                  │
│   "name": "Perkeo Observatory",    │
│   "must": [                        │
│     { "type": "SoulJoker", ...     │
└────────────────────────────────────┘
```

#### 4. Settings
- Default deck/stake
- Default thread count
- Color scheme
- Save location for configs

### Search Results View

```
┌─ Search Results ────────────────────────────────────────┐
│ Searching... [████████████████────────] 67% (23.4M/35M) │
│ Found: 42 seeds                                         │
│                                                         │
│ SEED       SCORE  DETAILS                               │
│ ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━  │
│ KJ8XPQW1   850    Perkeo (Ante 2), Observatory (A1)    │
│ 9MXLK2P3   820    Perkeo (Ante 1), Observatory (A2)    │
│ P7QWXM9K   800    Perkeo (Ante 3), Observatory (A1)    │
│                                                         │
│ [Copy Selected] [Analyze] [Export] [New Search]        │
└─────────────────────────────────────────────────────────┘
```

## Implementation Tasks

### Phase 1: Foundation (Non-Breaking)
- [ ] **Task 1.1**: Create new `Motely.TUI` project targeting net10.0
- [ ] **Task 1.2**: Add Terminal.Gui NuGet package
- [ ] **Task 1.3**: Create basic app shell with main menu (5 options)
- [ ] **Task 1.4**: Test cross-platform compatibility (Win/Mac/Linux)

### Phase 2: Quick Search (Non-Breaking)
- [ ] **Task 2.1**: Create `QuickSearchTemplates.cs` with 5 pre-built configs
- [ ] **Task 2.2**: Implement Quick Search menu view
- [ ] **Task 2.3**: Wire up template selection → MotelySearch execution
- [ ] **Task 2.4**: Create simple results display (list view)

### Phase 3: Load JSON Config (Non-Breaking)
- [ ] **Task 3.1**: Create file browser dialog
- [ ] **Task 3.2**: Implement JSON preview pane
- [ ] **Task 3.3**: Load selected JSON → MotelySearch
- [ ] **Task 3.4**: Error handling for invalid JSON

### Phase 4: Custom Filter Builder (Non-Breaking)
- [ ] **Task 4.1**: Create filter type selection screen (checkboxes)
- [ ] **Task 4.2**: Build Joker configuration wizard
- [ ] **Task 4.3**: Build Soul Joker configuration wizard
- [ ] **Task 4.4**: Build Voucher configuration wizard
- [ ] **Task 4.5**: Build Planet/Tarot/Spectral card wizards
- [ ] **Task 4.6**: Build Boss/Tag wizards
- [ ] **Task 4.7**: Create search settings screen
- [ ] **Task 4.8**: Convert TUI selections → MotelyJsonConfig
- [ ] **Task 4.9**: Option to save config as JSON file

### Phase 5: Search Execution & Results (Non-Breaking)
- [ ] **Task 5.1**: Create progress bar with live stats
- [ ] **Task 5.2**: Build results table view with sorting
- [ ] **Task 5.3**: Implement "Copy seed" functionality
- [ ] **Task 5.4**: Implement "Analyze seed" detail view
- [ ] **Task 5.5**: Export results to CSV/TXT

### Phase 6: Settings & Polish (Non-Breaking)
- [ ] **Task 6.1**: Create settings screen
- [ ] **Task 6.2**: Persist user preferences to config file
- [ ] **Task 6.3**: Add keyboard shortcuts help (F1)
- [ ] **Task 6.4**: Add color scheme options
- [ ] **Task 6.5**: Performance optimization for large result sets

### Phase 7: Advanced Features (Non-Breaking)
- [ ] **Task 7.1**: Search history (recent searches)
- [ ] **Task 7.2**: Favorites/bookmarks for configs
- [ ] **Task 7.3**: Batch search (multiple configs)
- [ ] **Task 7.4**: Search scheduler/background mode

## Libraries to Research

### Primary: Terminal.Gui
```bash
dotnet add package Terminal.Gui
```
**Pros:**
- Full TUI framework (dialogs, menus, windows)
- Mouse support
- Cross-platform
- Active development

**Cons:**
- Larger learning curve
- More dependencies

### Alternative: Spectre.Console
```bash
dotnet add package Spectre.Console
```
**Pros:**
- Simpler API
- Beautiful built-in components
- Great for wizards/prompts

**Cons:**
- Less interactive (no mouse)
- More linear flow

## Success Metrics
- Launch TUI with zero parameters → working UI in < 500ms
- Complete a Quick Search → results in < 3 seconds
- Build custom filter → save valid JSON in < 60 seconds
- Zero crashes on any supported platform

## Launch Plan
1. Ship Phase 1-3 as "MotelyJSON TUI Preview"
2. Gather feedback on UX flow
3. Complete Phase 4-5 for v1.0
4. Polish with Phase 6-7 based on usage

---

**Ready to rock this TUI! 🚀**

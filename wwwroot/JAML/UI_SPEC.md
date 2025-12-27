# JAML Web UI - Complete Specification

## Layout Structure

### Top Level
- **Top Center Tab** (48px height, fixed at top)
  - Contains: Home button, "JAML" label, Settings button
  - Position: Fixed at top center
  - Draggable: YES - dragging left/right resizes the vertical split
  - Visual: Standard tab appearance with border

### Main Layout (Below Top Tab)
- **Full Split Container** (flexbox, fills remaining space)
  - **Landscape Mode**: Side-by-side (row)
  - **Portrait Mode**: Stacked vertically (column)
  
  - **Left Half** (flex: 1)
    - Contains two sections stacked vertically:
      1. JAML Editor Section (red)
      2. Blueprint Analyzer Section (blue)
    
  - **Vertical Splitter** (in landscape only)
    - Position: Attached to top-center-tab (starts at 48px from top)
    - Width: 4px
    - Draggable: YES - resizes left/right halves
    - Visual: Simple line, can be invisible (drag via top-center-tab)
  
  - **Right Half** (flex: 1)
    - Contains one section:
      1. Results Section (purple)

## Section Structure

Each section follows this pattern:
```
.section-with-tab (wrapper)
  ├── .section-tab (grab bar, 16px total height)
  │   ├── 8px visible tab (with label)
  │   └── 8px invisible space above
  └── .panel-section (content area)
      └── [section content]
```

### Section Tab Specifications
- **Height**: 16px total
  - 8px visible tab (with label and ≡ icon)
  - 8px invisible space above (for stacking when collapsed)
- **Position**: `position: absolute`, `top: -16px` (attached to section)
- **Behavior**: 
  - Drag to resize section height (vertical)
  - Double-click to collapse/expand
  - NO click-to-teleport (removed)
  - NO floating (always attached to parent section)
- **Visual**: 
  - Colored background (red/blue/purple)
  - ≡ icon on left
  - Label text
  - NO borders (clean look)
  - Clip-path for manila folder effect

### Section Border Specifications
- **Border Style**: 2px double border effect
  - 1px outer border (normal color: red/blue/purple)
  - 1px inner border via box-shadow (dark variant)
- **Top Border**: 8px solid (creates space for tab)
- **Spacing**: 4px margin between panels
- **Border Radius**: Rounded corners (8px)

## Section Details

### 1. JAML Editor Section (Red)
- **Tab**: Red background, "JAML" label, left-aligned
- **Content**: Text editor (Monaco/plain textarea)
- **Resize Behavior**: 
  - Can drag tab up to cover top-center-tab (for mobile keyboard)
  - Stops 8px from screen top (to show tab)
  - Can drag down to reveal buttons
- **Min Height**: 100px

### 2. Blueprint Analyzer Section (Blue)
- **Tab**: Blue background, "Blueprint Analyzer" label, right-aligned
- **Content**: Blueprint iframe
- **Resize Behavior**: 
  - Drag tab to resize height
  - Stops 8px from screen top when dragged all the way up
- **Default Height**: 180px

### 3. Results Section (Purple)
- **Tab**: Purple background, "Results" label with badge, left-aligned
- **Content**: Search controls and results table
- **Resize Behavior**: 
  - Drag tab to resize height
  - Stops 8px from screen top when dragged all the way up
- **Flex**: 1 (takes remaining space)

## Responsive Behavior

### Landscape Mode (width > height)
- **Layout**: Side-by-side (left/right split)
- **Vertical Splitter**: Visible, attached to top-center-tab
- **Top-Center-Tab**: Controls vertical splitter position
- **Sections**: Stacked vertically within each half

### Portrait Mode (height > width)
- **Layout**: Stacked vertically (all sections in one column)
- **Vertical Splitter**: Hidden (width: 0)
- **Top-Center-Tab**: Still visible, but splitter disabled
- **Sections**: All stack vertically in order:
  1. JAML Editor
  2. Blueprint Analyzer
  3. Results

## Drag & Resize Rules

### Vertical Resizing (Section Tabs)
- **Drag Direction**: Up/down
- **Min Height**: 100px for sections
- **Max Height**: No limit (can fill screen)
- **Top Constraint**: When dragged to top, stops 8px from screen top
  - This leaves 8px space for the tab (16px total: 8px tab + 8px space)
- **Update Rate**: ~10 FPS (100ms throttle) during drag
- **Visual Feedback**: Real-time resize (content updates during drag)

### Horizontal Resizing (Top-Center-Tab / Vertical Splitter)
- **Drag Direction**: Left/right
- **Min Width**: 200px for each half
- **Max Width**: No limit
- **Update Rate**: Real-time (no throttle needed)
- **Visual Feedback**: Real-time resize

## Visual Design Rules

### Colors
- **Red**: `#ff4c40` (JAML Editor)
- **Blue**: `#0093ff` (Blueprint)
- **Purple**: `#9b59b6` (Results)
- **Dark Variants**: Used for inner borders
  - Red: `#a02721`
  - Blue: `#0057a1`
  - Purple: `#5D3570`

### Borders
- **Outer**: 1px solid (normal color)
- **Inner**: 1px via box-shadow inset (dark variant)
- **Top**: 8px solid (for tab space)
- **Spacing**: 4px margin between panels

### Tabs
- **Height**: 16px total (8px visible + 8px space)
- **Padding**: 4px 12px 4px 8px (left padding smaller for icon)
- **Icon**: ≡ (three horizontal lines)
- **Font**: m6x11plus, 12px, bold
- **No Borders**: Clean colored background only

### Backgrounds
- **Main**: `#33464b` (no gray bars)
- **Panels**: `#3a5055`
- **Dark**: `#1e2b2d`

## JavaScript Functions Required

1. **initTopCenterTab()**
   - Makes top-center-tab draggable left/right
   - Resizes left-half and right-half flex-basis
   - Updates vertical splitter position

2. **initFilterSectionDrag()**
   - Makes JAML section tab draggable up/down
   - Allows covering top-center-tab
   - Stops 8px from screen top
   - Throttles updates to ~10 FPS

3. **initCollapsibleDrag()**
   - Makes Blueprint/Results section tabs draggable
   - Resizes section heights
   - Stops 8px from screen top
   - Throttles updates to ~10 FPS

4. **updateSectionTabPositions()**
   - Ensures tabs stay attached (position: absolute)
   - Removes any fixed positioning
   - Called on resize/orientation change

5. **toggleSection()**
   - Double-click handler for collapse/expand
   - Toggles .collapsed class
   - Updates flex values

## HTML Structure

```html
<div class="app-layout">
  <div class="top-center-tab">
    <button onclick="goHome()">Home</button>
    <span>JAML</span>
    <button onclick="toggleWidgetSettings()">Settings</button>
  </div>
  
  <div class="full-split">
    <div class="left-half">
      <div class="half-content">
        <div class="section-with-tab">
          <span class="section-tab section-tab-red">JAML</span>
          <div id="jamlEditorSection" class="panel-section">...</div>
        </div>
        <div class="section-with-tab">
          <span class="section-tab section-tab-blue">Blueprint Analyzer</span>
          <div id="blueprintSection" class="panel-section">...</div>
        </div>
      </div>
    </div>
    
    <div class="full-splitter"></div>
    
    <div class="right-half">
      <div class="half-content">
        <div class="section-with-tab">
          <span class="section-tab section-tab-purple">Results</span>
          <div id="resultsSection" class="panel-section">...</div>
        </div>
      </div>
    </div>
  </div>
</div>
```

## Key Constraints

1. **NO floating tabs** - Always `position: absolute` relative to `.section-with-tab`
2. **NO duplicate lines** - Single border per section, no grab-bar visual lines
3. **NO teleporting** - Tabs stay attached to their sections
4. **NO click-to-collapse** - Only double-click works
5. **Clean borders** - 2px double border effect (1px outer + 1px inner)
6. **Proper spacing** - 4px between panels, 8px top border for tabs
7. **Mobile-friendly** - Portrait mode stacks vertically, JAML editor can cover top buttons

## Implementation Checklist

- [ ] Clean HTML structure (no duplicate grab-bars)
- [ ] CSS for sections with proper borders
- [ ] CSS for tabs (attached, no floating)
- [ ] CSS for responsive layout (landscape/portrait)
- [ ] JavaScript for top-center-tab drag (horizontal split)
- [ ] JavaScript for section tab drag (vertical resize)
- [ ] JavaScript for double-click collapse
- [ ] JavaScript to prevent tab floating
- [ ] Remove all grab-bar elements from HTML
- [ ] Remove all grab-tab CSS (or hide it)
- [ ] Test landscape mode
- [ ] Test portrait mode
- [ ] Test drag to top (8px constraint)
- [ ] Test drag to resize
- [ ] Test double-click collapse



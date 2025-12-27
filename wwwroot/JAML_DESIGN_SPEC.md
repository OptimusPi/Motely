# JAML UI - Complete Design Specification

## Purpose
A web-based interface for creating, editing, and testing JAML filter configurations for Balatro seed searching.

## Core Functionality

### Primary Features
1. **JAML Editor** - Edit filter configurations in YAML format
2. **Blueprint Integration** - Analyze seeds using external Blueprint tool
3. **Search Execution** - Start/stop seed searches with real-time results
4. **Results Display** - View and export search results in a table format
5. **Filter Management** - Load, save, and organize filter configurations

## Layout Structure

### Top Bar (JAML Logo/Splitter)
- **Position**: Fixed at top center of viewport
- **Height**: 48px
- **Content**:
  - Home button (left)
  - **JAML Logo** (center) - Draggable splitter that controls layout mode
  - Settings button (right)
- **Behavior**: Drag left/right to switch between vertical stack and horizontal split layouts
- **Layout Control**: 
  - **Left position**: Forces vertical stack layout (all panels full width)
  - **Center position**: Auto-detect based on screen size
  - **Right position**: Forces horizontal split layout (panels side-by-side)
- **Visual**: Clean bar with border, splitter shows orientation indicators

### Main Layout (Xbox 360 Blades Style)

#### Core Principles
- **Panel Borders**: Colored borders always take full width of current layout container
- **Anchored Borders**: Each panel's colored border naturally anchors to content above/below
- **Modular Lego-Bricks**: Panels stack like building blocks with seamless connections
- **Full-Width Borders**: Borders span entire width (single column) or full side (split layout)

#### Vertical Stack Mode (Portrait/Mobile/Forced)
- **Structure**: Single-column stack of full-width panels
- **Panel Flow**: Each panel's colored border touches the one above/below
- **No Splitter**: Top bar controls layout mode, not internal panel sizes
- **Responsive**: All panels always visible, collapsible via tabs

#### Horizontal Split Mode (Landscape/Forced)
- **Structure**: Two-column layout with panels distributed left/right
- **Panel Distribution**: User-assignable (drag panels between columns)
- **Column Borders**: Each column's panels maintain full-width borders within column
- **Splitter Control**: Top bar controls mode switching, not column widths

## Panel Structure (Manilla Envelope Tabs)

### Tab Design (Inspired by Office Supplies)
- **Appearance**: Tab-like extensions like manilla envelope labels
- **Height**: Same as grab bars (8px visible + 8px invisible space)
- **Position**: Attached to top of panel's colored border
- **Behavior**: Click to select panel, drag to move/reorder

### Colored Border System
- **Full Width**: Always spans entire width of current layout container
- **Anchored**: Top border connects to content above, bottom to content below
- **Color Coding**: Red (#ff4c40), Blue (#0093ff), Purple (#9b59b6)
- **Grab Bars**: Built-in resize handles at top/bottom of colored border

### Panel Collapse Behavior
- **Trigger**: Double-click tab OR drag bottom edge all the way up
- **Effect**: Panel collapses to tab-only, neighboring panels expand to fill space
- **Tab Stacking**: Collapsed tabs arrange left-to-right without overlap
- **Visual**: Seamless connection where colored borders meet

### Grab Bar Rules
- **Location**: Top/bottom edges of each panel's colored border
- **Availability**: Only available when there's content above/below to resize against
- **Top Panel**: No top grab bar (would be pointless)
- **Bottom Panel**: No bottom grab bar (nothing below to resize)
- **Collapsed Panels**: Grab bars disappear, only tabs remain

## Panel Management

### Drag & Drop
- **Panel Movement**: Drag tabs to reorder panels within columns or move between columns
- **Visual Feedback**: Highlight drop zones, show insertion points
- **Column Assignment**: Drag panels between left/right columns in split mode
- **Tab Stacking**: When multiple panels collapsed, tabs arrange horizontally

### Layout Switching
- **Top Bar Control**: Drag JAML logo to switch between stack/split modes
- **Mode Persistence**: Layout preference saved to localStorage
- **Smooth Transitions**: Animate between stack and split layouts
- **Panel Preservation**: Panel order and collapse states maintained during mode switches

## Visual Design (Blades Dashboard Inspired)

### Color-Coded Panels
- **JAML Editor**: Red borders and tabs
- **Blueprint**: Blue borders and tabs  
- **Results**: Purple borders and tabs
- **Background**: Dark teal (#33464b)
- **Content Areas**: Medium gray (#3a5055)

### Border System
- **Thickness**: 8px top border (creates tab space), 2px sides/bottom
- **Color**: Section-specific color matching tab
- **Connection**: Borders touch seamlessly between stacked panels
- **Grab Integration**: Resize handles built into border edges

### Tab Styling
- **Shape**: Tab-like extensions protruding from panel borders
- **Positioning**: Left-aligned in stack mode, side-appropriate in split mode
- **Interaction**: Hover effects, drag feedback
- **Typography**: Monospace font, clear labels

## Interaction Model

### Layout Philosophy
- **Blades Style**: Clean, modular, stackable panels like Xbox 360 dashboard
- **Manilla Envelope**: Tab design inspired by office organization
- **Lego Bricks**: Modular building blocks that connect seamlessly
- **Full-Width Borders**: Always utilize available horizontal space

### Panel States
- **Expanded**: Full content visible with colored border frame
- **Collapsed**: Tab-only, content hidden, border collapsed
- **Dragging**: Semi-transparent with visual feedback
- **Resizing**: Live feedback with snap points and constraints

### User Choice & Control
- **Layout Mode**: Top bar splitter controls stack vs split
- **Panel Order**: Drag tabs to reorder within/between columns
- **Panel Visibility**: Double-click or drag to collapse/expand
- **Column Assignment**: Drag panels between left/right in split mode

### 1. JAML Editor Panel
- **Purpose**: Edit JAML filter configuration
- **Visual Identity**: Red colored border and tab
- **Tab Label**: "JAML" with manilla envelope style
- **Content**:
  - Text editor (textarea with syntax highlighting)
  - Filter selection dropdown
  - Action buttons (Start Search, Stop Search)
  - Status indicators
- **Border Behavior**:
  - Full-width red border in stack mode
  - Full-column-width red border in split mode
  - Anchors to content above/below seamlessly
- **Grab Bars**: Bottom grab bar available (for resizing against panels below)

### 2. Blueprint Analyzer Panel
- **Purpose**: Analyze seeds using Blueprint tool
- **Visual Identity**: Blue colored border and tab
- **Tab Label**: "Blueprint Analyzer" with manilla envelope style
- **Content**:
  - Seed input field
  - Embedded iframe showing Blueprint tool
  - Analyze button
- **Border Behavior**:
  - Full-width blue border in stack mode
  - Full-column-width blue border in split mode
  - Anchors to content above/below seamlessly
- **Grab Bars**: Top and bottom grab bars (can resize against neighboring panels)

### 3. Results Panel
- **Purpose**: Display search results and controls
- **Visual Identity**: Purple colored border and tab
- **Tab Label**: "Results" with count badge, manilla envelope style
- **Content**:
  - Search controls (seed source, batch settings)
  - Results table (sortable columns)
  - Export button
  - Clear button
  - Progress indicators
- **Border Behavior**:
  - Full-width purple border in stack mode
  - Full-column-width purple border in split mode
  - Anchors to content above/below seamlessly
- **Grab Bars**: Top grab bar available (for resizing against panels above)

## Panel Headers

### Tab Design (Manilla Envelope Style)
- **Shape**: Tab-like protrusion from top border, like office envelope labels
- **Dimensions**: 8px visible height + 8px invisible space above
- **Position**: Attached to top of panel's colored border
- **Content**: Section label + visual grab indicator
- **Behavior**: Click to select, drag to move/reorder panels

### Colored Border System (Full-Width)
- **Width**: Always spans full width of layout container
- **Thickness**: 8px top (tab space), 2px sides, 2px bottom
- **Color**: Section-specific (Red/Blue/Purple)
- **Anchoring**: Top connects to content above, bottom to content below
- **Grab Integration**: Resize handles built into top/bottom edges

### Interaction Behavior
- **Tab Click**: Select panel, bring to front if needed
- **Tab Drag**: Move panel within column or between columns
- **Border Grab**: Resize panel height against neighbors
- **Double-Click Tab**: Collapse/expand panel

## Panel Drag & Rearrange

### Visual Feedback (Xbox 360 Blades Style)
- **Dragging State**: Panel becomes semi-transparent with slight rotation
- **Drop Indicators**: Blue line shows insertion point between panels
- **Column Boundaries**: Visual feedback when crossing between columns
- **Tab Stacking Preview**: Shows how collapsed tabs will arrange

### Drag Rules (Modular Lego-Brick System)
- **Within Column**: Drag tabs to reorder panels vertically
- **Between Columns**: Drag across column boundary to move panels
- **Full-Width Constraint**: Panels always maintain full width of their column
- **Seamless Connection**: Borders snap together when reordering
- **Persistence**: Panel order and column assignment saved to localStorage

## Top Bar Splitter (Layout Mode Control)

### Visual Design
- **Center Logo**: "JAML" text with grab handles
- **Position Indicators**: 
  - Left: ⋮⋮⋮ (stack mode)
  - Center: JAML (auto mode)  
  - Right: ⋮⋮⋮ (split mode)
- **Grab Feedback**: Shows orientation when dragging to edges

### Interaction Behavior
- **Drag Range**: Snaps to left/center/right positions
- **Left Snap**: Forces vertical stack layout
- **Center Snap**: Auto-detect based on screen dimensions
- **Right Snap**: Forces horizontal split layout
- **Smooth Transition**: Animate between layout modes
- **State Persistence**: Layout preference saved to localStorage

## Layout Mode Switching

### Stack Mode (Vertical)
- **Trigger**: Top bar dragged left OR auto-detect portrait/mobile
- **Panel Layout**: Single column, full-width panels
- **Tab Position**: Left-aligned tabs
- **Border Behavior**: Full viewport width, seamless vertical stacking
- **Grab Bars**: Available between panels for height adjustment

### Split Mode (Horizontal)
- **Trigger**: Top bar dragged right OR auto-detect landscape
- **Panel Layout**: Two columns with user-assignable panels
- **Tab Position**: Left column tabs left-aligned, right column tabs right-aligned
- **Border Behavior**: Full column width within each side
- **Column Management**: Drag panels between left/right columns

### Mode Transitions
- **Animation**: Smooth morphing between stack and split layouts
- **Panel Preservation**: Order and collapse states maintained
- **Responsive**: Auto-switches based on screen size (unless user forced)
- **Mobile**: Always stack mode for touch optimization

## Visual Design (Blades Dashboard Inspired)

### Color Scheme
- **JAML Editor**: Red (#ff4c40) - borders, tabs, accents
- **Blueprint**: Blue (#0093ff) - borders, tabs, accents
- **Results**: Purple (#9b59b6) - borders, tabs, accents
- **Background**: Dark teal (#33464b)
- **Panel Content**: Medium gray (#3a5055)
- **Grab Bars**: Subtle dark gray with hover highlights

### Border System (Lego-Brick Connection)
- **Thickness**: 8px top (tab space), 2px sides/bottom
- **Color Matching**: Tab and border share section color
- **Seamless Joints**: Borders connect without visible gaps
- **Grab Integration**: Resize handles at border edges
- **Shadow Effects**: Subtle depth for layered appearance

### Tab Styling (Manilla Envelope)
- **Shape**: Tab protrusion from panel border
- **Dimensions**: Compact but touch-friendly
- **Typography**: Monospace, clear section labels
- **Interaction States**: Hover, drag, selected highlights
- **Positioning Logic**: Context-aware (left/right/center based on layout)

### Typography
- **Primary Font**: m6x11plus monospace (Balatro aesthetic)
- **Tab Labels**: Bold, section-colored text
- **Body Text**: System default, readable contrast
- **Status Indicators**: Monospace for technical data

## Interaction Requirements

### Panel Management Rules
- **Selection**: Click tab to select panel
- **Movement**: Drag tab to reorder or move between columns
- **Resizing**: Grab border edges to adjust panel heights
- **Collapsing**: Double-click tab or drag bottom to top
- **Expansion**: Click collapsed tab to restore

### Layout Control Philosophy
- **User Choice**: Top bar splitter gives explicit control
- **Responsive Defaults**: Auto-switch based on screen size
- **Touch Friendly**: Large touch targets on mobile
- **State Persistence**: All user choices remembered

### Drag & Resize Constraints
- **Panel Order**: Maintain logical workflow order by default
- **Minimum Heights**: 100px expanded, tab-only when collapsed
- **Maximum Flexibility**: No arbitrary height limits
- **Column Balance**: Encourage balanced left/right distribution
- **Touch Prevention**: Disable text selection during drag operations

## State Management

### Layout State
- **Mode**: stack/split/auto
- **Panel Order**: Array of panel IDs in display order
- **Column Assignment**: Which panels in left/right columns
- **Collapse States**: Which panels are minimized
- **Panel Heights**: Custom heights for expanded panels

### Persistence Strategy
- **localStorage**: All layout state automatically saved
- **Auto-Restore**: State restored on page load
- **Migration**: Handle schema changes gracefully
- **Performance**: Debounced saves to prevent spam

### Responsive Behavior
- **Breakpoint Detection**: width > height = landscape/split candidate
- **Mobile Overrides**: Always stack on touch devices
- **Orientation Changes**: Smooth adaptation to rotation
- **Window Resize**: Live layout adjustments

## Performance Requirements
- **Smooth Dragging**: 60 FPS during all drag operations
- **Layout Transitions**: Smooth animations between modes
- **State Saves**: Debounced to prevent performance hits
- **Memory Management**: Efficient DOM manipulation
- **Touch Optimization**: Optimized for mobile performance

## Accessibility (Blades-Style UX)
- **Keyboard Navigation**: Tab through panels, enter to expand
- **Screen Reader**: Proper ARIA labels for panels and controls
- **Touch Targets**: Minimum 44px for mobile interaction
- **Focus Management**: Logical tab order through interface
- **High Contrast**: Sufficient color contrast ratios

## Browser Compatibility
- **Modern Browsers**: Chrome, Firefox, Safari, Edge
- **Mobile**: iOS Safari, Chrome Mobile, Samsung Internet
- **Touch Support**: Full touch gesture support
- **Responsive**: Adapts to any screen size/orientation

## Implementation Notes

### Framework Architecture
- **Alpine.js**: Reactive state management for UI
- **Vanilla JS**: Custom drag/drop and layout logic
- **CSS Grid/Flexbox**: Modern layout system
- **localStorage API**: State persistence

### Drag & Drop Implementation
- **Native Events**: mousedown/mousemove/mouseup for precision
- **Touch Events**: Pointer events for mobile compatibility
- **Collision Detection**: Element position calculations
- **Visual Feedback**: CSS transforms, opacity, shadows

### Layout Algorithm
- **Mode Detection**: Based on screen size and user preference
- **Panel Distribution**: Smart defaults with user override
- **Responsive Rules**: Breakpoint-based layout switching
- **Animation System**: Smooth transitions between states

## Future Enhancements
- **Advanced Layouts**: Custom column configurations
- **Panel Templates**: Save/load panel arrangements
- **Touch Gestures**: Multi-touch panel manipulation
- **Animation Presets**: Custom transition effects
- **Accessibility Audit**: WCAG 2.1 AA compliance
- **Performance Profiling**: Optimize for large panel sets


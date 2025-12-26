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

### Top Bar
- **Position**: Fixed at top center of viewport
- **Height**: 48px
- **Content**: 
  - Home button (left)
  - "JAML" label (center)
  - Settings button (right)
- **Behavior**: Draggable left/right to adjust vertical split between left and right panels
- **Visual**: Tab-like appearance with border

### Main Layout

#### Landscape Mode (width > height)
- **Structure**: Two-column layout
  - **Left Column**: Contains JAML Editor and Blueprint Analyzer (stacked vertically)
  - **Vertical Divider**: 4px wide, draggable, positioned between columns
  - **Right Column**: Contains Results panel
- **Responsive**: Columns resize proportionally, minimum 200px each

#### Portrait Mode (height > width)
- **Structure**: Single-column layout
  - All sections stack vertically in order:
    1. JAML Editor
    2. Blueprint Analyzer
    3. Results
- **Vertical Divider**: Hidden/disabled

## Sections

### 1. JAML Editor Section
- **Purpose**: Edit JAML filter configuration
- **Visual Identity**: Red theme
- **Tab Label**: "JAML"
- **Content**:
  - Text editor (supports plain text or syntax highlighting)
  - Filter selection dropdown
  - Action buttons (Start Search, Stop Search)
  - Status indicators
- **Resize Behavior**:
  - Tab can be dragged up/down to adjust section height
  - Minimum height: 100px
  - Can expand to cover top bar (for mobile keyboard access)
  - Stops 8px from viewport top when dragged up

### 2. Blueprint Analyzer Section
- **Purpose**: Analyze seeds using Blueprint tool
- **Visual Identity**: Blue theme
- **Tab Label**: "Blueprint Analyzer"
- **Content**:
  - Seed input field
  - Embedded iframe showing Blueprint tool
  - External link button
- **Resize Behavior**:
  - Tab can be dragged up/down to adjust section height
  - Default height: 180px
  - Minimum height: 100px
  - Stops 8px from viewport top when dragged up

### 3. Results Section
- **Purpose**: Display search results and controls
- **Visual Identity**: Purple theme
- **Tab Label**: "Results" (with count badge)
- **Content**:
  - Search controls (seed source, batch settings)
  - Results table (sortable columns)
  - Export button
  - Clear button
  - Progress indicators
- **Resize Behavior**:
  - Tab can be dragged up/down to adjust section height
  - Takes remaining space by default (flex: 1)
  - Minimum height: 100px
  - Stops 8px from viewport top when dragged up

## Section Tabs

### Visual Design
- **Total Height**: 16px
  - 8px visible tab area
  - 8px invisible space above (for stacking when collapsed)
- **Position**: Attached to top of section (absolute positioning)
- **Content**: 
  - Three-line icon (≡) on left
  - Section label text
- **Styling**: Colored background matching section theme, no borders
- **Font**: Monospace, bold, 12px

### Interaction Behavior
- **Vertical Drag**: Resize section height (up/down)
- **Double-Click**: Collapse/expand section
- **Constraints**:
  - Minimum section height: 100px
  - Maximum: No limit (can fill viewport)
  - Top constraint: 8px from viewport top
- **Update Rate**: Smooth real-time updates during drag

## Visual Design

### Color Scheme
- **JAML Editor**: Red (#ff4c40)
- **Blueprint**: Blue (#0093ff)
- **Results**: Purple (#9b59b6)
- **Background**: Dark teal (#33464b)
- **Panels**: Slightly lighter (#3a5055)
- **Dark accents**: Darker variant (#1e2b2d)

### Borders
- **Style**: Double border effect (2px total)
  - Outer: 1px solid (section color)
  - Inner: 1px (darker variant via shadow)
- **Top Border**: 8px solid (creates space for tab)
- **Spacing**: 4px margin between sections
- **Corners**: Rounded (8px radius)

### Typography
- **Primary Font**: Monospace (m6x11plus when available)
- **Tab Labels**: 12px, bold
- **Body Text**: System default, readable size

## Interaction Requirements

### Drag & Resize Rules

#### Horizontal Resize (Top Bar / Vertical Splitter)
- **Trigger**: Drag top bar left/right OR drag vertical divider
- **Effect**: Adjusts left/right column widths proportionally
- **Constraints**: 
  - Minimum column width: 200px each
  - Maximum: No limit
- **Update**: Real-time, smooth
- **Conflict Prevention**: Only one horizontal resize handler active at a time

#### Vertical Resize (Section Tabs)
- **Trigger**: Drag section tab up/down
- **Effect**: Adjusts section height within its column
- **Constraints**:
  - Minimum: 100px per section
  - Maximum: No limit
  - Top: 8px from viewport top
- **Update**: Throttled to ~10 FPS during drag
- **Conflict Prevention**: Only one vertical resize handler active at a time

### State Management
- **Resize State**: Track which element is being resized (none, horizontal, or vertical)
- **Prevent Conflicts**: When one resize starts, disable others
- **Save State**: Persist resize positions to localStorage
- **Restore State**: Load saved positions on page load

## Functional Requirements

### JAML Editor
- Load filter from dropdown
- Edit filter text
- Validate JAML syntax
- Save filter to disk
- Auto-save draft to localStorage
- Support syntax highlighting (optional)

### Search Execution
- Start search with current filter
- Stop running search
- Display real-time progress
- Show seeds searched, results found, speed
- Handle errors gracefully
- Support multiple concurrent searches (future)

### Results Display
- Show results in sortable table
- Display columns: seed, score, tallies
- Export to CSV
- Clear results
- Pagination or virtualization for large result sets
- Real-time updates via SignalR

### Blueprint Integration
- Enter seed value
- Load Blueprint tool in iframe
- Navigate to seed analysis
- Open in new tab
- Handle iframe sandbox restrictions

### Filter Management
- List all available filters
- Group by author
- Search/filter list
- Load filter into editor
- Save current filter
- Delete filter
- Share filter via URL parameter (?filter=id)

## Responsive Behavior

### Breakpoints
- **Landscape**: width > height → Two-column layout
- **Portrait**: height > width → Single-column layout

### Mobile Considerations
- JAML editor can expand to cover top bar (for keyboard)
- Touch-friendly drag targets (minimum 44px)
- Prevent text selection during drag
- Handle orientation changes gracefully

## Performance Requirements
- Smooth 60 FPS during drag operations
- Throttle resize updates to ~10 FPS
- Debounce save operations
- Lazy load Monaco editor (if used)
- Virtualize large result tables

## Accessibility
- Keyboard navigation support
- ARIA labels for drag handles
- Focus management
- Screen reader announcements for state changes

## Browser Compatibility
- Modern browsers (Chrome, Firefox, Safari, Edge)
- Mobile browsers (iOS Safari, Chrome Mobile)
- Graceful degradation for older browsers

## Data Flow

### Filter Loading
1. User selects filter from dropdown
2. Fetch filter JAML from API
3. Load into editor
4. Update URL with filter ID
5. Check for existing search results
6. Display results if available

### Search Execution
1. User clicks "Start Search"
2. Validate JAML syntax
3. Send POST to /search endpoint
4. Receive search ID
5. Connect to SignalR hub
6. Receive real-time updates
7. Update results table
8. Update progress indicators

### State Persistence
- Save editor content to localStorage (draft)
- Save resize positions to localStorage
- Save selected filter to localStorage
- Restore on page load

## Error Handling
- Invalid JAML: Show inline error, highlight syntax
- Network errors: Show notification, allow retry
- Search failures: Display error message, allow restart
- Missing filters: Show placeholder, allow creation

## Future Enhancements (Out of Scope for MVP)
- Visual filter builder
- Multiple search tabs
- Search history
- Filter templates
- Advanced search options
- Export formats beyond CSV


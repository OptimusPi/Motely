# JAML Genie Widget - Product Requirements Document

## Overview

The JAML Genie is an AI-powered chat widget that helps users create JAML (Joker Ante Markup Language) filters for Balatro seed searching through natural language interaction. It integrates with the MCP (Model Context Protocol) API backend to generate valid JAML filters and provides a knowledge base for answering questions about game mechanics.

## Purpose
The JAML Genie widget serves as an intelligent assistant that:
- **Generates JAML filters** from natural language prompts (e.g., "Create a filter for Blueprint joker")
- **Answers questions** about jokers, vouchers, decks, and game mechanics
- **Provides context-aware responses** using a comprehensive knowledge base
- **Integrates seamlessly** with the JAML Editor for direct filter loading

## Architecture

### Frontend Components

1. **JamlGeniePanel.vue** - Panel component embedded in the main JAML UI
   - Location: `vue-jaml-ui/src/components/JamlGeniePanel.vue`
   - Used as a resizable panel in the main interface

2. **JamlGenie.vue** - Standalone full-page view
   - Location: `vue-jaml-ui/src/views/JamlGenie.vue`
   - Route: `/genie`
   - Can be used as a dedicated genie page

### Backend API Endpoints

The widget connects to two MCP API endpoints:

#### 1. `/mcp/generate` (POST)
**Purpose:** Generate JAML filter from natural language (no search execution)

**Request:**
```json
{
  "prompt": "Create a filter for Blueprint joker"
}
```

**Response:**
```json
{
  "success": true,
  "jaml": "name: Blueprint Filter\ndeck: Red\nstake: White\nmust:\n  - joker: Blueprint\nshould: []\nmustNot: []",
  "reasoning": "AI-generated JAML filter for: Create a filter for Blueprint joker",
  "error": null
}
```

**Error Response:**
```json
{
  "success": false,
  "jaml": null,
  "reasoning": null,
  "error": "Error message here"
}
```

#### 2. `/mcp/prompt` (POST)
**Purpose:** Generate JAML filter AND automatically start a seed search

**Request:**
```json
{
  "prompt": "Find seeds with Blueprint joker"
}
```

**Response:**
```json
{
  "success": true,
  "jamlFilter": "name: Blueprint Filter\n...",
  "reasoning": "AI reasoning",
  "searchId": "search-12345",
  "results": [...],
  "columns": ["seed", "score", ...],
  "message": "Generated JAML filter for: ... Search started with ID: search-12345",
  "searchUrl": "/JAML/?search=search-12345"
}
```

**Note:** The widget primarily uses `/mcp/generate` to avoid auto-starting searches, giving users control.

## Knowledge Base Integration

### Data Structure

The widget uses a comprehensive knowledge base located at:
- `vue-jaml-ui/src/constants/balatroKnowledge.js`

### Knowledge Base Contents

1. **Jokers** - Detailed information about 8+ jokers including:
   - Blueprint, Baron, Stuntman, Supernova, Cavendish, Fortune Teller, Ramen, Sock and Buskin
   - Each joker includes: effect, trigger conditions, scaling behavior, synergies, anti-synergies, rarity, cost, unlock conditions, compatibility, special interactions

2. **Vouchers** - 14 vouchers with base and upgraded versions:
   - Overstock, Clearance Sale, Hone, Reroll Surplus, Crystal Ball, Telescope, Grabber, Wasteful, Tarot Merchant, Planet Merchant, Seed Money, Blank, Magic Trick, Hieroglyph, Director's Cut

3. **Core Mechanics** - Game system documentation:
   - Scoring pipeline and formula
   - Poker hand rankings
   - Shop system and economy
   - Discard mechanics
   - Hand and deck limits

### Search Functions

```javascript
// Find specific joker by name or ID
findJoker('Blueprint') // Returns joker object

// Find specific voucher
findVoucher('Telescope') // Returns voucher object

// Search jokers by query
searchJokers('multiplier') // Returns array of matching jokers

// Format joker info for display
formatJokerInfo(joker) // Returns formatted markdown string

// Format voucher info
formatVoucherInfo(voucher) // Returns formatted markdown string
```

## User Interface

### Chat Interface

- **Message Display:**
  - User messages (red background, left-aligned)
  - Genie messages (purple background, right-aligned)
  - Typing indicator when AI is processing
  - Timestamps for each message
  - Auto-scroll to bottom on new messages

- **Input:**
  - Textarea with Enter to send, Shift+Enter for newline
  - Send button (disabled when typing or empty)
  - Placeholder: "Ask me about JAML filters, Balatro strategies..."

### Sidebar

**Quick Actions:**
- 🎯 Generate Filter - Randomly selects a filter generation prompt
- 🃏 Analyze Deck - Asks about deck selection strategy
- 🎲 Strategy Tips - Requests advanced strategy advice

**Recent Filters:**
- Displays up to 5 most recent filters from API
- Clickable to load filter into conversation
- Shows filter name and creation date

### Generated JAML Actions

When JAML is generated, the widget displays:
- **Copy JAML** button - Copies JAML to clipboard
- **Use in Editor** button - Loads JAML into the JAML Editor panel
- Formatted code block with syntax highlighting

## Response Logic

### Query Detection

The widget intelligently detects user intent:

1. **Create/Generate Requests:**
   - Keywords: "create", "generate", "make", "build", "filter for", "find", "search for"
   - Triggers API call to `/mcp/generate`
   - Returns formatted JAML with action buttons

2. **Information Requests:**
   - Specific joker names → Detailed joker information
   - Specific voucher names → Voucher details
   - General questions → Context-aware responses using knowledge base

3. **Search Queries:**
   - Searches knowledge base for matching jokers
   - Returns formatted information

### Response Formatting

- **Markdown Support:**
  - Bold text (`**text**`)
  - Inline code (`` `code` ``)
  - Code blocks (```yaml ... ```)
  - Line breaks

- **Code Block Styling:**
  - Dark background with border
  - Monospace font
  - Syntax highlighting ready
  - Scrollable for long code

## Integration Points

### Event Emission

The widget emits events to parent components:

```javascript
// Load JAML into editor
emit('load-jaml', jamlString)
```

### Parent Component Handling

The parent (JamlUI.vue) handles:

```javascript
@load-jaml="handleLoadJamlFromGenie"

const handleLoadJamlFromGenie = (jaml) => {
  // Find first JAML editor panel
  const editorPanel = panels.find(p => p.baseId === 'jaml-editor')
  if (editorPanel) {
    jamlContent.value = jaml
    showToast('JAML loaded into editor!', 'success')
  }
}
```

## API Integration Details

### Request Format

```javascript
const response = await post('/mcp/generate', { 
  prompt: userMessage 
})
```

### Error Handling

- Network errors → Fallback to knowledge base responses
- API errors → Display error message with helpful suggestions
- Validation errors → Show error with retry guidance

### Loading States

- `isTyping` flag prevents multiple simultaneous requests
- Typing indicator shown during API calls
- Buttons disabled during processing

## Styling & UX

### Design System

- Uses Balatro color scheme:
  - Red, Blue, Green, Purple, Gold
- Monospace font: `m6x11plus`
- Dark theme with transparency effects

### Responsive Design

- Sidebar collapses on smaller screens
- Chat takes full width on mobile
- Touch-friendly button sizes

### Animations

- Message fade-in animation
- Typing indicator with bouncing dots
- Smooth scroll to bottom
- Button hover effects

## Technical Implementation

### Dependencies

```javascript
import { useApi } from '../composables/useApi'
import { 
  findJoker, 
  findVoucher, 
  searchJokers, 
  formatJokerInfo,
  formatVoucherInfo,
  jokers,
  vouchers,
  coreMechanics
} from '../constants/balatroKnowledge'
```

### State Management

- `messages` - Array of chat messages
- `userInput` - Current input text
- `isTyping` - Loading state
- `recentFilters` - Cached filter list

### Lifecycle

```javascript
onMounted(async () => {
  await loadRecentFilters()
  await nextTick()
  inputRef.value?.focus()
})
```

## Example Usage Flow

1. **User asks:** "Create a filter for Blueprint joker"
2. **Widget detects** create intent
3. **Calls API:** `POST /mcp/generate` with prompt
4. **Receives JAML:** Validated JAML filter
5. **Displays:** Formatted JAML with copy/use buttons
6. **User clicks:** "Use in Editor"
7. **JAML loads:** Into the JAML Editor panel

## Alternative: Information Query Flow

1. **User asks:** "Tell me about Blueprint"
2. **Widget searches:** Knowledge base for "Blueprint"
3. **Finds joker:** Blueprint joker object
4. **Formats info:** Using `formatJokerInfo()`
5. **Displays:** Detailed joker information with synergies, anti-synergies, special interactions

## Configuration

### API Base URL

The widget uses the `useApi` composable which:
- In development: Uses relative URLs (Vite proxy)
- In production: Uses `VITE_API_URL` env var or same origin

### Knowledge Base

The knowledge base is a static JavaScript module that can be:
- Extended with more jokers/vouchers
- Updated with new game mechanics
- Used independently for other features

## Backend Requirements

### MCP Server Setup

The backend must have:

1. **McpServer service registered:**
```csharp
builder.Services.AddScoped<McpServer>(sp => {
    var logger = sp.GetRequiredService<ILogger<McpServer>>();
    var httpClient = new HttpClient();
    var config = sp.GetRequiredService<IConfiguration>();
    return new McpServer(logger, httpClient, config);
});
```

2. **Endpoints configured:**
```csharp
app.MapPost("/mcp/generate", async (HttpRequest request, McpServer mcpServer) => {
    var req = await request.ReadFromJsonAsync<McpPromptRequest>();
    var (jaml, reasoning, error) = await mcpServer.GenerateJamlOnlyAsync(req.Prompt);
    return Results.Ok(new { success: string.IsNullOrEmpty(error), jaml, reasoning, error });
});
```

3. **Cloudflare Workers AI configured:**
- Worker URL in `appsettings.json`
- Model: `@cf/meta/llama-3.1-8b-instruct-fp8` (default)

## Testing Checklist

- [ ] Widget loads and displays initial greeting
- [ ] Knowledge base queries return correct information
- [ ] API calls to `/mcp/generate` work
- [ ] Generated JAML is valid and formatted correctly
- [ ] Copy button copies JAML to clipboard
- [ ] "Use in Editor" loads JAML into editor
- [ ] Error handling works for API failures
- [ ] Typing indicator shows during API calls
- [ ] Recent filters load from API
- [ ] Mobile responsive design works

## Future Enhancements

Potential improvements:
- Save generated JAML as named filters
- Search history/chat persistence
- Export chat conversations
- Multiple genie instances
- Custom knowledge base entries
- Integration with seed search results

## Notes for Implementation

- The widget is designed to be self-contained but integrates with parent components
- Knowledge base can be expanded without widget changes
- API endpoints are RESTful and stateless
- Error messages should be user-friendly
- Loading states prevent duplicate requests
- All user actions should provide feedback

---

**Created:** 2024-12-26  
**Version:** 1.0  
**Status:** Implemented and Working

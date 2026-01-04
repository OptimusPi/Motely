# New Year Completion To-Do List
**Target: 85% Complete, 85% Confidence, 85% to Specifications**  
**Goal: Finish Today - Goldilocks Solution**

---

## 🔴 CRITICAL - Must Fix for Chat to Work

### 1. Implement SearchBroadcaster and Wire to SignalR (85% confidence)
**Status:** ❌ NOT IMPLEMENTED  
**Priority:** CRITICAL  
**Files:** `Motely.API/SearchBroadcaster.cs` (NEW), `Motely.API/MotelyApiHost.cs`

**Problem:**
- `SearchManager` has `SetBroadcaster()` but broadcaster is never set
- `_broadcaster` is always null, so search updates never reach frontend
- Chat works (SearchHub has SendMessage), but search results don't broadcast

**Solution:**
- Create `SearchBroadcaster.cs` implementing `ISearchBroadcaster`
- Use `IHubContext<SearchHub>` to send messages via SignalR
- In `MotelyApiHost.cs`, create broadcaster instance and call `SearchManager.Instance.SetBroadcaster(broadcaster)`
- Map SignalR events: `SearchUpdate`, `Result`, `Progress` to match frontend expectations

**Estimated Time:** 30-45 minutes  
**Confidence:** 85% - Straightforward SignalR integration

---

### 2. Verify Chat Connection Works End-to-End (85% confidence)
**Status:** ⚠️ NEEDS TESTING  
**Priority:** HIGH  
**Files:** `vue-jaml-ui/src/composables/useChat.js`, `Motely.API/Hubs/SearchHub.cs`

**What to Verify:**
- Chat panel connects to `/searchHub` on page load
- Messages send via `SendMessage` method
- Messages receive via `ReceiveMessage` event
- User join/leave notifications work
- Works in both dev (Vite proxy) and prod (direct connection)

**Test Steps:**
1. Open two browser windows
2. Type message in one, verify it appears in both
3. Check browser console for connection errors
4. Verify SignalR connection shows as connected

**Estimated Time:** 15-20 minutes  
**Confidence:** 85% - Code looks correct, just needs verification

---

## 🟡 HIGH PRIORITY - Core Functionality

### 3. Fix Missing SignalR Event Handlers in Frontend (85% confidence)
**Status:** ⚠️ PARTIAL  
**Priority:** HIGH  
**Files:** `vue-jaml-ui/src/composables/useSignalR.js`, `vue-jaml-ui/src/components/ActiveSearchesPanel.vue`

**Problem:**
- Frontend listens for `Result`, `Progress`, `SearchUpdate` events
- But SearchManager broadcasts JSON strings, not direct events
- Need to parse JSON and map to correct event names

**Solution:**
- Update `useSignalR.js` to listen for generic `SearchUpdate` event
- Parse JSON payload and route to correct handlers based on `type` field
- Map: `type: "result"` → `Result` handler, `type: "progress"` → `Progress` handler

**Estimated Time:** 20-30 minutes  
**Confidence:** 85% - Standard event routing pattern

---

### 4. Implement MCP Resource Reading (85% confidence)
**Status:** ❌ NOT IMPLEMENTED  
**Priority:** MEDIUM  
**Files:** `Motely.API/McpProtocol/McpServer.cs` (line 557)

**Problem:**
- `HandleResourceRead` returns "not yet implemented" error
- MCP clients expect resource reading capability

**Solution:**
- Implement reading of JAML filter files, seed source files, or search result files
- Return file content as resource data
- Add proper error handling for missing files

**Estimated Time:** 30-45 minutes  
**Confidence:** 85% - File I/O is straightforward

---

### 5. Implement MCP Prompt Generation (85% confidence)
**Status:** ❌ NOT IMPLEMENTED  
**Priority:** MEDIUM  
**Files:** `Motely.API/McpProtocol/McpServer.cs` (line 613)

**Problem:**
- `HandlePromptGet` returns "not yet implemented" error
- MCP clients expect prompt templates

**Solution:**
- Map prompt names to JAML generation logic
- Use existing `McpServer.ProcessPromptAsync` for prompt execution
- Return generated JAML as prompt result

**Estimated Time:** 30-45 minutes  
**Confidence:** 85% - Can reuse existing prompt processing

---

## 🟢 MEDIUM PRIORITY - Polish & Testing

### 6. Test MCP Protocol Endpoint (`POST /mcp`) (85% confidence)
**Status:** ⚠️ NEEDS TESTING  
**Priority:** MEDIUM  
**Files:** `Motely.API/MotelyApiHost.cs`

**What to Test:**
- `initialize` method returns capabilities
- `tools/list` returns available tools
- `tools/call` executes `generate_jaml_filter` and `search_seeds`
- Error handling for invalid requests

**Test Command:**
```bash
curl -X POST http://localhost:3141/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}'
```

**Estimated Time:** 20-30 minutes  
**Confidence:** 85% - Endpoint exists, just needs validation

---

### 7. Verify Search Results Broadcast Correctly (85% confidence)
**Status:** ⚠️ NEEDS TESTING  
**Priority:** MEDIUM  
**Files:** `Motely.API/SearchManager.cs`, Frontend result panels

**What to Verify:**
- Search starts → `SearchUpdate` event fires
- Results found → `Result` events fire with seed/score data
- Progress updates → `Progress` events fire with batch info
- Search completes → `SearchUpdate` with completion status

**Test Steps:**
1. Start a search from frontend
2. Open browser DevTools → Network → WS tab
3. Verify SignalR messages appear
4. Verify results appear in ResultsPanel

**Estimated Time:** 20-30 minutes  
**Confidence:** 85% - Depends on #1 being fixed first

---

### 8. Fix Fertilizer Dump Implementation (85% confidence)
**Status:** ⚠️ INCOMPLETE  
**Priority:** LOW  
**Files:** `Motely.API/SearchManager.cs` (line 1385)

**Problem:**
- `DumpToFertilizerAndDeleteDb` has TODO comment
- Logic exists but may not be fully implemented

**Solution:**
- Verify `GetTopResultsFromDb` works correctly
- Implement adding results to fertilizer pile
- Test deletion of search DB after dump

**Estimated Time:** 30-45 minutes  
**Confidence:** 85% - Logic mostly there, needs completion

---

## 🔵 LOW PRIORITY - Nice to Have

### 9. Add Error Handling for Chat Disconnections (85% confidence)
**Status:** ⚠️ PARTIAL  
**Priority:** LOW  
**Files:** `vue-jaml-ui/src/composables/useChat.js`

**Enhancement:**
- Show visual indicator when chat disconnects
- Auto-reconnect with exponential backoff
- Queue messages when disconnected, send on reconnect

**Estimated Time:** 30-45 minutes  
**Confidence:** 85% - Standard reconnection pattern

---

### 10. Verify All Panel Colors Match Specification (85% confidence)
**Status:** ✅ DONE  
**Priority:** LOW  
**Files:** `vue-jaml-ui/src/views/JamlUI.vue`

**Verification:**
- Left: Red (JAML Editor), Orange (Blueprint) ✅
- Right: Green (Active Searches), Blue (Chat), Purple (Results) ✅
- Divider: Gold ✅

**Estimated Time:** 5 minutes (just visual check)  
**Confidence:** 100% - Already implemented

---

## 📋 Testing Checklist

### Backend Testing
- [ ] Start API server (`dotnet run` in `Motely.API`)
- [ ] Test `/health` endpoint returns 200
- [ ] Test `/mcp` endpoint with initialize request
- [ ] Test SignalR hub at `/searchHub` connects
- [ ] Test chat message send/receive
- [ ] Test search start and result broadcasting

### Frontend Testing
- [ ] Build Vue app (`npm run build` in `vue-jaml-ui`)
- [ ] Verify build outputs to `wwwroot/JAML/`
- [ ] Test chat panel connection
- [ ] Test search start from frontend
- [ ] Verify results appear in real-time
- [ ] Test panel resizing and layout persistence

### Integration Testing
- [ ] Open two browser windows, verify chat works
- [ ] Start search, verify progress updates
- [ ] Verify search results appear in ResultsPanel
- [ ] Test MCP client connection (if available)

---

## 🎯 Completion Criteria

**85% Complete = 8.5 out of 10 items done:**
- Critical (2 items): Must complete both
- High Priority (3 items): Complete at least 2
- Medium Priority (3 items): Complete at least 2
- Low Priority (2 items): Complete at least 1

**85% Confidence = Each item has clear solution path and estimated time**

**85% to Specifications = Items meet user requirements, not over-engineered**

---

## 🚀 Quick Start Order

1. **#1 - SearchBroadcaster** (CRITICAL - blocks search updates)
2. **#2 - Verify Chat** (CRITICAL - user specifically requested)
3. **#3 - Fix SignalR Handlers** (HIGH - needed for search to work)
4. **#7 - Test Search Results** (HIGH - verify everything works)
5. **#6 - Test MCP Endpoint** (MEDIUM - verify MCP works)
6. **#4 - MCP Resource Reading** (MEDIUM - complete MCP features)
7. **#5 - MCP Prompt Generation** (MEDIUM - complete MCP features)
8. **#8 - Fertilizer Dump** (LOW - polish)
9. **#9 - Chat Error Handling** (LOW - polish)

---

## 📝 Notes

- Chat functionality is **already implemented** in SearchHub - just needs verification
- Search broadcasting is **missing** - SearchBroadcaster needs to be created and wired
- MCP Protocol endpoint exists and works, but some methods return "not implemented"
- Frontend SignalR connection works, but event routing may need adjustment
- All color changes are complete and verified

**Total Estimated Time:** 4-6 hours for full completion  
**Minimum Viable (Critical + 2 High):** 1.5-2 hours

---

**Last Updated:** 2024-12-26  
**Status:** Ready to Execute 🚀

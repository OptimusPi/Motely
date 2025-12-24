# MCP Server Purpose - Quick Answer

## What Should the MCP Server Do?

**Answer: Provide tools, let AI decide.**

---

## The Two Jobs

### Job 1: Generate Configs
- **Tool:** `generate_jaml_filter`
- **Input:** Human prompt (natural language)
- **Output:** JAML config
- **Should NOT:** Automatically search for seeds

### Job 2: Search for Seeds
- **Tool:** `search_seeds`
- **Input:** JAML config
- **Output:** Seeds (if found)
- **Should NOT:** Generate JAML (that's tool 1's job)

---

## Current Problem

Right now, `generate_jaml_filter` does **BOTH**:
1. ✅ Generates JAML config
2. ❌ **Also automatically starts a search**

This is wrong because:
- If user just wants a config → Gets unnecessary search
- If user wants to modify config → Can't (search already started)
- Violates separation of concerns

---

## Correct Behavior

### Scenario 1: "Generate a JAML filter"
- User: "Generate a JAML filter for Blueprint"
- Claude calls: `generate_jaml_filter` only
- Returns: Just the JAML config (no search)

### Scenario 2: "Find me a seed"
- User: "Find me a seed with Blueprint"
- Claude calls:
  1. `generate_jaml_filter` → Gets JAML
  2. `search_seeds` → Uses JAML to search
- Returns: Seeds

### Scenario 3: "Search this JAML"
- User: "Search for seeds with this JAML: ..."
- Claude calls: `search_seeds` only
- Returns: Seeds

---

## Summary

**MCP Server's Job:**
- ✅ Provide tools (generate config, search seeds, check status)
- ❌ **NOT** to automatically chain tools together
- ❌ **NOT** to assume user wants to search after generating config

**AI Client's Job:**
- ✅ Understand user intent
- ✅ Call appropriate tools in sequence
- ✅ Orchestrate the workflow

**Result:** Clean separation, maximum flexibility! 🎯



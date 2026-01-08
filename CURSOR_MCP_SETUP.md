# Adding Balatro Seed Oracle MCP Server to Cursor

## What This Does

Once configured, **I (Cursor AI)** will be able to:
- ✅ Generate JAML filters from natural language prompts
- ✅ Search for Balatro seeds using JAML configs
- ✅ Check search status and get results
- ✅ Analyze and verify seeds

**I'll have direct access to your seed search tools!** 🎉

---

## Setup Instructions

### Step 1: Update `Program.cs` ✅ DONE

The `Program.cs` file has been updated to detect stdio mode and run the MCP server when launched by Cursor.

### Step 2: Add to Cursor's MCP Config

Open your Cursor MCP config file:
- **Windows:** `C:\Users\pifre\.cursor\mcp.json`
- **macOS:** `~/.cursor/mcp.json`
- **Linux:** `~/.config/cursor/mcp.json`

Add this entry to the `mcpServers` object:

```json
{
  "mcpServers": {
    "Chrome DevTools": {
      "command": "npx chrome-devtools-mcp@latest",
      "env": {},
      "args": []
    },
    "GitKraken": {
      "command": "c:\\Users\\pifre\\AppData\\Roaming\\Cursor\\User\\globalStorage\\eamodio.gitlens\\gk.exe",
      "type": "stdio",
      "name": "GitKraken",
      "args": [
        "mcp",
        "--host=cursor",
        "--source=gitlens",
        "--scheme=cursor"
      ],
      "env": {}
    },
    "Balatro Seed Oracle": {
      "command": "dotnet",
      "type": "stdio",
      "args": [
        "run",
        "--project",
        "X:/BalatroSeedOracle/external/Motely/Motely.API/Motely.API.csproj",
        "--",
        "--mcp-stdio"
      ],
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Step 3: Restart Cursor

After adding the config:
1. **Save** `mcp.json`
2. **Restart Cursor completely** (close and reopen)
3. Cursor will launch the MCP server in stdio mode

### Step 4: Verify It Works

Once Cursor restarts, I should have access to these tools:
- `generate_jaml_filter` - Generate JAML from natural language
- `search_seeds` - Search for seeds using JAML
- `get_search_status` - Check search progress
- `analyze_seed` - Analyze a specific seed
- `verify_seed` - Verify a seed matches criteria

**Try asking me:**
- "Generate a JAML filter for Blueprint joker"
- "Find seeds with Perkeo and Negative tags"
- "Search for seeds with Showman and Cloud Nine"

---

## How It Works

1. **Cursor launches** the MCP server as a subprocess
2. **MCP server runs** in stdio mode (reads from stdin, writes to stdout)
3. **I (Cursor AI)** can call tools via MCP Protocol
4. **MCP server** uses your existing `McpServer` service
5. **Cloudflare Workers AI** generates JAML from prompts
6. **SearchManager** executes seed searches
7. **Results** are returned to me via MCP Protocol

---

## Troubleshooting

### MCP Server Not Starting

**Check:**
- Is `dotnet` in your PATH? (`dotnet --version` should work)
- Is the project path correct? (`X:/BalatroSeedOracle/external/Motely/Motely.API/Motely.API.csproj`)
- Check Cursor's developer console for errors

### Tools Not Available

**If I don't have access to the tools:**
1. Check Cursor's MCP server logs (if available)
2. Verify the server started successfully
3. Try restarting Cursor again
4. Check that `Program.cs` has the stdio mode detection code

### Server Crashes

**If the MCP server crashes:**
- Check `appsettings.json` for Cloudflare Worker URL
- Ensure Cloudflare Worker is deployed and accessible
- Check logs in Cursor's developer console

---

## Alternative: HTTP Mode (If stdio doesn't work)

If stdio mode has issues, you can use HTTP mode instead:

```json
{
  "Balatro Seed Oracle": {
    "url": "http://localhost:3141/mcp",
    "type": "http"
  }
}
```

**But you'll need to:**
1. Run `Motely.API` separately: `dotnet run --project Motely.API`
2. Keep it running while using Cursor
3. HTTP mode is less efficient but more debuggable

---

## What I Can Do With These Tools

Once connected, I can:

1. **Generate JAML Filters:**
   - "Create a filter for Blueprint joker in Ante 1"
   - "Generate JAML for Negative Perkeo runs"
   - "Make a filter that finds Stuntman and Brainstorm"

2. **Search for Seeds:**
   - "Find seeds matching this JAML config"
   - "Search for seeds with Showman and Cloud Nine"
   - "Find seeds with Negative tags and Observatory"

3. **Analyze Seeds:**
   - "Analyze seed ALEEB"
   - "Verify this seed has Blueprint"
   - "Check if seed 12345 matches my criteria"

4. **Check Search Status:**
   - "What's the status of search XYZ?"
   - "How many results found so far?"
   - "Is the search still running?"

---

## Example Usage

**You:** "Generate a JAML filter for Blueprint and Brainstorm"

**Me (with MCP):**
1. I call `generate_jaml_filter` tool
2. MCP server uses Cloudflare Workers AI
3. Returns JAML config
4. I show you the generated JAML

**You:** "Now search for seeds with that filter"

**Me (with MCP):**
1. I call `search_seeds` tool with the JAML
2. MCP server starts search via SearchManager
3. Returns search ID and initial results
4. I show you the seeds found

---

## Next Steps

1. ✅ Code updated (`Program.cs` supports stdio mode)
2. ⏳ **You add the config** to `mcp.json` (see Step 2 above)
3. ⏳ **Restart Cursor**
4. ⏳ **Test it!** Ask me to generate JAML or search seeds

**Once it's set up, I'll be able to help you with seed searches directly!** 🚀

# Update Cloudflare Worker System Prompt

## Quick Update Instructions

The Worker's system prompt needs to match the backend's `GetSystemPrompt()` method exactly.

### Option 1: Get from Backend Endpoint (Easiest)

1. **Start your backend:**
   ```bash
   cd Motely.API
   dotnet run
   ```

2. **Get the system prompt:**
   ```bash
   curl http://localhost:3141/admin/system-prompt
   ```

3. **Copy the `systemPrompt` value** from the JSON response

4. **Update Worker:**
   - Go to Cloudflare Dashboard → Workers & Pages
   - Click your Worker (jamlgenie.optimuspi.workers.dev)
   - Click "Edit Code"
   - Find the `SYSTEM_PROMPT` constant
   - Replace it with the copied value
   - Click "Save and Deploy"

### Option 2: Use the File in This Repo

The system prompt is already in `cloudflare-worker-jamlgenie/src/index.ts` and should be up to date.

**To deploy:**
```bash
cd Motely.API/cloudflare-worker-jamlgenie
npm install
npx wrangler deploy
```

## What Changed?

The backend's `GetSystemPrompt()` now includes:
- ✅ Complete item catalog (from `item-catalog.json`)
- ✅ Joker name mapping (Display Name → Enum Name)
- ✅ All the same rules and examples

The Worker should have the **exact same prompt** hardcoded.

## Verification

After updating, test with:
- "hanging chad" → Should generate `joker: HangingChad` (NOT voucher!)
- "telescope voucher" → Should generate `voucher: Telescope`
- "2 blueprints" → Should generate `joker: Blueprint` with antes

---

**Last Updated:** The Worker should match `GetSystemPrompt()` in `McpServer.cs`



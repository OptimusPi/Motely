# Quick Deploy: Update Cloudflare Worker System Prompt

## ✅ The Worker Code is Already Updated!

The system prompt in `src/index.ts` has been updated with:
- ✅ Complete item catalog
- ✅ Joker name mapping (Display Name → Enum Name)
- ✅ All rules and examples

## Deploy Now

### Option 1: Using Wrangler (Recommended - Auto-configures AI binding)

```bash
cd Motely.API/cloudflare-worker-jamlgenie
npm install
npx wrangler login
npx wrangler deploy
```

**✅ Wrangler automatically enables the AI binding from `wrangler.jsonc`**

### Option 2: Cloudflare Dashboard

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) → Workers & Pages
2. Click your Worker: `jamlgenie` (or whatever you named it)
3. Click **"Edit Code"** (or "Quick Edit")
4. **Copy the entire contents** of `Motely.API/cloudflare-worker-jamlgenie/src/index.ts`
5. **Paste it** into the Cloudflare editor
6. **⚠️ IMPORTANT:** Go to **Settings** → **Variables** → **AI Bindings** → Add binding named `AI`
7. Click **"Save and Deploy"**

**⚠️ If you skip step 6, you'll get: "Cannot read properties of undefined (reading 'run')"**

## What Was Updated?

The system prompt now includes:
- **JOKER NAME MAPPING** section (was missing!)
- Complete item catalog (already had this)
- All the same rules and examples as the backend

## Verify It Works

After deploying, test with:
- "hanging chad" → Should return `joker: HangingChad` (NOT voucher!)
- "telescope voucher" → Should return `voucher: Telescope`
- "2 blueprints" → Should return `joker: Blueprint`

---

**The Worker code in this repo is ready to deploy!** 🚀



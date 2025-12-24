# Fix: "Cannot read properties of undefined (reading 'run')"

## The Problem

The Cloudflare Worker's AI binding isn't enabled. The error means `env.AI` is `undefined`.

## Quick Fix (Cloudflare Dashboard)

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) → **Workers & Pages**
2. Click your Worker: **`jamlgenie`** (or whatever you named it)
3. Go to **Settings** → **Variables**
4. Scroll to **AI Bindings**
5. Click **Add binding**
6. Set:
   - **Variable name:** `AI` (must match exactly - case sensitive)
   - **AI Binding:** Leave as default (Workers AI)
7. Click **Save**
8. The worker will automatically redeploy

## Verify It Works

After enabling the binding, test with:
```bash
curl -X POST https://your-worker.workers.dev \
  -H "Content-Type: application/json" \
  -d '{"prompt": "find me 2 blueprints"}'
```

Should return: `{"success":true,"jaml":"..."}`

## Alternative: Deploy with Wrangler

If you deploy with `wrangler`, the binding is automatically configured from `wrangler.jsonc`:

```bash
cd Motely.API/cloudflare-worker-jamlgenie
npx wrangler deploy
```

The `wrangler.jsonc` already has:
```jsonc
{
  "ai": {
    "binding": "AI"
  }
}
```

So wrangler will automatically enable it.

## Why This Happens

- **Dashboard deployment:** Bindings must be manually enabled
- **Wrangler deployment:** Bindings are auto-configured from `wrangler.jsonc`

---

**TL;DR:** Go to Dashboard → Worker → Settings → Variables → Add AI binding named `AI` → Save



# Deploy JamlGenie Worker

## Quick Deploy Steps

1. **Get the system prompt:**
   ```bash
   # Start your backend, then visit:
   http://localhost:3141/admin/system-prompt
   # Copy the entire "systemPrompt" value
   ```

2. **Paste it into the Worker:**
   - Open `src/index.ts`
   - Find `const SYSTEM_PROMPT = \`[PASTE...]\`;`
   - Replace `[PASTE...]` with the system prompt you copied

3. **Deploy with wrangler:**
   ```bash
   cd cloudflare-worker-jamlgenie
   npm install
   wrangler login
   wrangler deploy
   ```

## Or Deploy via Cloudflare Dashboard

1. Go to **Workers & Pages** → Click **jamlgenie** worker
2. Click **"Edit Code"** or **"Quick Edit"**
3. Paste the code from `src/index.ts` (with system prompt filled in)
4. Click **"Save and Deploy"**

## Make Sure AI Binding is Enabled

In Cloudflare Dashboard:
- Go to your Worker → **Settings** → **Variables**
- Make sure **AI Binding** is enabled
- It should be named `AI` (matches `env.AI` in code)


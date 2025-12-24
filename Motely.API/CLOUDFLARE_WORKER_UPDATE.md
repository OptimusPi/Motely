# Cloudflare Worker - JamlGenie

## Worker URL
`https://jamlgenie.optimuspi.workers.dev`

## Deploy

The system prompt is already embedded in `cloudflare-worker-jamlgenie/src/index.ts`.

```bash
cd Motely.API/cloudflare-worker-jamlgenie
npm install
npx wrangler deploy
```

## What It Does

1. Receives natural language prompt from backend: `{ "prompt": "2 blueprints" }`
2. Uses hardcoded system prompt + user prompt with Workers AI
3. Returns JAML filter: `{ "success": true, "jaml": "..." }`

## Model

Using `@cf/meta/llama-3.1-8b-instruct-fp8` (free tier)

## If You Need to Update the System Prompt

The system prompt is in `cloudflare-worker-jamlgenie/src/index.ts` as `SYSTEM_PROMPT` constant.

Source of truth: `GetSystemPrompt()` in `McpServer.cs`

## Testing

After deploy, test with:
- "hanging chad" → should generate `joker: HangingChad` (NOT voucher!)
- "2 blueprints" → should generate `joker: Blueprint`
- "telescope voucher" → should generate `voucher: Telescope`

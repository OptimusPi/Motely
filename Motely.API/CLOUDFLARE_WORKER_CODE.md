# Cloudflare Worker Code - Copy This

## What Your Worker Should Look Like

Your Worker needs to:
1. Accept `{ prompt: "user's request" }` from the backend
2. Hardcode the system prompt (for security)
3. Call Workers AI with `messages` array format

## Step-by-Step in Cloudflare Dashboard

1. **Go to Workers & Pages** → Click your Worker (jamlgenie.optimuspi.workers.dev)
2. **Click "Edit Code"** (or "Quick Edit")
3. **Replace the Worker code** with this:

```javascript
export default {
  async fetch(request, env) {
    // Only accept POST requests
    if (request.method !== 'POST') {
      return new Response('Method not allowed', { status: 405 });
    }

    try {
      // Get user prompt from backend
      const body = await request.json();
      const userPrompt = body.prompt || '';
      
      if (!userPrompt) {
        return new Response(JSON.stringify({ 
          success: false, 
          error: 'Missing prompt' 
        }), {
          headers: { 'Content-Type': 'application/json' }
        });
      }

      // SYSTEM PROMPT - Hardcoded (DO NOT accept from users for security)
      // Get the full system prompt from: http://localhost:3141/admin/system-prompt
      // Copy the "systemPrompt" value from that JSON response
      const SYSTEM_PROMPT = `[PASTE THE FULL SYSTEM PROMPT HERE - GET IT FROM /admin/system-prompt ENDPOINT]`;

      // Call Workers AI with messages format
      const ai = new Ai(env.AI);
      const response = await ai.run('@cf/meta/llama-3.1-8b-instruct-fp8', {
        messages: [
          { role: 'system', content: SYSTEM_PROMPT },
          { role: 'user', content: userPrompt }
        ],
        max_tokens: 2048,
        temperature: 0.7
      });

      // Extract the generated JAML from response
      const generatedJaml = response.response || response.text || '';
      
      // Return as JSON (backend expects this format)
      return new Response(JSON.stringify({
        success: true,
        jaml: generatedJaml.trim(),
        config: null
      }), {
        headers: { 'Content-Type': 'application/json' }
      });

    } catch (error) {
      return new Response(JSON.stringify({
        success: false,
        error: error.message || 'Unknown error'
      }), {
        status: 500,
        headers: { 'Content-Type': 'application/json' }
      });
    }
  }
};
```

## How to Get the System Prompt

1. **Start your backend** (if not running)
2. **Visit**: `http://localhost:3141/admin/system-prompt`
3. **Copy the entire `systemPrompt` value** from the JSON response
4. **Paste it** where it says `[PASTE THE FULL SYSTEM PROMPT HERE...]` in the Worker code above

## Important Notes

- The `Ai` class is automatically available via `env.AI` binding (no import needed in newer Workers)
- If you get an error about `Ai`, try: `import { Ai } from '@cloudflare/workers-ai';` at the top
- The model is `@cf/meta/llama-3.1-8b-instruct` (free tier)
- Make sure your Worker has the AI binding enabled in Settings → Variables → AI Binding

## After Updating

1. Click **"Save and Deploy"**
2. Test by asking JAM Genie: "hanging chad"
3. It should generate JAML with `type: "Joker"` (NOT voucher or spectral)


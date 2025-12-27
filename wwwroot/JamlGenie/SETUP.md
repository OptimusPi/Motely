# JamlGenie Cloudflare Workers Setup

## Quick Setup

1. **Install Wrangler CLI:**
   ```bash
   npm install -g wrangler
   ```

2. **Login to Cloudflare:**
   ```bash
   wrangler login
   ```

3. **Enable Workers AI:**
   - Go to Cloudflare Dashboard → Workers & Pages → AI
   - Enable Workers AI (free tier available)

4. **Deploy the worker:**
   ```bash
   # Copy the example files
   cp worker.example.js worker.js
   cp wrangler.toml.example wrangler.toml
   
   # Deploy
   wrangler deploy
   ```

5. **Get your worker URL:**
   After deployment, you'll get a URL like:
   ```
   https://jamlgenie.your-subdomain.workers.dev
   ```

6. **Update app.js:**
   ```javascript
   const GENIE_API = 'https://jamlgenie.your-subdomain.workers.dev';
   ```

## Alternative: Use Existing balatrogenie.app

If you already have `balatrogenie.app` working, just update `app.js`:
```javascript
const GENIE_API = 'https://balatrogenie.app/generate';
```

## Testing

1. Open `index.html` in browser
2. Type: "I want 2 blueprints yo"
3. Press Enter or click ✨
4. Get JSON!

That's it! 🧞✨

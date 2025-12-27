# Local Development Guide

## Test Worker Locally (No Deployment Needed!)

### Quick Start

1. **Install Wrangler:**
   ```bash
   npm install -g wrangler
   ```

2. **Login:**
   ```bash
   wrangler login
   ```

3. **Start Dev Server:**
   ```bash
   cd worker
   wrangler dev
   ```

4. **Update app.js for local testing:**
   ```javascript
   const GENIE_API = 'http://localhost:8787';
   ```

5. **Open index.html** and test!

### What You'll See

```
 ⛅️ wrangler dev

⎔ Starting local server...
[wrangler:inf] Ready on http://localhost:8787
```

The worker runs locally using your Cloudflare Workers AI account, so you can test everything before deploying!

### Testing

1. Open `index.html` in browser
2. Type: "I want 2 blueprints yo"
3. Press Enter
4. See it work! 🧞✨

### Switch Back to Production

When ready to deploy, change `app.js` back:
```javascript
const GENIE_API = 'https://jamlgenie.YOUR-SUBDOMAIN.workers.dev';
```

Then deploy via GitHub Actions or `wrangler deploy`.

---

**Note:** Local dev uses your real Cloudflare Workers AI quota, but it's perfect for testing!

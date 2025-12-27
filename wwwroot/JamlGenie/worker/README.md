# JamlGenie Worker - Local Development

## Test Locally Before Deploying

### Prerequisites
```bash
npm install -g wrangler
```

### Login to Cloudflare
```bash
wrangler login
```

### Run Local Dev Server
```bash
cd worker
wrangler dev
```

This will:
- Start a local server (usually `http://localhost:8787`)
- Use your Cloudflare Workers AI account
- Hot reload on changes

### Test It
Update `app.js` temporarily:
```javascript
const GENIE_API = 'http://localhost:8787';
```

Then open `index.html` and test!

### Deploy When Ready
```bash
wrangler deploy
```

Or use GitHub Actions (automatic on push).

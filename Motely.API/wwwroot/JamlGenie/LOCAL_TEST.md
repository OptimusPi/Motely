# Local Testing with cloudflared

## Option 1: Test Worker Locally (Recommended)

### Install Wrangler CLI
```bash
npm install -g wrangler
```

### Login to Cloudflare
```bash
wrangler login
```

### Test Worker Locally
```bash
cd worker
wrangler dev
```

This will:
- Start local dev server
- Give you a URL like `http://localhost:8787`
- Hot-reload on changes

### Update app.js for local testing:
```javascript
const GENIE_API = 'http://localhost:8787';
```

## Option 2: Tunnel Web App with cloudflared

### Install cloudflared
Download from: https://github.com/cloudflare/cloudflared/releases

Or via package manager:
```bash
# Windows (choco)
choco install cloudflared

# macOS
brew install cloudflared

# Linux
# Download binary or use package manager
```

### Start Tunnel
```bash
cd external/Motely/Motely.API/wwwroot/JamlGenie
cloudflared tunnel --url http://localhost:8000
```

### Serve Web App Locally
```bash
# Python 3
python -m http.server 8000

# Or Node.js
npx http-server -p 8000

# Or PHP
php -S localhost:8000
```

### Access via Tunnel
cloudflared will give you a URL like:
```
https://random-subdomain.trycloudflare.com
```

Open that URL in browser - it will tunnel to your local server!

## Option 3: Use Existing balatrogenie.app

If you just want to test the UI without deploying:

```javascript
// In app.js - already set to this
const GENIE_API = 'https://balatrogenie.app/generate';
```

Just open `index.html` and test!

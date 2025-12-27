# Deploy JamlGenie Worker NOW

## Quick Deploy (2 options)

### Option 1: Manual Deploy (Fastest)

```bash
cd external/Motely/Motely.API/wwwroot/JamlGenie/worker
npm install -g wrangler@latest
wrangler login
wrangler deploy
```

After deploy, wrangler will show you the URL like:
```
https://jamlgenie.YOUR-ACCOUNT.workers.dev
```

### Option 2: GitHub Actions (Auto)

1. Set GitHub secrets:
   - `CLOUDFLARE_API_TOKEN` - Your API token
   - `CLOUDFLARE_ACCOUNT_ID` - Your account ID
   
2. Push or manually run workflow

## After Deployment

Update `app.js` line 11:
```javascript
const GENIE_API = 'https://jamlgenie.YOUR-ACCOUNT.workers.dev';
```

Replace `YOUR-ACCOUNT` with your actual workers.dev subdomain.

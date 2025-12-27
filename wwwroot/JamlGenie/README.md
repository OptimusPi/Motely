# JamlGenie - Ready for Cloudflare Pages Deployment

## Quick Deploy

### Using Wrangler CLI:

```bash
cd Motely.API/wwwroot/JamlGenie
npx wrangler pages deploy . --project-name=balatrogenie
```

### Or use the scripts:

**Windows (PowerShell):**
```powershell
.\deploy.ps1
```

**Linux/Mac:**
```bash
chmod +x deploy.sh
./deploy.sh
```

## After Deployment

1. **Go to Cloudflare Dashboard** → Pages → `balatrogenie` project
2. **Set Environment Variable:**
   - Variable: `API_BASE_URL`
   - Value: Your backend API URL (e.g., `https://your-api-server.com`)
3. **Add Custom Domain:**
   - Settings → Custom domains → Add `balatrogenie.app`

## Files Included

- `index.html` - Main page
- `app.js` - Frontend logic (auto-detects API URL)
- `style.css` - Styling
- `genie.svg` - Genie illustration
- `favicon.ico` - Favicon
- `_redirects` - Cloudflare Pages routing
- `wrangler.toml` - Wrangler config

## API URL Configuration

The frontend will automatically detect the API URL in this order:
1. Meta tag: `<meta name="api-base-url" content="...">` in index.html
2. Global variable: `window.API_BASE_URL`
3. Default: `window.location.origin` (same domain)

For Cloudflare Pages, set the `API_BASE_URL` environment variable in the dashboard.





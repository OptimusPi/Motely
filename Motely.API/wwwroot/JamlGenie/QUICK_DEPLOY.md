# Quick Deploy to BalatroGenie.app

## Option 1: Cloudflare Dashboard (Easiest)

1. **Go to Cloudflare Dashboard** → Pages → Create a project
2. **Connect your Git repository**
3. **Build settings:**
   - Framework: None
   - Build command: (empty)
   - Build output: `Motely.API/wwwroot/JamlGenie`
4. **Environment variables:**
   - Add `API_BASE_URL` = `https://your-backend-api.com` (your backend server URL)
5. **Custom domain:** Add `balatrogenie.app`
6. **Deploy!**

## Option 2: Wrangler CLI

```bash
cd Motely.API/wwwroot/JamlGenie
npm install -g wrangler
wrangler pages deploy . --project-name=balatrogenie
```

Then set `API_BASE_URL` in Cloudflare Pages dashboard.

## Important: Backend API URL

You need to set `API_BASE_URL` environment variable in Cloudflare Pages to point to your backend API server.

Example:
- `API_BASE_URL` = `https://api.yourserver.com`
- Or `http://192.168.0.171:3141` (if testing locally)

## After Deploy

1. Visit `https://balatrogenie.app`
2. Check browser console (F12) for any errors
3. Make sure your backend API has CORS enabled for `balatrogenie.app`





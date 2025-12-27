# Cloudflare Pages Setup Guide

## Quick Setup (2 Methods)

### Method 1: Environment Variable (Recommended)

1. **Go to:** https://dash.cloudflare.com
2. **Click:** Workers & Pages → Pages
3. **Click:** Your "balatrogenie" project
4. **Click:** Settings tab
5. **Scroll to:** "Environment variables"
6. **Click:** "Add variable"
7. **Enter:**
   - Variable name: `API_BASE_URL`
   - Value: `https://your-backend-api.com` (your actual API URL)
   - Environment: Production
8. **Click:** Save
9. **Done!** (Will apply on next deployment)

### Method 2: Meta Tag (Immediate)

1. **Edit:** `index.html` in the JamlGenie folder
2. **Find:** The commented meta tag around line 7-8
3. **Uncomment and set:**
   ```html
   <meta name="api-base-url" content="https://your-backend-api.com">
   ```
4. **Redeploy:**
   ```bash
   cd Motely.API/wwwroot/JamlGenie
   npx wrangler pages deploy . --project-name=balatrogenie
   ```

## Add Custom Domain (balatrogenie.app)

1. **Go to:** Cloudflare Dashboard → Pages → balatrogenie
2. **Click:** "Custom domains" tab
3. **Click:** "Set up a custom domain"
4. **Enter:** `balatrogenie.app`
5. **Click:** Continue
6. Cloudflare will automatically configure DNS

## Test Your Setup

1. Visit: https://balatrogenie.pages.dev (or your custom domain)
2. Open browser console (F12)
3. Check for errors
4. Try making a wish
5. Verify API calls go to your backend (check Network tab)

## Troubleshooting

**"Failed to fetch" errors:**
- Check API_BASE_URL is set correctly
- Verify your backend API is accessible
- Check CORS settings on your backend

**SignalR connection fails:**
- Ensure WebSocket is enabled on your backend
- Check CORS allows credentials
- Verify SignalR hub is at `/searchHub`





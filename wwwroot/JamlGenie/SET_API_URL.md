# How to Set API_BASE_URL in Cloudflare Pages

## Step-by-Step Instructions

### 1. Go to Cloudflare Dashboard
- Visit: https://dash.cloudflare.com
- Log in to your account

### 2. Navigate to Pages
- Click **"Workers & Pages"** in the left sidebar
- Click **"Pages"** (or find it in the top navigation)
- Find and click on your **"balatrogenie"** project

### 3. Go to Settings
- Click the **"Settings"** tab at the top
- Scroll down to find **"Environment variables"** section

### 4. Add Environment Variable
- Click **"Add variable"** button
- **Variable name:** `API_BASE_URL`
- **Value:** Your backend API URL (e.g., `https://your-api-server.com` or `http://192.168.0.171:3141`)
- **Environment:** Select **"Production"** (or "All environments" if you want it for all)
- Click **"Save"**

### 5. Redeploy (if needed)
- The environment variable will be available on the next deployment
- If you want to apply it immediately, go to **"Deployments"** tab
- Click the **"..."** menu on the latest deployment
- Click **"Retry deployment"** (or just wait for the next auto-deploy)

## Example Values

- **Local development:** `http://localhost:3141`
- **Local network:** `http://192.168.0.171:3141`
- **Production API:** `https://api.yourserver.com`
- **Same domain:** Leave empty (will use `window.location.origin`)

## Verify It's Working

1. Visit your deployed site: https://balatrogenie.pages.dev
2. Open browser console (F12)
3. Type: `window.API_BASE_URL` (should show your API URL)
4. Or check Network tab when making a wish - API calls should go to your backend

## Alternative: Use Meta Tag

If you prefer, you can also set it in the HTML file:

1. Edit `index.html`
2. Add this line in the `<head>` section:
   ```html
   <meta name="api-base-url" content="https://your-api-server.com">
   ```
3. Redeploy

The meta tag method works immediately without needing environment variables.





# Deploy JamlGenie to Cloudflare Workers

## Quick Setup (5 minutes)

### Step 1: Get Cloudflare Credentials

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com/)
2. Click your profile → **My Profile** → **API Tokens**
3. Click **Create Token** → **Create Custom Token**
4. Give it these permissions:
   - **Account** → **Workers Scripts** → **Edit**
   - **Account** → **Workers AI** → **Read** (if available)
5. Copy the token (you'll need it for GitHub secrets)

### Step 2: Get Your Account ID

1. In Cloudflare Dashboard, select your account
2. Copy the **Account ID** from the right sidebar

### Step 3: Enable Workers AI

1. Go to **Workers & Pages** → **AI**
2. Click **Enable Workers AI** (free tier available!)

### Step 4: Set GitHub Secrets

1. Go to your GitHub repo → **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret** and add:

   - **Name:** `CLOUDFLARE_API_TOKEN`
   - **Value:** (paste your API token from Step 1)

   - **Name:** `CLOUDFLARE_ACCOUNT_ID`
   - **Value:** (paste your Account ID from Step 2)

   - **Name:** `CLOUDFLARE_ACCOUNT_SUBDOMAIN` (optional, for custom domain)
   - **Value:** `your-subdomain` (or leave empty to use default workers.dev)

### Step 5: Deploy!

**Option A: Automatic (on push)**
- Just push changes to `worker/` folder
- GitHub Actions will deploy automatically

**Option B: Manual**
- Go to **Actions** tab in GitHub
- Click **Deploy JamlGenie Worker**
- Click **Run workflow**

### Step 6: Update app.js

After deployment, you'll get a URL like:
```
https://jamlgenie.your-subdomain.workers.dev
```

Update `app.js` line 3:
```javascript
const GENIE_API = 'https://jamlgenie.your-subdomain.workers.dev';
```

## Testing

1. Open `index.html` in browser
2. Type: "I want 2 blueprints yo"
3. Press Enter
4. Get JSON! 🧞✨

## Troubleshooting

**"Workers AI not enabled"**
- Go to Cloudflare Dashboard → Workers & Pages → AI
- Enable Workers AI

**"Invalid API token"**
- Make sure token has Workers Scripts Edit permission
- Regenerate token if needed

**"Account ID not found"**
- Check you're using the right account
- Account ID is in the right sidebar of dashboard

**Deployment fails**
- Check GitHub Actions logs
- Make sure all secrets are set correctly
- Verify Workers AI is enabled

## Custom Domain (Optional)

To use a custom domain like `jamlgenie.yourdomain.com`:

1. Add route in `wrangler.toml`:
```toml
routes = [
  { pattern = "jamlgenie.yourdomain.com", custom_domain = true }
]
```

2. Add DNS record in Cloudflare:
   - Type: CNAME
   - Name: jamlgenie
   - Target: your-subdomain.workers.dev

That's it! 🎉

# Deploy JamlGenie - Super Simple

## One Command Deploy

```powershell
cd Motely.API\wwwroot\JamlGenie
.\deploy.ps1
```

That's it! It will:
1. Install wrangler if needed
2. Deploy to Cloudflare Pages
3. Give you the URL

## Set API URL (One Time)

After first deploy, go to Cloudflare Dashboard:
1. Pages → `balatrogenie` project
2. Settings → Environment Variables
3. Add: `API_BASE_URL` = `https://your-backend-url.com`
4. Save

## Manual Deploy (if script fails)

```powershell
cd Motely.API\wwwroot\JamlGenie
npx wrangler pages deploy . --project-name=balatrogenie
```

## Add to GitHub/Balatro Seed Oracle

If you want to add this as a submodule or separate repo:

**Option A: Submodule (keeps it separate)**
```bash
cd BalatroSeedOracle
git submodule add https://github.com/yourusername/jamlgenie.git external/JamlGenie
```

**Option B: Just commit to existing repo**
```bash
cd BalatroSeedOracle
git add Motely.API/wwwroot/JamlGenie
git commit -m "Add JamlGenie frontend"
git push
```

Then in Cloudflare Pages, connect the GitHub repo and set build output to `Motely.API/wwwroot/JamlGenie`


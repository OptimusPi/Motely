# 🚀 Quick Start - Deploy JamlGenie

## 3 Steps to Deploy:

### 1️⃣ Get Cloudflare Secrets (2 min)

**Get API Token:**
- Cloudflare Dashboard → Profile → API Tokens
- Create Token → Custom Token
- Permissions: **Workers Scripts: Edit**
- Copy token

**Get Account ID:**
- Cloudflare Dashboard → Right sidebar
- Copy Account ID

**Enable Workers AI:**
- Workers & Pages → AI → Enable (free!)

### 2️⃣ Add GitHub Secrets (1 min)

GitHub Repo → Settings → Secrets → Actions → New secret:

- `CLOUDFLARE_API_TOKEN` = (your token)
- `CLOUDFLARE_ACCOUNT_ID` = (your account ID)

### 3️⃣ Deploy! (automatic)

**Just push this code** or manually:
- GitHub → Actions → "Deploy JamlGenie Worker" → Run workflow

**Get your URL:**
- After deployment, check Actions logs
- URL will be: `https://jamlgenie.YOUR-SUBDOMAIN.workers.dev`

**Update app.js:**
```javascript
const GENIE_API = 'https://jamlgenie.YOUR-SUBDOMAIN.workers.dev';
```

## Done! 🧞✨

Open `index.html` and test: "I want 2 blueprints yo"

---

**Full instructions:** See `DEPLOY.md`

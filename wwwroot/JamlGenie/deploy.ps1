# Deploy JamlGenie to Cloudflare Pages (BalatroGenie.app)

Write-Host "🚀 Deploying JamlGenie to Cloudflare Pages..." -ForegroundColor Cyan

# Check if wrangler is installed
if (-not (Get-Command wrangler -ErrorAction SilentlyContinue)) {
    Write-Host "📦 Installing wrangler..." -ForegroundColor Yellow
    npm install -g wrangler
}

# Deploy to Cloudflare Pages
Write-Host "📤 Deploying to balatrogenie project..." -ForegroundColor Green
wrangler pages deploy . --project-name=balatrogenie

Write-Host "✅ Deployment complete!" -ForegroundColor Green
Write-Host "🌐 Visit: https://balatrogenie.pages.dev" -ForegroundColor Cyan
Write-Host ""
Write-Host "⚠️  Don't forget to:" -ForegroundColor Yellow
Write-Host "   1. Set API_BASE_URL environment variable in Cloudflare Pages dashboard" -ForegroundColor Yellow
Write-Host "   2. Add custom domain balatrogenie.app in Pages settings" -ForegroundColor Yellow





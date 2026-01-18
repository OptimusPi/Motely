# Quick test script for JAML Genie
Write-Host "🚀 Testing JAML Genie Worker" -ForegroundColor Cyan
Write-Host ""

# Check if wrangler is installed
if (-not (Get-Command wrangler -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Wrangler not found. Install it:" -ForegroundColor Red
    Write-Host "   npm install -g wrangler" -ForegroundColor Yellow
    exit 1
}

# Check if we're in the right directory
if (-not (Test-Path "cloudflare-worker-jamlgenie\package.json")) {
    Write-Host "❌ Run this from external\Motely\Motely.API\ directory" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Step 1: Installing dependencies..." -ForegroundColor Yellow
Set-Location cloudflare-worker-jamlgenie
npm install

Write-Host ""
Write-Host "✅ Dependencies installed!" -ForegroundColor Green
Write-Host ""
Write-Host "🔧 Step 2: Starting dev server..." -ForegroundColor Yellow
Write-Host "   (This will start on http://localhost:8787)" -ForegroundColor Gray
Write-Host ""
Write-Host "   In another terminal, test with:" -ForegroundColor Cyan
Write-Host '   curl.exe -X POST http://localhost:8787 -H "Content-Type: application/json" -d "{\"prompt\":\"Blueprint and Brainstorm\"}"' -ForegroundColor White
Write-Host ""
Write-Host "   Or use PowerShell:" -ForegroundColor Cyan
Write-Host '   Invoke-RestMethod -Uri http://localhost:8787 -Method POST -ContentType "application/json" -Body ''{"prompt":"Blueprint and Brainstorm"}''' -ForegroundColor White
Write-Host ""

# Start wrangler dev
wrangler dev

# Deploy and Overwrite Old Prototype on balatrogenie.app
Write-Host "🚀 Deploying to balatrogenie.app (overwriting old prototype)" -ForegroundColor Cyan
Write-Host ""

# Check if wrangler is installed
if (-not (Get-Command wrangler -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Wrangler not found. Install it:" -ForegroundColor Red
    Write-Host "   npm install -g wrangler" -ForegroundColor Yellow
    exit 1
}

# Step 1: Deploy JAML Genie Worker
Write-Host "📦 Step 1: Deploying JAML Genie Worker..." -ForegroundColor Yellow
Set-Location cloudflare-worker-jamlgenie

if (-not (Test-Path "node_modules")) {
    Write-Host "   Installing dependencies..." -ForegroundColor Gray
    npm install
}

Write-Host "   Deploying (this will overwrite old worker if name matches)..." -ForegroundColor Gray
wrangler deploy

Write-Host "   ✅ JAML Genie deployed!" -ForegroundColor Green
Write-Host ""

# Step 2: Create/Update Vectorize Index
Write-Host "📊 Step 2: Setting up Vectorize index..." -ForegroundColor Yellow
Write-Host "   Checking if index exists..." -ForegroundColor Gray

$indexExists = wrangler vectorize list 2>&1 | Select-String "jaml-examples"
if ($indexExists) {
    Write-Host "   ✅ Index 'jaml-examples' already exists" -ForegroundColor Green
} else {
    Write-Host "   Creating index..." -ForegroundColor Gray
    wrangler vectorize create jaml-examples --dimensions=768 --metric=cosine --description="JAML filter examples for RAG"
    Write-Host "   ✅ Index created!" -ForegroundColor Green
}

Write-Host ""

# Step 3: Seed Vectorize Index
Write-Host "🌱 Step 3: Seeding Vectorize index..." -ForegroundColor Yellow
$env:JAMLGENIE_WORKER_URL="https://jamlgenie.balatrogenie.app"
Write-Host "   Generating embeddings and inserting into Vectorize..." -ForegroundColor Gray
node seed-vectorize.js

Write-Host "   ✅ Vectorize seeded!" -ForegroundColor Green
Write-Host ""

# Step 4: Deploy MCP Server Worker
Set-Location ..\cloudflare-worker
Write-Host "🔌 Step 4: Deploying MCP Server Worker..." -ForegroundColor Yellow

if (-not (Test-Path "node_modules")) {
    Write-Host "   Installing dependencies..." -ForegroundColor Gray
    npm install
}

Write-Host "   Deploying (this will overwrite old worker if name matches)..." -ForegroundColor Gray
wrangler deploy

Write-Host "   ✅ MCP Server deployed!" -ForegroundColor Green
Write-Host ""

# Step 5: Test Both Workers
Write-Host "🧪 Step 5: Testing deployments..." -ForegroundColor Yellow
Write-Host ""

Write-Host "   Testing JAML Genie..." -ForegroundColor Gray
try {
    $jamlResponse = Invoke-RestMethod -Uri "https://jamlgenie.balatrogenie.app" -Method POST -ContentType "application/json" -Body '{"prompt":"Blueprint and Brainstorm"}' -ErrorAction Stop
    Write-Host "   ✅ JAML Genie working! Response:" -ForegroundColor Green
    Write-Host "      Success: $($jamlResponse.success)" -ForegroundColor Gray
    if ($jamlResponse.jaml) {
        Write-Host "      JAML generated: $($jamlResponse.jaml.Substring(0, [Math]::Min(50, $jamlResponse.jaml.Length)))..." -ForegroundColor Gray
    }
} catch {
    Write-Host "   ⚠️  JAML Genie test failed: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "   Testing MCP Server..." -ForegroundColor Gray
try {
    $mcpResponse = Invoke-RestMethod -Uri "https://mcp.balatrogenie.app" -Method POST -ContentType "application/json" -Body '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' -ErrorAction Stop
    Write-Host "   ✅ MCP Server working! Response:" -ForegroundColor Green
    if ($mcpResponse.result -and $mcpResponse.result.tools) {
        Write-Host "      Tools available: $($mcpResponse.result.tools.Count)" -ForegroundColor Gray
        foreach ($tool in $mcpResponse.result.tools) {
            Write-Host "        - $($tool.name)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "   ⚠️  MCP Server test failed: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Workers deployed:" -ForegroundColor Cyan
Write-Host "  🌐 JAML Genie: https://jamlgenie.balatrogenie.app" -ForegroundColor White
Write-Host "  🔌 MCP Server: https://mcp.balatrogenie.app" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Go to https://dash.cloudflare.com to verify workers" -ForegroundColor White
Write-Host "  2. Test MCP server in Cursor/Claude Desktop" -ForegroundColor White
Write-Host "  3. Check Vectorize index: wrangler vectorize list" -ForegroundColor White

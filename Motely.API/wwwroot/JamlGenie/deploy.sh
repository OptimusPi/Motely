#!/bin/bash
# Deploy JamlGenie to Cloudflare Pages (BalatroGenie.app)

echo "🚀 Deploying JamlGenie to Cloudflare Pages..."

# Check if wrangler is installed
if ! command -v wrangler &> /dev/null; then
    echo "📦 Installing wrangler..."
    npm install -g wrangler
fi

# Deploy to Cloudflare Pages
echo "📤 Deploying to balatrogenie project..."
wrangler pages deploy . --project-name=balatrogenie

echo "✅ Deployment complete!"
echo "🌐 Visit: https://balatrogenie.pages.dev"
echo ""
echo "⚠️  Don't forget to:"
echo "   1. Set API_BASE_URL environment variable in Cloudflare Pages dashboard"
echo "   2. Add custom domain balatrogenie.app in Pages settings"





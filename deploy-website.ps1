#!/usr/bin/env pwsh

# Automated deployment script for Motely Seed Searcher Website
# This script builds the .NET WASM framework and deploys the website

Write-Host "🚀 Starting Motely Website Deployment..." -ForegroundColor Green

# Build .NET WASM framework
Write-Host "📦 Building .NET WASM framework..." -ForegroundColor Yellow
dotnet build ./Motely.Run -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET build failed!" -ForegroundColor Red
    exit 1
}

# Copy WASM files to wwwroot
Write-Host "📋 Copying WASM framework files..." -ForegroundColor Yellow
$source = "./Motely.Run/bin/Release/net10.0-browser/browser-wasm/publish/*"
$dest = "./wwwroot/motely-framework/"
Copy-Item -Path "./Motely.Run/bin/Release/net10.0-browser/browser-wasm/publish/*" -Destination "./wwwroot/motely-framework/" -Recurse -Force
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to copy WASM files!" -ForegroundColor Red
    exit 1
}

# Copy all wwwroot to public directory
Write-Host "📋 Copying static files to public directory..." -ForegroundColor Yellow
Copy-Item -Path "./wwwroot/*" -Destination "./Motely.SeedSearcherWebsite/public/" -Recurse -Force
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to copy static files!" -ForegroundColor Red
    exit 1
}

# Build Next.js website
Write-Host "🏗️ Building Next.js website..." -ForegroundColor Yellow
Set-Location ./Motely.SeedSearcherWebsite
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Next.js build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
Write-Host "🌐 Website is ready in: ./Motely.SeedSearcherWebsite/out" -ForegroundColor Cyan
Write-Host "🔧 To run locally: npm run start" -ForegroundColor Cyan

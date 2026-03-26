@echo off
echo 🚀 Starting Motely Website Deployment...

echo 📦 Building .NET WASM framework...
dotnet build ./Motely.Run -c Release --verbosity quiet
if errorlevel 1 goto :error

echo 📋 Copying WASM framework files...
xcopy "./Motely.Run/bin/Release/net10.0-browser/browser-wasm/publish/*" "./wwwroot/motely-framework/" /E /Y
if errorlevel 1 goto :error

echo 📋 Copying static files to public directory...
xcopy "./wwwroot/*" "./Motely.SeedSearcherWebsite/public/" /E /Y
if errorlevel 1 goto :error

echo 🏗️ Building Next.js website...
cd ./Motely.SeedSearcherWebsite
call npm run build
if errorlevel 1 goto :error

echo ✅ Deployment completed successfully!
echo 🌐 Website is ready in: ./Motely.SeedSearcherWebsite/out
echo 🔧 To run locally: npm run start
goto :end

:error
echo ❌ Deployment failed!
exit /b 1

:end

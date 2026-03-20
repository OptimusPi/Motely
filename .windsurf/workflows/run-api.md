---
description: Run the Motely API dev server
---

## Step 1: Copy WASM framework to wwwroot

Ensures `_framework_st` is staged before the server starts serving static files.

// turbo
```powershell
node x:\JammySeedFinder\src\MotelyJAML\Motely.API\copy-wasm.mjs
```

## Step 2: Start the API

// turbo
```powershell
dotnet run --project x:\JammySeedFinder\src\MotelyJAML\Motely.API\Motely.API.csproj --launch-profile http
```

The dashboard opens at: http://192.168.0.171:3141/
API status endpoint: http://192.168.0.171:3141/api/status

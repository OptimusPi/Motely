# Build & Publish motely-wasm@2.2.0 and motely-node@2.2.0

**Windows PowerShell Edition**

---

## PHASE 1: Build C# WASM (generates framework files)

```powershell
cd x:\JammySeedFinder\src\MotelyJAML

# 1. Clean
dotnet clean
Remove-Item -Recurse -Force */bin, */obj -ErrorAction SilentlyContinue

# 2. Publish BrowserWasm (outputs to Motely.npm/_framework)
dotnet publish Motely.BrowserWasm/Motely.BrowserWasm.csproj -c Release
node stage-packages.mjs browser

# 3. Publish SingleThread (outputs to Motely.node/_framework)
dotnet publish Motely.SingleThread/Motely.SingleThread.csproj -c Release
node stage-packages.mjs singlethread node

# 4. Verify files exist
Test-Path Motely.npm\_framework\dotnet.wasm
Test-Path Motely.node\_framework\dotnet.wasm
```

---

## PHASE 2: Build & Package Browser NPM (motely-wasm@2.2.0)

```powershell
cd x:\JammySeedFinder\src\MotelyJAML\Motely.npm

# 5. Install dependencies
npm install

# 6. Build TypeScript → JavaScript
npm run build

# 7. Verify package contents
Get-ChildItem -Force
# Should have: index.js, index.d.ts, README.md, _framework/, jaml.schema.json

# 8. Test locally (optional)
npm link

# 9. Publish to npm
npm publish
```

---

## PHASE 3: Build & Package Node.js NPM (motely-node@2.2.0)

```powershell
cd x:\JammySeedFinder\src\MotelyJAML\Motely.node

# 10. Install dependencies
npm install

# 11. Build TypeScript → JavaScript
npm run build

# 12. Stage framework files
npm run stage-framework

# 13. Verify package contents
Get-ChildItem -Force
# Should have: index.js, index.cjs, index.d.ts, README.md, _framework/, jaml.schema.json

# 14. Test locally (optional)
npm link

# 15. Publish to npm
npm publish
```

---

## PHASE 4: Verify Published Packages

```powershell
# 16. Check npm registry
npm view motely-wasm@2.2.0
npm view motely-node@2.2.0

# 17. Test installation in a fresh directory
mkdir C:\temp\test-motely
cd C:\temp\test-motely
npm init -y
npm install motely-wasm@2.2.0
npm install motely-node@2.2.0

# 18. Verify types are available
Get-Content node_modules\motely-wasm\index.d.ts -Head 20
Get-Content node_modules\motely-node\index.d.ts -Head 20
```

---

## One-Liner Cleanup (if needed)

```powershell
# Remove all bin/obj directories recursively
Get-ChildItem -Path x:\JammySeedFinder\src\MotelyJAML -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

---

## Troubleshooting

**"npm: The term 'npm' is not recognized"**
- Install Node.js from https://nodejs.org (includes npm)
- Restart PowerShell after install

**"dotnet: The term 'dotnet' is not recognized"**
- Install .NET 10 SDK from https://dotnet.microsoft.com/download
- Restart PowerShell after install

**"Access Denied" when deleting bin/obj**
- Close any open Visual Studio instances
- Run PowerShell as Administrator

**npm publish fails with 403**
- Run `npm login` first
- Verify you're logged in: `npm whoami`
- Check package name is unique: `npm view motely-wasm`

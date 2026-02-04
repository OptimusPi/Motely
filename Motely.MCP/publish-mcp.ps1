param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish",
    [switch]$Npm
)

$projectPath = Join-Path $PSScriptRoot "Motely.MCP.csproj"
$publishPath = Join-Path $PSScriptRoot $OutputDir
$npmServerPath = Join-Path $PSScriptRoot ".." "motely-mcp-server"

if (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
}

Write-Host "Building Motely.MCP for $Runtime..." -ForegroundColor Cyan

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $publishPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$exeName = if ($Runtime -like "win-*") { "Motely.MCP.exe" } else { "Motely.MCP" }
$exePath = Join-Path $publishPath $exeName

if (Test-Path $exePath) {
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "Executable: $exePath" -ForegroundColor Yellow
    
    # Copy to npm package bin folder if -Npm flag is set
    if ($Npm) {
        $npmBinPath = Join-Path $npmServerPath "bin"
        if (-not (Test-Path $npmBinPath)) {
            New-Item -ItemType Directory -Path $npmBinPath -Force | Out-Null
        }
        
        # Copy all files from publish to npm bin
        Copy-Item -Path "$publishPath\*" -Destination $npmBinPath -Recurse -Force
        
        Write-Host ""
        Write-Host "Copied to npm package: $npmBinPath" -ForegroundColor Green
        
        # Build the npm package
        Write-Host ""
        Write-Host "Building npm package..." -ForegroundColor Cyan
        Push-Location $npmServerPath
        try {
            npm install
            npm run build
            Write-Host "npm package built successfully!" -ForegroundColor Green
        } finally {
            Pop-Location
        }
    }
    
    Write-Host ""
    Write-Host "MCP Configuration for Claude Desktop / Cursor:" -ForegroundColor Cyan
    Write-Host @"

Option 1: Direct executable
{
  "mcpServers": {
    "motely": {
      "command": "$($exePath.Replace('\', '/'))",
      "args": ["--mcp-stdio"],
      "env": {
        "MCP_MODE": "stdio"
      }
    }
  }
}

Option 2: Via npx (after publishing npm package)
{
  "mcpServers": {
    "motely": {
      "command": "npx",
      "args": ["@balatroseedoracle/motely-mcp-server"],
      "env": {}
    }
  }
}
"@
}

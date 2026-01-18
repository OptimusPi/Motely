param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputDir = "publish"
)

$projectPath = Join-Path $PSScriptRoot "Motely.MCP.csproj"
$publishPath = Join-Path $PSScriptRoot $OutputDir

if (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
}

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $publishPath `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true

if ($LASTEXITCODE -ne 0) {
    exit 1
}

$exeName = if ($Runtime -like "win-*") { "Motely.MCP.exe" } else { "Motely.MCP" }
$exePath = Join-Path $publishPath $exeName

if (Test-Path $exePath) {
    Write-Host "Executable: $exePath"
    Write-Host ""
    Write-Host "Cursor MCP config:"
    Write-Host @"
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "$($exePath.Replace('\', '/'))",
      "args": ["--mcp-stdio"],
      "env": {
        "MCP_MODE": "stdio"
      }
    }
  }
}
"@
}

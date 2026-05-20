# Regenerate jaml.schema.json and motely-item-formats.* to every DefaultOutputPaths sink.
# Run from repo root:  .\regen-jaml-schema.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "Building Motely.CLI..."
dotnet build Motely.CLI/Motely.CLI.csproj -c Release -v q

Write-Host "Generating JAML schema + item format artifacts..."
dotnet run --project Motely.CLI/Motely.CLI.csproj -c Release --no-build -- schema
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running JamlSchemaSnapshotTests..."
dotnet test Motely.Tests/Motely.Tests.csproj -c Release --filter "FullyQualifiedName~JamlSchema" -v q
exit $LASTEXITCODE

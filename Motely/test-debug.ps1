Write-Host "Testing ALEEB Blueprint search..."
$env:DEBUG = "true"
& dotnet run -c Release -- --json test-aleeb-simple --seed ALEEB 2>&1 | Select-String -Pattern "Blueprint|Joker|ante 2" | Select-Object -First 20
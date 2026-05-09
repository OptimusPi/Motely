# Clean bootsharp intermediates so BootsharpEmit re-inspects fresh assemblies, then publish.
$bootsharpIntermediates = "$PSScriptRoot\obj\Release\net10.0\browser-wasm\bootsharp"
if (Test-Path $bootsharpIntermediates) {
    Remove-Item -Recurse -Force $bootsharpIntermediates
}

Set-Location "$PSScriptRoot\.."
dotnet publish "$PSScriptRoot\Motely.Wasm.csproj" -c Release

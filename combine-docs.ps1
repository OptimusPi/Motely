$files = Get-ChildItem 'D:\bootsharp\docs\guide\*.md' | Sort-Object Name
$out = ''
foreach ($f in $files) {
    $out += "# $($f.Name)`n"
    $out += (Get-Content $f.FullName -Raw)
    $out += "`n`n"
}
$out | Out-File 'X:\JammySeedFinder\src\MotelyJAML\bootsharp-docs.md' -Encoding utf8
Write-Host "done"

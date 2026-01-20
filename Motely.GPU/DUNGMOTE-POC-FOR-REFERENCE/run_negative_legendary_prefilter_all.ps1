# Walk the entire 8-char seed space in chunks for negative_legendary_prefilter.
param(
    [UInt64]$Chunk = 100000000,              # seeds per chunk
    [string]$Antes = "2,10",                 # comma-separated
    [int]$MinHits = 3,                       # --min-hits
    [string]$StartSeed = "11111111",         # starting seed
    [string]$OutputFile = "",                # optional append output
    [int]$BlockSize = 256,
    [int]$BlocksPerSm = 32
)

if (-not (Test-Path ".\\negative_legendary_prefilter.exe")) {
    .\build.ps1 legendary_prefilter
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

$chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray()

function Convert-IndexToSeed([UInt64]$idx) {
    $s = ""
    for ($i = 0; $i -lt 8; $i++) {
        $s = $chars[$idx % 35] + $s
        $idx = [UInt64]([Math]::Floor($idx / 35))
    }
    return $s
}

function SeedToIndex([string]$seed) {
    # uses same alphabet as balatro_rng.cuh
    $map = @{}
    for ($i = 0; $i -lt 35; $i++) { $map[$chars[$i]] = [UInt64]$i }
    [UInt64]$idx = 0
    for ($i = 0; $i -lt 8; $i++) {
        $c = $seed[$i]
        $idx = ($idx * 35) + $map[$c]
    }
    return $idx
}

[UInt64]$total = 1
for ($i=0; $i -lt 8; $i++) { $total *= 35 }

[UInt64]$startIdx = SeedToIndex $StartSeed

while ($startIdx -lt $total) {
    [UInt64]$count = $Chunk
    if ($startIdx + $count -gt $total) { $count = $total - $startIdx }

    $seed = Convert-IndexToSeed $startIdx

    $args = @("$count", $Antes, $seed, "--min-hits", "$MinHits", "--block-size", "$BlockSize", "--blocks-per-sm", "$BlocksPerSm")

    if ($OutputFile -ne "") {
        # append run output
        & .\negative_legendary_prefilter.exe @args | Out-File -FilePath $OutputFile -Append -Encoding utf8
    } else {
        & .\negative_legendary_prefilter.exe @args
    }

    $startIdx += $count
}


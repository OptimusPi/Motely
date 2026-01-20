# Build script for Balatro GPU Seed Searcher (CUDA/HIP)
param(
    [Parameter(Position=0)]
    [string]$Target = "soul_search",
    [switch]$HIP = $false
)

# Detect HIP from environment or parameter
if ($env:HIP -eq "1" -or $HIP) {
    $USE_HIP = $true
    $HIP_ARCH = "gfx1100"  # RDNA 3 (RX 7000 series) - adjust for your GPU
    $HIPCC_FLAGS = "-O2 --offload-arch=$HIP_ARCH --fmad=false"
    Write-Host "Building with HIP (AMD ROCm)..." -ForegroundColor Cyan
} else {
    $USE_HIP = $false
    $CUDA_ARCH = "sm_89"
    $NVCC_FLAGS = "-O2 -arch=$CUDA_ARCH --fmad=true"
    Write-Host "Building with CUDA (NVIDIA)..." -ForegroundColor Green
}

$VS_PATH = "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"

switch ($Target) {
    "soul_search" {
        Write-Host "Building soul_joker_filter_search.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o soul_joker_filter_search.exe soul_joker_filter_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o soul_joker_filter_search.exe soul_joker_filter_search.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "erratic" {
        Write-Host "Building erratic_search.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o erratic_search.exe erratic_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o erratic_search.exe erratic_search.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "raw_soul" {
        Write-Host "Building raw_soul_edition_check.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o raw_soul_edition_check.exe raw_soul_edition_check.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o raw_soul_edition_check.exe raw_soul_edition_check.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "soul_edition" {
        Write-Host "Building soul_edition_search.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o soul_edition_search.exe soul_edition_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o soul_edition_search.exe soul_edition_search.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "legendary_prefilter" {
        Write-Host "Building negative_legendary_prefilter.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_legendary_prefilter.exe negative_legendary_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_legendary_prefilter.exe negative_legendary_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "rare_prefilter" {
        Write-Host "Building negative_rare_prefilter.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_rare_prefilter.exe negative_rare_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_rare_prefilter.exe negative_rare_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "uncommon_prefilter" {
        Write-Host "Building negative_uncommon_prefilter.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_uncommon_prefilter.exe negative_uncommon_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_uncommon_prefilter.exe negative_uncommon_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "negative_tag_skipper" {
        Write-Host "Building negative_tag_skipper.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_tag_skipper.exe negative_tag_skipper.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_tag_skipper.exe negative_tag_skipper.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "economy_rush" {
        Write-Host "Building economy_rush_search.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o economy_rush_search.exe economy_rush_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o economy_rush_search.exe economy_rush_search.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "joker_prefilter" {
        Write-Host "Building negative_joker_prefilter.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_joker_prefilter.exe negative_joker_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_joker_prefilter.exe negative_joker_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "ultimate_filter" {
        Write-Host "Building ultimate_filter.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o ultimate_filter.exe ultimate_filter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o ultimate_filter.exe ultimate_filter.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "consecutive_negative" {
        Write-Host "Building consecutive_negative_checker.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o consecutive_negative_checker.exe consecutive_negative_checker.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o consecutive_negative_checker.exe consecutive_negative_checker.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "showman_consecutive" {
        Write-Host "Building showman_consecutive_filter.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o showman_consecutive_filter.exe showman_consecutive_filter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o showman_consecutive_filter.exe showman_consecutive_filter.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "verify" {
        Write-Host "Building verify_rng.exe..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o verify_rng.exe verify_rng.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o verify_rng.exe verify_rng.cu"
        }
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Built successfully!"
        }
    }
    "clean" {
        Write-Host "Cleaning build artifacts..."
        Remove-Item -ErrorAction SilentlyContinue *.exe, *.exp, *.lib, *.obj
        Write-Host "✓ Cleaned!"
    }
    "all" {
        Write-Host "Building all targets..."
        Write-Host ""
        
        # Pre-filters (4)
        Write-Host "[1/13] Building joker_prefilter..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_joker_prefilter.exe negative_joker_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_joker_prefilter.exe negative_joker_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[2/13] Building legendary_prefilter..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_legendary_prefilter.exe negative_legendary_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_legendary_prefilter.exe negative_legendary_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[3/13] Building rare_prefilter..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_rare_prefilter.exe negative_rare_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_rare_prefilter.exe negative_rare_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[4/13] Building uncommon_prefilter..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_uncommon_prefilter.exe negative_uncommon_prefilter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_uncommon_prefilter.exe negative_uncommon_prefilter.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        # Other searches (9)
        Write-Host "[5/13] Building soul_search..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o soul_joker_filter_search.exe soul_joker_filter_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o soul_joker_filter_search.exe soul_joker_filter_search.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[6/13] Building soul_edition..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o soul_edition_search.exe soul_edition_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o soul_edition_search.exe soul_edition_search.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[7/13] Building raw_soul..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o raw_soul_edition_check.exe raw_soul_edition_check.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o raw_soul_edition_check.exe raw_soul_edition_check.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[8/13] Building erratic..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o erratic_search.exe erratic_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o erratic_search.exe erratic_search.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }

        Write-Host "[9/13] Building negative_tag_skipper..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o negative_tag_skipper.exe negative_tag_skipper.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o negative_tag_skipper.exe negative_tag_skipper.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }

        Write-Host "[10/13] Building economy_rush..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o economy_rush_search.exe economy_rush_search.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o economy_rush_search.exe economy_rush_search.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }

        Write-Host "[11/13] Building ultimate_filter..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o ultimate_filter.exe ultimate_filter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o ultimate_filter.exe ultimate_filter.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[12/13] Building consecutive_negative..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o consecutive_negative_checker.exe consecutive_negative_checker.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o consecutive_negative_checker.exe consecutive_negative_checker.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host "[13/13] Building showman_consecutive..."
        if ($USE_HIP) {
            cmd /c "`"$VS_PATH`" && hipcc $HIPCC_FLAGS -o showman_consecutive_filter.exe showman_consecutive_filter.cu"
        } else {
            cmd /c "`"$VS_PATH`" && nvcc $NVCC_FLAGS -o showman_consecutive_filter.exe showman_consecutive_filter.cu"
        }
        if ($LASTEXITCODE -eq 0) { Write-Host "✓" } else { Write-Host "✗ FAILED" }
        
        Write-Host ""
        Write-Host "✓ All builds complete!"
    }
    default {
        Write-Host "Usage: .\build.ps1 [target]"
        Write-Host ""
        Write-Host "Build all:"
        Write-Host "  all                - Build all executables"
        Write-Host ""
        Write-Host "Fast Pre-filters (use these!):"
        Write-Host "  joker_prefilter     - Negative jokers (any rarity) - FASTEST!"
        Write-Host "  legendary_prefilter - Negative legendary from Soul cards"
        Write-Host "  rare_prefilter      - Negative rare from shop slots"
        Write-Host "  uncommon_prefilter  - Negative uncommon from shop slots"
        Write-Host ""
        Write-Host "Utilities:"
        Write-Host "  verify              - RNG verification test"
        Write-Host ""
        Write-Host "Other searches:"
        Write-Host "  soul_search      - Full legendary filter search"
        Write-Host "  soul_edition     - Configurable legendary search"
        Write-Host "  raw_soul         - Simple ante 1 check"
        Write-Host "  erratic          - Erratic deck search"
        Write-Host "  negative_tag_skipper - Negative tag + consecutive joker search"
        Write-Host "  economy_rush     - Find seeds with strong early economy"
        Write-Host "  ultimate_filter  - Find Perkeo from Soul card in Spectral packs"
        Write-Host "  consecutive_negative - Check seeds one at a time for consecutive negative jokers"
        Write-Host "  clean            - Delete all .exe files"
    }
}


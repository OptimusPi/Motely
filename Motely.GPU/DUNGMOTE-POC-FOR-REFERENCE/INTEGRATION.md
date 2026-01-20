# Integration Guide for Graphical Seed Searchers

This guide explains how to integrate the CUDA seed searchers into graphical applications like Ouija or other GUI-based seed search tools.

## Overview

The CUDA searchers are compiled as standalone executables. To integrate them into a graphical application, you have several options:

1. **C API Wrapper (Recommended)** - Wrap CUDA kernels in C functions callable from other languages
2. **Process Spawning** - Launch executables as subprocesses and parse output
3. **DLL/Shared Library** - Compile CUDA code as a DLL/shared library

## Option 1: C API Wrapper (Recommended)

Create a wrapper that exposes CUDA kernels through a C API:

### Example: `dungmot_api.cu`

```cuda
#include "negative_rare_prefilter.cu"

extern "C" {
    // Wrapper function that can be called from C/C++/C#
    int search_negative_rare(
        uint64_t start_idx,
        uint64_t num_seeds,
        int* antes,
        int num_antes,
        char* output_buffer,
        size_t buffer_size
    ) {
        // Allocate device memory
        // Launch kernel
        // Copy results to output_buffer
        // Return count
    }
}
```

### Compilation

```bash
nvcc --shared -o dungmot_api.dll dungmot_api.cu -arch=sm_89
```

### Usage from C#

```csharp
[DllImport("dungmot_api.dll")]
static extern int search_negative_rare(
    ulong startIdx,
    ulong numSeeds,
    int[] antes,
    int numAntes,
    StringBuilder output,
    int bufferSize
);
```

## Option 2: Process Spawning

Launch executables as subprocesses and parse CSV output:

### C# Example

```csharp
ProcessStartInfo psi = new ProcessStartInfo {
    FileName = "negative_rare_prefilter.exe",
    Arguments = $"{seedCount} {antes} {startSeed} output.csv",
    RedirectStandardOutput = true,
    UseShellExecute = false
};

Process proc = Process.Start(psi);
string output = proc.StandardOutput.ReadToEnd();
proc.WaitForExit();

// Parse CSV: seed,hit_count
var results = output.Split('\n')
    .Where(line => !string.IsNullOrEmpty(line))
    .Select(line => {
        var parts = line.Split(',');
        return new { Seed = parts[0], Hits = int.Parse(parts[1]) };
    });
```

### Progress Monitoring

The prefilters print progress to stdout every 0.1%:
```
Progress: 12.34% | Seed: ABC12345 | Matches: 42
```

Parse these lines to update progress bars in your GUI.

## Option 3: DLL/Shared Library

Compile CUDA code as a DLL:

### Build Command

```powershell
nvcc --shared -o dungmot.dll negative_rare_prefilter.cu -arch=sm_89
```

### Export Functions

Use `extern "C"` to export functions:

```cuda
extern "C" __declspec(dllexport) int SearchNegativeRare(...) {
    // Implementation
}
```

## Resume Capability

To resume a search:

1. Sort output CSV alphabetically by seed
2. Extract the last seed from the sorted file
3. Use that seed as `start_seed` for the next run

Example PowerShell:
```powershell
$lastSeed = (Get-Content output.csv | Sort-Object | Select-Object -Last 1).Split(',')[0]
.\run_rare_prefilter.ps1 1000000 "1,2,3" $lastSeed output.csv
```

## Reference: Ouija/Immolate Integration

The original Ouija seed searcher (based on immolate) used OpenCL. This CUDA implementation follows similar patterns:

- **Kernel-based parallelization** - Each thread processes a range of seeds
- **Early exit optimization** - Stop checking when filter conditions fail
- **Stream-based RNG** - Maintain PRNG state for sequential random calls
- **CSV output format** - Simple, parseable results

## Performance Considerations

- **GPU Memory**: Ensure GPU has enough memory for kernel launches
- **Block Size**: Default is 256 threads per block (tuned for most GPUs)
- **Progress Updates**: Progress prints every 0.1% to minimize overhead
- **Output Buffering**: stdout is unbuffered for real-time results

## Example Integration Workflow

1. User selects filter type (rare/uncommon/legendary)
2. User specifies search range (start seed, count, antes)
3. GUI spawns prefilter executable as subprocess
4. Parse progress lines to update progress bar
5. Parse result lines to populate results table
6. Allow user to resume from last seed if search interrupted

## Troubleshooting

- **CUDA not found**: Ensure CUDA toolkit is installed and in PATH
- **GPU not detected**: Check `nvidia-smi` output
- **Build errors**: Ensure Visual Studio build tools are installed
- **Performance**: Use `--fmad=false` flag for accuracy (required!)


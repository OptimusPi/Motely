# GPU Tuning Guide for RTX 4070 SUPER

## What Do These Parameters Mean?

### `--block-size` (Threads Per Block)
- **What it is**: How many threads work together in one "block"
- **Range**: 128, 256, 512, 1024 (must be power of 2)
- **Effect**: 
  - Smaller (128): More blocks can run simultaneously, better for memory-bound workloads
  - Larger (512-1024): Better for compute-intensive workloads, fewer blocks but more threads per block
- **RTX 4070 SUPER**: Can handle up to 1024 threads per block

### `--blocks-per-sm` (Blocks Per Streaming Multiprocessor)
- **What it is**: How many blocks each SM (Streaming Multiprocessor) can run at once
- **Range**: 8, 16, 32, 64 (typical values)
- **Effect**:
  - Lower (8-16): Fewer blocks but more resources per block
  - Higher (32-64): More blocks, better occupancy, but less resources per block
- **RTX 4070 SUPER**: Has 56 SMs, so `blocks_per_sm × 56 = total blocks`

### Total Threads Calculation
```
Total Threads = blocks_per_sm × 56 (SMs) × block_size
```

**Example**: `--block-size 512 --blocks-per-sm 16`
- Total blocks = 16 × 56 = 896 blocks
- Total threads = 896 × 512 = 458,752 threads

## How to Find the Best Settings

### Method 1: Automated Benchmark Script
```powershell
.\benchmark_gpu.ps1 -SeedCount 1000000 -Antes "1,2,3"
```

This will test multiple configurations and show you which is fastest.

### Method 2: Manual Testing
Run the same search with different settings and compare the "Rate" output:

```powershell
# Test 1: Default
.\negative_joker_prefilter.exe 1000000 "1,2,3" --block-size 256 --blocks-per-sm 32

# Test 2: High occupancy
.\negative_joker_prefilter.exe 1000000 "1,2,3" --block-size 128 --blocks-per-sm 64

# Test 3: Large blocks
.\negative_joker_prefilter.exe 1000000 "1,2,3" --block-size 512 --blocks-per-sm 16
```

**Look for**: Higher "Rate: X.XX M seeds/sec" = faster

### Method 3: Understanding Occupancy

**Occupancy** = How many threads are active vs. maximum possible

- **High occupancy** (more blocks): Better for memory-bound, hides memory latency
- **Low occupancy** (fewer, larger blocks): Better for compute-bound, more registers per thread

**For negative joker prefilter** (memory + compute):
- Usually benefits from **moderate-high occupancy**
- Try: `--block-size 256 --blocks-per-sm 32` or `--block-size 512 --blocks-per-sm 16`

## RTX 4070 SUPER Specific Recommendations

### Starting Point (Usually Good)
```powershell
--block-size 256 --blocks-per-sm 32
```
- 1,792 blocks × 256 threads = 458,752 total threads
- Good balance of occupancy and resources

### High Throughput (Try This First)
```powershell
--block-size 512 --blocks-per-sm 16
```
- 896 blocks × 512 threads = 458,752 total threads
- Often fastest for compute-heavy workloads

### Maximum Occupancy
```powershell
--block-size 128 --blocks-per-sm 64
```
- 3,584 blocks × 128 threads = 458,752 total threads
- Best for memory-bound workloads

## What to Look For

1. **Rate (M seeds/sec)**: Higher is better - this is your throughput
2. **Time (ms)**: Lower is better - how long the search took
3. **GPU Utilization**: Use `nvidia-smi` while running to see GPU usage
   - 95-100% = good utilization
   - <80% = might benefit from more blocks

## Common Patterns

- **Memory-bound** (lots of random memory access): Smaller block_size, more blocks_per_sm
- **Compute-bound** (lots of math): Larger block_size, fewer blocks_per_sm
- **Balanced**: Medium block_size, medium blocks_per_sm

## Quick Test Command

```powershell
# Run benchmark on 1M seeds
.\benchmark_gpu.ps1 -SeedCount 1000000 -Antes "1,2,3"
```

This will test 6 different configurations and tell you which is fastest!


# Standard Batch Processing Pattern

**ALL FILTERS MUST USE THIS PATTERN** - no more duplicating code!

## The Problem
Every filter was duplicating:
- Batch chunking logic
- Sync patterns
- Memory management
- Result collection
- Progress updates

## The Solution
Use `balatro_batch_main.cuh` and follow this pattern:

## Standard Pattern

```c
#include "balatro_batch_main.cuh"  // Add this!

// In your main() function, use this pattern:

uint64_t batches_per_chunk = calculate_batches_per_chunk(batch_chars);

for (uint64_t chunk_start = start_batch; 
     chunk_start <= end_batch && chunk_start < calculate_total_batches(batch_chars); 
     chunk_start += batches_per_chunk) {
    
    uint64_t chunk_end = chunk_start + batches_per_chunk - 1;
    if (chunk_end > end_batch) chunk_end = end_batch;
    if (chunk_end >= calculate_total_batches(batch_chars)) {
        chunk_end = calculate_total_batches(batch_chars) - 1;
    }
    
    // Launch all batches in chunk (async, no sync yet)
    for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
        cudaMemcpyAsync(d_result_buffer_count, &zero, sizeof(int), cudaMemcpyHostToDevice);
        
        your_kernel<<<num_blocks, block_size>>>(
            batch, batch_chars,
            // ... your kernel args
        );
        cudaGetLastError();  // Check for launch errors
    }
    
    // Sync ONCE per chunk (not per batch) - reduces overhead, smoother GPU
    cudaDeviceSynchronize();
    
    // Collect results from all batches in chunk
    for (uint64_t batch = chunk_start; batch <= chunk_end; batch++) {
        int buf_count = 0;
        cudaMemcpy(&buf_count, d_result_buffer_count, sizeof(int), cudaMemcpyDeviceToHost);
        
        if (buf_count > 0) {
            YourResultType* h = (YourResultType*)malloc(sizeof(YourResultType) * buf_count);
            cudaMemcpy(h, d_results, sizeof(YourResultType) * buf_count, cudaMemcpyDeviceToHost);
            
            // Output results
            for (int i = 0; i < buf_count; i++) {
                printf("%s,%d\n", h[i].seed_str, h[i].score);
            }
            
            free(h);
        }
        
        int total_count = 0;
        cudaMemcpy(&total_count, d_result_count, sizeof(int), cudaMemcpyDeviceToHost);
        progress_update(&progress, seeds_per_batch, total_count, batch);
    }
}
```

## Benefits

1. **Smoother GPU utilization** - Sync once per chunk, not per batch
2. **Consistent code** - All filters use the same pattern
3. **Easy to maintain** - Fix bugs once, all filters benefit
4. **Optimal chunk sizes** - `calculate_batches_per_chunk()` handles it

## Files to Update

All filters should use this pattern:
- [x] `negative_joker_prefilter.cu` - DONE
- [ ] `negative_tag_skipper.cu`
- [ ] `negative_legendary_prefilter.cu`
- [ ] `negative_rare_prefilter.cu`
- [ ] `negative_uncommon_prefilter.cu`
- [ ] `ultimate_filter.cu`
- [ ] `economy_rush_search.cu`
- [ ] Others...

## Key Points

- Use `calculate_batches_per_chunk(batch_chars)` - don't hardcode!
- Launch all batches in chunk before syncing
- Sync once per chunk, not per batch
- Collect results after sync
- Always use `cudaMemcpyAsync` for resets (if possible)

/**
 * @file balatro_progress.cuh
 * @brief Lightweight progress reporting for batch searches
 */

#ifndef BALATRO_PROGRESS_CUH
#define BALATRO_PROGRESS_CUH

#include <stdio.h>
#include <stdint.h>
#include <time.h>
#include "balatro_batch.cuh"

#ifdef __cplusplus
#include <chrono>
#define USE_CHRONO
#endif

typedef struct {
    uint64_t total_seeds;
    uint64_t seeds_searched;
    uint64_t seeds_matched;
    uint64_t current_batch_index;
    uint64_t total_batches;
    int batch_chars;
#ifdef USE_CHRONO
    std::chrono::high_resolution_clock::time_point start_time;
    std::chrono::high_resolution_clock::time_point last_update;
#else
    clock_t start_time;
    clock_t last_update;
#endif
    uint64_t last_seeds_searched;
    uint64_t update_interval_ms;
} ProgressTracker;

__host__ __forceinline__ void format_seeds_abbrev(uint64_t v, char* out) {
    // Formats with M/B/T suffix; caps at T.
    const double one_m = 1000000.0;
    const double one_b = 1000000000.0;
    const double one_t = 1000000000000.0;
    if (v >= (uint64_t)one_t) {
        double val = v / one_t;
        sprintf(out, "%.2fT", val);
    } else if (v >= (uint64_t)one_b) {
        double val = v / one_b;
        sprintf(out, "%.2fB", val);
    } else if (v >= (uint64_t)one_m) {
        double val = v / one_m;
        sprintf(out, "%.2fM", val);
    } else {
        sprintf(out, "%llu", (unsigned long long)v);
    }
}

__host__ __forceinline__ uint64_t progress_next_interval(uint64_t current_ms) {
    // Sequence: 1s,2s,4s,8s,16s,30s,64s,128s,256s, cap at 1024s
    if (current_ms < 1000) return 1000;
    if (current_ms < 2000) return 2000;
    if (current_ms < 4000) return 4000;
    if (current_ms < 8000) return 8000;
    if (current_ms < 16000) return 16000;
    if (current_ms < 30000) return 30000;
    if (current_ms < 64000) return 64000;
    if (current_ms < 128000) return 128000;
    if (current_ms < 256000) return 256000;
    return 1024000; // cap at 1024s
}

__host__ __forceinline__ void progress_init(ProgressTracker* tracker, uint64_t total, uint64_t total_batches_count, int batch_chars_count) {
    tracker->total_seeds = total;
    tracker->seeds_searched = 0;
    tracker->seeds_matched = 0;
    tracker->current_batch_index = 0;
    tracker->total_batches = total_batches_count;
    tracker->batch_chars = batch_chars_count;
#ifdef USE_CHRONO
    tracker->start_time = std::chrono::high_resolution_clock::now();
    tracker->last_update = tracker->start_time;
#else
    tracker->start_time = clock();
    tracker->last_update = tracker->start_time;
#endif
    tracker->last_seeds_searched = 0;
    tracker->update_interval_ms = 1000;  // Start with 1 second
}

__host__ __forceinline__ void progress_print(ProgressTracker* tracker) {
#ifdef USE_CHRONO
    auto now = std::chrono::high_resolution_clock::now();
    auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - tracker->start_time).count();
    double elapsed_seconds = elapsed / 1000.0;
#else
    clock_t now = clock();
    double elapsed_seconds = ((double)(now - tracker->start_time) / CLOCKS_PER_SEC);
#endif

    double seeds_per_sec = (tracker->seeds_searched > 0 && elapsed_seconds > 0)
        ? tracker->seeds_searched / elapsed_seconds : 0.0;
    double filter_rate = (tracker->seeds_searched > 0)
        ? (double)tracker->seeds_matched / tracker->seeds_searched * 100.0 : 0.0;
    double percent_done = (tracker->total_seeds > 0)
        ? (double)tracker->seeds_searched / tracker->total_seeds * 100.0 : 0.0;

    char batch_suffix[9];
    if (tracker->batch_chars > 0) {
        batch_index_to_suffix_host(tracker->current_batch_index, tracker->batch_chars, batch_suffix);
    } else {
        batch_suffix[0] = '\0';
    }

    char searched_fmt[32];
    char matched_fmt[32];
    format_seeds_abbrev(tracker->seeds_searched, searched_fmt);
    format_seeds_abbrev(tracker->seeds_matched, matched_fmt);

    // Use newline instead of \r to avoid mixing with stdout
    fprintf(stderr, "$[Progress] %.2f%% done | Batch: %llu (suffix:%s) | Searched: %s | Matched: %s (%.8f%%) | Speed: %.2f M seeds/sec\n",
        percent_done,
        (unsigned long long)tracker->current_batch_index,
        batch_suffix,
        searched_fmt,
        matched_fmt,
        filter_rate,
        seeds_per_sec / 1000000.0);
    fflush(stderr);
}

__host__ __forceinline__ void progress_update(ProgressTracker* tracker, uint64_t seeds_processed, uint64_t matches, uint64_t batch_idx) {
    tracker->seeds_searched += seeds_processed;
    tracker->seeds_matched += matches;
    tracker->current_batch_index = batch_idx;

#ifdef USE_CHRONO
    auto now = std::chrono::high_resolution_clock::now();
    auto dt_ms = std::chrono::duration_cast<std::chrono::milliseconds>(now - tracker->last_update).count();
    // STRICT: Only print if enough time has passed (no batch-count triggers)
    if (dt_ms >= (int64_t)tracker->update_interval_ms) {
        progress_print(tracker);
        tracker->last_update = now;
        tracker->last_seeds_searched = tracker->seeds_searched;
        // Advance interval (exponential backoff)
        if (tracker->update_interval_ms < 1024000ULL) {
            tracker->update_interval_ms = progress_next_interval(tracker->update_interval_ms);
        }
    }
#else
    clock_t now = clock();
    double dt_ms = ((double)(now - tracker->last_update) / CLOCKS_PER_SEC) * 1000.0;
    // STRICT: Only print if enough time has passed (no batch-count triggers)
    if (dt_ms >= (int64_t)tracker->update_interval_ms) {
        progress_print(tracker);
        tracker->last_update = now;
        tracker->last_seeds_searched = tracker->seeds_searched;
        // Advance interval (exponential backoff)
        if (tracker->update_interval_ms < 1024000ULL) {
            tracker->update_interval_ms = progress_next_interval(tracker->update_interval_ms);
        }
    }
#endif
}

__host__ __forceinline__ void progress_print_final(ProgressTracker* tracker) {
#ifdef USE_CHRONO
    auto now = std::chrono::high_resolution_clock::now();
    auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(now - tracker->start_time).count();
    double elapsed_seconds = elapsed / 1000.0;
#else
    clock_t now = clock();
    double elapsed_seconds = ((double)(now - tracker->start_time) / CLOCKS_PER_SEC);
#endif

    double seeds_per_sec = (tracker->seeds_searched > 0 && elapsed_seconds > 0)
        ? tracker->seeds_searched / elapsed_seconds : 0.0;
    double filter_rate = (tracker->seeds_searched > 0)
        ? (double)tracker->seeds_matched / tracker->seeds_searched * 100.0 : 0.0;

    fprintf(stderr, "\n$=== Final Results ===\n");
    fprintf(stderr, "$Total seeds searched: %llu\n", (unsigned long long)tracker->seeds_searched);
    fprintf(stderr, "$Matches found: %llu\n", (unsigned long long)tracker->seeds_matched);
    fprintf(stderr, "$Filter rate: %.4f%%\n", filter_rate);
    fprintf(stderr, "$Time: %.2f seconds\n", elapsed_seconds);
    fprintf(stderr, "$Average speed: %.2f M seeds/sec\n", seeds_per_sec / 1000000.0);
}

#endif // BALATRO_PROGRESS_CUH



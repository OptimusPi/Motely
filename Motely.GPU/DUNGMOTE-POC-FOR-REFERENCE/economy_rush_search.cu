/**
 * @file economy_rush_search.cu
 * @brief Find seeds with strong early economy across multiple antes
 *
 * This filter searches for seeds with multiple economy-boosting elements
 * appearing in early antes (1-4), enabling powerful early-game builds.
 *
 * Economy elements checked:
 * - Investment tag (small blind skip)
 * - Voucher tag (small blind skip)
 * - Credit Card joker (common)
 * - Business Card joker (common)
 * - Golden Ticket joker (common)
 * - Golden Joker (common, gives $3 per hand)
 * - Lucky Cat joker (uncommon, +$2 per discard)
 * - Trading Card joker (uncommon, +$4 per pack opened)
 * - Certificate joker (uncommon, +$3 per joker sold)
 *
 * Output: CSV format with seed and economy score
 * Format: SEED,ECONOMY_SCORE
 *
 * Notes:
 * - Compiled with `--fmad=false` for Lua-precision compatibility (required for accurate RNG).
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "gpu_common.h"
#include <chrono>

#include "balatro_enums.cuh"
#include "balatro_rng.cuh"
#include "balatro_streams.cuh"
#include "balatro_batch.cuh"
#include "balatro_progress.cuh"
#include "balatro_args.cuh"

// Economy joker IDs
#define J_CREDIT_CARD_ID    ((int)RARITY_COMMON | J_CREDIT_CARD)
#define J_BUSINESS_CARD_ID  ((int)RARITY_COMMON | J_BUSINESS_CARD)
#define J_GOLDEN_TICKET_ID  ((int)RARITY_COMMON | J_GOLDEN_TICKET)
#define J_GOLDEN_JOKER_ID  ((int)RARITY_COMMON | J_GOLDEN_JOKER)
#define J_LUCKY_CAT_ID      ((int)RARITY_UNCOMMON | J_LUCKY_CAT)
#define J_TRADING_CARD_ID   ((int)RARITY_UNCOMMON | J_TRADING_CARD)
#define J_CERTIFICATE_ID    ((int)RARITY_UNCOMMON | J_CERTIFICATE)

// Economy scoring weights
#define TAG_INVESTMENT_SCORE  5
#define TAG_VOUCHER_SCORE     3
#define JOKER_CREDIT_CARD_SCORE   4
#define JOKER_BUSINESS_CARD_SCORE 3
#define JOKER_GOLDEN_TICKET_SCORE 3
#define JOKER_GOLDEN_JOKER_SCORE  2
#define JOKER_LUCKY_CAT_SCORE     2
#define JOKER_TRADING_CARD_SCORE  2
#define JOKER_CERTIFICATE_SCORE   2

struct EconomyRushConfig {
    int max_ante;           // Check up to this ante (default: 4)
    int min_score;          // Minimum economy score to match (default: 8)
    uint32_t allowed_antes; // Bitmask of antes to check (default: 0xF = antes 1-4)
};

__device__ __forceinline__ int check_economy_joker(int joker_id) {
    if (joker_id == J_CREDIT_CARD_ID) return JOKER_CREDIT_CARD_SCORE;
    if (joker_id == J_BUSINESS_CARD_ID) return JOKER_BUSINESS_CARD_SCORE;
    if (joker_id == J_GOLDEN_TICKET_ID) return JOKER_GOLDEN_TICKET_SCORE;
    if (joker_id == J_GOLDEN_JOKER_ID) return JOKER_GOLDEN_JOKER_SCORE;
    if (joker_id == J_LUCKY_CAT_ID) return JOKER_LUCKY_CAT_SCORE;
    if (joker_id == J_TRADING_CARD_ID) return JOKER_TRADING_CARD_SCORE;
    if (joker_id == J_CERTIFICATE_ID) return JOKER_CERTIFICATE_SCORE;
    return 0;
}

__device__ __forceinline__ int evaluate_economy_rush(
    const char* seed_str,
    double seed_hash,
    const EconomyRushConfig* config
) {
    int total_score = 0;
    
    for (int ante = 1; ante <= config->max_ante; ante++) {
        if (!(config->allowed_antes & (1 << (ante - 1)))) continue;
        
        // Check small blind tag for Investment or Voucher
        Tag small_tag = get_tag_for_ante(seed_str, 8, seed_hash, ante, "1", 1);
        if (small_tag == TAG_INVESTMENT) {
            total_score += TAG_INVESTMENT_SCORE;
        } else if (small_tag == TAG_VOUCHER) {
            total_score += TAG_VOUCHER_SCORE;
        }
        
        // Check shop jokers for economy jokers
        JokerStream js = create_shop_joker_stream(seed_str, 8, seed_hash, ante, STAKE_WHITE);
        
        // Check first 5 shop slots (reasonable early game)
        for (int slot = 0; slot < 5; slot++) {
            Item joker = get_next_joker(&js);
            int joker_index = joker.type_value & JOKER_INDEX_MASK;
            int joker_rarity = get_joker_rarity(joker.type_value);
            int joker_full_id = ((int)joker_rarity | joker_index);
            
            int score = check_economy_joker(joker_full_id);
            total_score += score;
        }
    }
    
    return total_score;
}

GPU_KERNEL void economy_rush_kernel(
    uint64_t start_batch_index,
    uint64_t end_batch_index,
    int batch_chars,
    const EconomyRushConfig* config
) {
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;
    
    uint64_t batches = (end_batch_index >= start_batch_index) ? (end_batch_index - start_batch_index + 1) : 0;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t total_seeds = batches * seeds_per_batch;
    
    char seed_str[9];
    
    for (uint64_t global_seed_offset = tid; global_seed_offset < total_seeds; global_seed_offset += stride) {
        uint64_t batch_offset = global_seed_offset / seeds_per_batch;
        uint64_t local_idx = global_seed_offset % seeds_per_batch;
        uint64_t batch_index = start_batch_index + batch_offset;
        
        uint64_t seed_idx = local_index_to_seed_index(batch_index, local_idx, batch_chars);
        seed_index_to_string(seed_idx, seed_str);
        double seed_hash = pseudohash8(seed_str);
        
        int economy_score = evaluate_economy_rush(seed_str, seed_hash, config);
        
        if (economy_score >= config->min_score) {
            printf("%s,%d\n", seed_str, economy_score);
        }
    }
}

static void usage(const char* exe) {
    printf("Usage: %s --start-batch N [--end-batch M] [--batch-chars N] [--max-ante N] [--min-score N] [--antes LIST] [--block-size N] [--blocks-per-sm N]\n", exe);
    printf("\n");
    printf("Economy Rush Search - Find seeds with strong early economy\n");
    printf("\n");
    printf("Options:\n");
    printf("  --start-batch N     Start batch index (default: 0)\n");
    printf("  --end-batch M       End batch index (default: -1 = all)\n");
    printf("  --batch-chars N     Batch size in characters (default: 4)\n");
    printf("  --max-ante N        Check up to ante N (default: 4)\n");
    printf("  --min-score N       Minimum economy score to match (default: 12)\n");
    printf("  --antes LIST        Comma-separated antes to check (default: 1,2,3,4)\n");
    printf("  --block-size N      Threads per block (default: 256)\n");
    printf("  --blocks-per-sm N   Blocks per SM (default: 32)\n");
    printf("\n");
    printf("Economy Scoring:\n");
    printf("  Investment tag:     +5 points\n");
    printf("  Voucher tag:        +3 points\n");
    printf("  Credit Card joker:  +4 points\n");
    printf("  Business Card:      +3 points\n");
    printf("  Golden Ticket:      +3 points\n");
    printf("  Golden Joker:       +2 points\n");
    printf("  Lucky Cat:          +2 points\n");
    printf("  Trading Card:       +2 points\n");
    printf("  Certificate:        +2 points\n");
    printf("\n");
    printf("Example:\n");
    printf("  %s --start-batch 0 --end-batch 10000 --batch-chars 4 --min-score 10 --max-ante 3\n", exe);
}

int main(int argc, char** argv) {
    if (argc < 2) {
        usage(argv[0]);
        return 1;
    }
    
    // Parse GPU flags
    int block_size, blocks_per_sm;
    parse_gpu_flags(argc, argv, &block_size, &blocks_per_sm);
    
    // Batch configuration
    int batch_chars = 4;
    int64_t start_batch_i64 = 0, end_batch_i64 = -1;
    parse_batch_flags(argc, argv, &batch_chars, &start_batch_i64, &end_batch_i64);
    
    if (start_batch_i64 < 0) start_batch_i64 = 0;
    uint64_t start_batch = (uint64_t)start_batch_i64;
    
    uint64_t total_batches = calculate_total_batches(batch_chars);
    uint64_t end_batch = (end_batch_i64 >= 0) ? (uint64_t)end_batch_i64 : (total_batches - 1);
    if (end_batch >= total_batches) end_batch = total_batches - 1;
    if (end_batch < start_batch) {
        fprintf(stderr, "Error: end_batch < start_batch\n");
        return 1;
    }
    
    // Economy config
    EconomyRushConfig config;
    config.max_ante = 4;
    config.min_score = 12;  // Higher default to reduce noise
    config.allowed_antes = 0xF; // Antes 1-4 by default
    
    const char* max_ante_arg = get_flag_value(argc, argv, "--max-ante");
    if (max_ante_arg) config.max_ante = atoi(max_ante_arg);
    if (config.max_ante < 1 || config.max_ante > 8) config.max_ante = 4;
    
    const char* min_score_arg = get_flag_value(argc, argv, "--min-score");
    if (min_score_arg) config.min_score = atoi(min_score_arg);
    if (config.min_score < 1) config.min_score = 1;
    
    const char* antes_arg = get_flag_value(argc, argv, "--antes");
    if (antes_arg) {
        config.allowed_antes = 0;
        char buf[256];
        strncpy(buf, antes_arg, sizeof(buf) - 1);
        buf[sizeof(buf) - 1] = '\0';
        char* tok = strtok(buf, ",");
        while (tok) {
            int a = atoi(tok);
            if (a >= 1 && a <= 8) {
                config.allowed_antes |= (1 << (a - 1));
            }
            tok = strtok(nullptr, ",");
        }
        if (config.allowed_antes == 0) config.allowed_antes = 0xF;
    }
    
    // GPU setup
    int device;
    GPU_GET_DEVICE(&device);
    GPUDeviceProp prop;
    GPU_GET_DEVICE_PROPERTIES(&prop, device);
    int num_blocks = prop.multiProcessorCount * blocks_per_sm;
    
    EconomyRushConfig* d_config = nullptr;
    GPU_MALLOC((void**)&d_config, sizeof(EconomyRushConfig));
    GPU_MEMCPY(d_config, &config, sizeof(EconomyRushConfig), GPU_MEMCPY_HOST_TO_DEVICE);
    
    uint64_t num_batches = end_batch - start_batch + 1;
    uint64_t seeds_per_batch = calculate_seeds_per_batch(batch_chars);
    uint64_t total_seeds = num_batches * seeds_per_batch;
    
    fprintf(stderr, "GPU: %s\n", prop.name);
    fprintf(stderr, "Config: %d blocks x %d threads\n", num_blocks, block_size);
    fprintf(stderr, "Batch: chars=%d, start=%llu, end=%llu (batches=%llu, seeds=%llu)\n",
        batch_chars,
        (unsigned long long)start_batch,
        (unsigned long long)end_batch,
        (unsigned long long)num_batches,
        (unsigned long long)total_seeds);
    fprintf(stderr, "Economy Rush: max_ante=%d, min_score=%d (auto-filtered), antes=0x%X\n\n",
        config.max_ante, config.min_score, config.allowed_antes);
    
    ProgressTracker progress;
    progress_init(&progress, total_seeds, num_batches, batch_chars);
    
    uint64_t BATCHES_PER_CHUNK = 100ULL;
    if (batch_chars == 1) BATCHES_PER_CHUNK = 10000ULL;
    else if (batch_chars == 2) BATCHES_PER_CHUNK = 1000ULL;
    else if (batch_chars == 3) BATCHES_PER_CHUNK = 100ULL;
    
    auto t0 = std::chrono::high_resolution_clock::now();
    
    for (uint64_t chunk_start = start_batch; chunk_start <= end_batch; chunk_start += BATCHES_PER_CHUNK) {
        uint64_t chunk_end = (chunk_start + BATCHES_PER_CHUNK - 1 < end_batch) ? (chunk_start + BATCHES_PER_CHUNK - 1) : end_batch;
        
        economy_rush_kernel<<<num_blocks, block_size>>>(
            chunk_start, chunk_end, batch_chars, d_config
        );
        
        GPUError err = GPU_GET_LAST_ERROR();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU launch error: %d\n", (int)err);
            break;
        }
        err = GPU_DEVICE_SYNCHRONIZE();
        if (err != GPU_SUCCESS) {
            fprintf(stderr, "\nGPU sync error: %d\n", (int)err);
            break;
        }
        
        uint64_t seeds_done = (chunk_end - chunk_start + 1) * seeds_per_batch;
        progress_update(&progress, seeds_done, 0, chunk_end);
    }
    
    progress_print_final(&progress);
    
    GPU_FREE(d_config);
    return 0;
}

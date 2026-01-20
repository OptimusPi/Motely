/**
 * @file balatro_rng.cuh
 * @brief CUDA RNG implementation - Verified accurate version
 *
 * This implements the exact LuaJIT Tausworthe PRNG used by Balatro.
 * Verified against Balatro game output - includes precision rounding that v1 was missing.
 *
 * Key features:
 * 1. Uses static LuaRandom.RandInt(seed) - no instantiation needed
 * 2. 11 warmup iterations per state (5x2 + 1)
 * 3. Precision handling via round-to-13-decimals (matches Balatro's string.format("%.13f"))
 * 4. PrngStream support for sequential random calls
 */

#ifndef BALATRO_RNG_CUH
#define BALATRO_RNG_CUH

#include "gpu_common.h"
#include <stdint.h>
#include <math.h>
#include <string.h>

// ============================================================================
// Constants
// ============================================================================
#define M_PI_CUDA    3.14159265358979323846
#define M_E_CUDA     2.7182818284590452354

// PRNG iteration constants
#define PRNG_MULTIPLIER   1.72431234
#define PRNG_ADDEND       2.134453429141
#define PRNG_PRECISION    10000000000000.0  // 10^13

// ============================================================================
// Seed Character Set (Base-35)
// ============================================================================
// Balatro seeds use 1-9 and A-Z, skipping 0. Total 35 characters.
__constant__ char SEED_CHARS[36] = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

// ============================================================================
// Core: Static LuaRandom.RandInt(seed)
// ============================================================================

/**
 * @brief Static LuaRandom.RandInt(seed) implementation
 *
 * This does all 4 state initializations AND 11 warmup iterations per state,
 * returning the first random 64-bit integer.
 */
GPU_DEVICE __forceinline__ uint64_t lua_randint_static(double seed) {
    double d = seed;
    uint64_t randint = 0;

    // state[0]: mask = 1 << 1 = 2, shift params (31, 45, 1, 18)
    {
        d = d * M_PI_CUDA + M_E_CUDA;
        uint64_t state;
        memcpy(&state, &d, sizeof(double));
        if (state < 2ULL) state += 2ULL;

        for (int i = 0; i < 11; i++) {
            state = (((state << 31) ^ state) >> 45) ^ ((state & (0xFFFFFFFFFFFFFFFFULL << 1)) << 18);
        }
        randint ^= state;
    }

    // state[1]: mask = 1 << 6 = 64, shift params (19, 30, 6, 28)
    {
        d = d * M_PI_CUDA + M_E_CUDA;
        uint64_t state;
        memcpy(&state, &d, sizeof(double));
        if (state < 64ULL) state += 64ULL;

        for (int i = 0; i < 11; i++) {
            state = (((state << 19) ^ state) >> 30) ^ ((state & (0xFFFFFFFFFFFFFFFFULL << 6)) << 28);
        }
        randint ^= state;
    }

    // state[2]: mask = 1 << 9 = 512, shift params (24, 48, 9, 7)
    {
        d = d * M_PI_CUDA + M_E_CUDA;
        uint64_t state;
        memcpy(&state, &d, sizeof(double));
        if (state < 512ULL) state += 512ULL;

        for (int i = 0; i < 11; i++) {
            state = (((state << 24) ^ state) >> 48) ^ ((state & (0xFFFFFFFFFFFFFFFFULL << 9)) << 7);
        }
        randint ^= state;
    }

    // state[3]: mask = 1 << 17 = 131072, shift params (21, 39, 17, 8)
    {
        d = d * M_PI_CUDA + M_E_CUDA;
        uint64_t state;
        memcpy(&state, &d, sizeof(double));
        if (state < 131072ULL) state += 131072ULL;

        for (int i = 0; i < 11; i++) {
            state = (((state << 21) ^ state) >> 39) ^ ((state & (0xFFFFFFFFFFFFFFFFULL << 17)) << 8);
        }
        randint ^= state;
    }

    return randint;
}

/**
 * @brief Static LuaRandom.RandDblMem(seed) - IEEE 754 double bits
 */
GPU_DEVICE __forceinline__ uint64_t lua_randdblmem_static(double seed) {
    // Mantissa mask + exponent for [1.0, 2.0)
    return (lua_randint_static(seed) & 0x000FFFFFFFFFFFFFULL) | 0x3FF0000000000000ULL;
}

/**
 * @brief Static LuaRandom.Random(seed) - returns double in [0, 1)
 */
GPU_DEVICE __forceinline__ double lua_random_static(double seed) {
    uint64_t bits = lua_randdblmem_static(seed);
    double result;
    memcpy(&result, &bits, sizeof(double));
    return result - 1.0;
}

/**
 * @brief Static LuaRandom.RandInt(seed, min, max) - returns int in [min, max)
 */
GPU_DEVICE __forceinline__ int lua_randint_range_static(double seed, int min, int max) {
    return (int)(lua_random_static(seed) * (double)(max - min)) + min;
}

// ============================================================================
// Pseudohash: Balatro's exact algorithm
// ============================================================================

/**
 * @brief Pseudohash a string (RIGHT to LEFT iteration)
 */
GPU_DEVICE double pseudohash(const char* str, int len) {
    double num = 1.0;

    for (int i = len - 1; i >= 0; i--) {
        double term1 = (1.1239285023 / num) * (double)(unsigned char)str[i] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)(i + 1);
        num = fmod(term1 + term2, 1.0);
        if (num < 0) num += 1.0;  // Ensure positive
    }

    return num;
}

/**
 * @brief Pseudohash for 8-char seeds
 */
GPU_DEVICE __forceinline__ double pseudohash8(const char* seed8) {
    return pseudohash(seed8, 8);
}

/**
 * @brief Pseudohash for 8-char seeds (v2 alias)
 */
GPU_DEVICE __forceinline__ double pseudohash8_v2(const char* seed8) {
    return pseudohash(seed8, 8);
}

// ============================================================================
// Suffix Caching (Motely-compatible batch optimization)
// ============================================================================

/**
 * @brief Compute pseudohash of a suffix string (for caching)
 * 
 * Used to cache the hash of the rightmost characters that are shared
 * across all seeds in a batch.
 * 
 * IMPORTANT: The suffix is the rightmost chars of the full seed.
 * When hashing the suffix, term2 must use the position in the FULL seed,
 * not the position in the suffix string.
 * 
 * @param suffix Suffix string (rightmost chars)
 * @param suffix_len Length of suffix
 * @param full_seed_len Total length of full seed (usually 8)
 * @return Pseudohash of the suffix
 */
GPU_DEVICE __forceinline__ double pseudohash_prefix(const char* suffix, int suffix_len, int full_seed_len) {
    double num = 1.0;
    
    // Process suffix RIGHT-TO-LEFT
    // suffix[i] at position i in suffix (0..suffix_len-1) corresponds to
    // full seed position (full_seed_len - suffix_len + i)
    for (int i = suffix_len - 1; i >= 0; i--) {
        int full_pos = full_seed_len - suffix_len + i;  // Position in full seed
        double term1 = (1.1239285023 / num) * (double)(unsigned char)suffix[i] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)(full_pos + 1);  // Use position in FULL seed
        num = fmod(term1 + term2, 1.0);
        if (num < 0) num += 1.0;
    }
    
    return num;
}

/**
 * @brief Compute pseudohash of a seed using cached batch suffix (Motely-compatible)
 * 
 * Motely batch processing: batch index encodes RIGHTMOST (8 - batch_chars) characters (the suffix).
 * All seeds in a batch share these rightmost characters (the suffix).
 * The leftmost batch_chars characters vary within the batch.
 * 
 * This function uses the cached suffix hash and incrementally computes the leftmost chars.
 * 
 * IMPORTANT: The pseudohash algorithm processes RIGHT-TO-LEFT (position 7 → 0).
 * We cache the hash of the rightmost chars (positions 7 down to batch_chars).
 * Then we incrementally add the leftmost chars (positions batch_chars-1 down to 0).
 * 
 * @param cached_suffix_hash Cached pseudohash of the batch suffix (rightmost chars, positions 7..batch_chars)
 * @param suffix_chars Number of characters in the cached suffix (8 - batch_chars)
 * @param seed_full Full 8-character seed string
 * @return Pseudohash of the seed
 */
GPU_DEVICE __forceinline__ double pseudohash8_with_batch_prefix(
    double cached_suffix_hash,
    int suffix_chars,
    const char* seed_full
) {
    // Motely-compatible caching:
    // - We cached the hash of the rightmost suffix_chars characters (positions 7 down to 8-suffix_chars)
    // - Now we need to incrementally add the leftmost (8 - suffix_chars) characters (positions suffix_chars-1 down to 0)
    // 
    // The pseudohash algorithm processes RIGHT-TO-LEFT (position 7 → 0):
    // - Cached: positions 7, 6, 5, 4 (if suffix_chars=4) - already processed
    // - Need to add: positions 3, 2, 1, 0 (leftmost chars) - process in order 3→2→1→0
    //
    // We process the leftmost chars from rightmost to leftmost (matching pseudohash order)
    int leftmost_chars = 8 - suffix_chars;
    double num = cached_suffix_hash;
    
    // Process leftmost chars from rightmost to leftmost
    // The pseudohash processes RIGHT-TO-LEFT: 7→6→5→4→3→2→1→0
    // We've cached the hash after processing the SUFFIX (rightmost suffix_chars chars)
    // The suffix was processed as a standalone string: positions in suffix 0..(suffix_chars-1) with term2 = (i+1)
    // Now we continue with LEFTMOST chars (positions 0 to leftmost_chars-1 in full seed)
    // We process them RIGHT-TO-LEFT: (leftmost_chars-1) → ... → 0
    // The term2 must use the position in the FULL seed: (char_pos + 1)
    for (int i = leftmost_chars - 1; i >= 0; i--) {
        int char_pos = i;  // Position in full seed (leftmost chars are at positions 0 to leftmost_chars-1)
        int char_index = char_pos + 1;    // Position in full seed (1-indexed for term2)
        double term1 = (1.1239285023 / num) * (double)(unsigned char)seed_full[char_pos] * M_PI_CUDA;
        double term2 = M_PI_CUDA * (double)char_index;
        num = fmod(term1 + term2, 1.0);
        if (num < 0) num += 1.0;
    }
    
    return num;
}

// ============================================================================
// PRNG Stream State Iteration
// ============================================================================

/**
 * @brief Iterate PRNG stream state
 *
 * Algorithm:
 *   state = (state * 1.72431234 + 2.134453429141) % 1
 *   state = rint(state * 10^13) / 10^13
 */
GPU_DEVICE __forceinline__ double iterate_prng_state(double state) {
    state = state * PRNG_MULTIPLIER + PRNG_ADDEND;
    state = fmod(state, 1.0);
    if (state < 0) state += 1.0;

    // Precision adjustment matches Balatro's string.format("%.13f")
    state = rint(state * PRNG_PRECISION) / PRNG_PRECISION;

    return state;
}

// ============================================================================
// PRNG Stream: For sequential random calls
// ============================================================================

struct PrngStream {
    double state;       // Current pseudohash state
    double seed_hash;   // Cached pseudohash(seed)
};

/**
 * @brief Create a PRNG stream for a key+seed combination
 */
GPU_DEVICE PrngStream create_prng_stream(
    const char* key, int key_len,
    const char* seed, int seed_len,
    double seed_hash
) {
    char combined[64];
    int combined_len = 0;

    for (int i = 0; i < key_len && combined_len < 63; i++) {
        combined[combined_len++] = key[i];
    }
    for (int i = 0; i < seed_len && combined_len < 63; i++) {
        combined[combined_len++] = seed[i];
    }
    combined[combined_len] = '\0';

    PrngStream stream;
    stream.state = pseudohash(combined, combined_len);
    stream.seed_hash = seed_hash;
    return stream;
}

/**
 * @brief Get next pseudoseed from stream
 */
GPU_DEVICE __forceinline__ double get_next_pseudoseed(PrngStream* stream) {
    stream->state = iterate_prng_state(stream->state);
    return (stream->state + stream->seed_hash) / 2.0;
}

/**
 * @brief Get next random double [0, 1) from stream
 */
GPU_DEVICE __forceinline__ double get_next_random(PrngStream* stream) {
    double ps = get_next_pseudoseed(stream);
    return lua_random_static(ps);
}

/**
 * @brief Build edition key for shop jokers: "edisho" + ante
 * @return Length of the key string
 */
GPU_DEVICE __forceinline__ int build_shop_edition_key(char* key_buf, int ante) {
    key_buf[0] = 'e'; key_buf[1] = 'd'; key_buf[2] = 'i';
    key_buf[3] = 's'; key_buf[4] = 'h'; key_buf[5] = 'o';
    if (ante < 10) {
        key_buf[6] = '0' + ante;
        key_buf[7] = '\0';
        return 7;
    } else {
        key_buf[6] = '0' + (ante / 10);
        key_buf[7] = '0' + (ante % 10);
        key_buf[8] = '\0';
        return 8;
    }
}

/**
 * @brief Build edition key for soul jokers: "edisou" + ante
 * @return Length of the key string
 */
GPU_DEVICE __forceinline__ int build_soul_edition_key(char* key_buf, int ante) {
    key_buf[0] = 'e'; key_buf[1] = 'd'; key_buf[2] = 'i';
    key_buf[3] = 's'; key_buf[4] = 'o'; key_buf[5] = 'u';
    if (ante < 10) {
        key_buf[6] = '0' + ante;
        key_buf[7] = '\0';
        return 7;
    } else {
        key_buf[6] = '0' + (ante / 10);
        key_buf[7] = '0' + (ante % 10);
        key_buf[8] = '\0';
        return 8;
    }
}

/**
 * @brief Get next random int in [min, max) from stream
 */
GPU_DEVICE __forceinline__ int get_next_random_int(PrngStream* stream, int min, int max) {
    double ps = get_next_pseudoseed(stream);
    return lua_randint_range_static(ps, min, max);
}

// ============================================================================
// One-shot Pseudorandom: For single random calls
// ============================================================================

/**
 * @brief Compute pseudoseed for predict_seed path
 */
GPU_DEVICE double compute_pseudoseed(
    const char* key, int key_len,
    const char* seed, int seed_len,
    double seed_hash
) {
    char combined[64];
    int combined_len = 0;

    for (int i = 0; i < key_len && combined_len < 63; i++) {
        combined[combined_len++] = key[i];
    }
    for (int i = 0; i < seed_len && combined_len < 63; i++) {
        combined[combined_len++] = seed[i];
    }
    combined[combined_len] = '\0';

    double pseed = pseudohash(combined, combined_len);
    pseed = iterate_prng_state(pseed);
    pseed = fabs(pseed);

    return (pseed + seed_hash) / 2.0;
}

/**
 * @brief One-shot pseudorandom call (returns double [0, 1))
 */
GPU_DEVICE double pseudorandom(
    const char* key, int key_len,
    const char* seed, int seed_len,
    double seed_hash
) {
    double pseed = compute_pseudoseed(key, key_len, seed, seed_len, seed_hash);
    return lua_random_static(pseed);
}

/**
 * @brief One-shot pseudorandom call with range [min, max)
 */
GPU_DEVICE uint64_t pseudorandom_range(
    const char* key, int key_len,
    const char* seed, int seed_len,
    double seed_hash,
    uint64_t min, uint64_t max
) {
    double pseed = compute_pseudoseed(key, key_len, seed, seed_len, seed_hash);
    return (uint64_t)(lua_random_static(pseed) * (double)(max - min)) + min;
}

// ============================================================================
// Seed Encoding/Decoding
// ============================================================================

/**
 * @brief Convert seed index to 8-char string (base-35)
 */
GPU_DEVICE void seed_index_to_string(uint64_t index, char* out) {
    for (int i = 7; i >= 0; i--) {
        out[i] = SEED_CHARS[index % 35];
        index /= 35;
    }
    out[8] = '\0';
}

/**
 * @brief Convert 8-char seed string to index (device version)
 */
GPU_DEVICE uint64_t seed_string_to_index(const char* seed) {
    uint64_t index = 0;
    for (int i = 0; i < 8; i++) {
        char c = seed[i];
        int val;
        if (c >= '1' && c <= '9') {
            val = c - '1';
        } else if (c >= 'A' && c <= 'Z') {
            val = 9 + (c - 'A');
        } else {
            val = 0;
        }
        index = index * 35 + val;
    }
    return index;
}

/**
 * @brief Convert 8-char seed string to index (host version)
 */
GPU_HOST uint64_t seed_string_to_index_host(const char* seed) {
    uint64_t index = 0;
    const char* chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    for (int i = 0; seed[i] && i < 8; i++) {
        char c = seed[i];
        int digit = -1;
        for (int j = 0; j < 35; j++) {
            if (chars[j] == c) {
                digit = j;
                break;
            }
        }
        if (digit >= 0) {
            index = index * 35 + digit;
        }
    }
    return index;
}

#endif // BALATRO_RNG_CUH

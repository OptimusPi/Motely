/**
 * @file erratic_search.cu
 * @brief CUDA Erratic Deck Seed Searcher
 *
 * Verified accurate against Balatro game for seeds: 11262R8Z, ALEEB, F1SH6, G77, FRAX1U29, 111111F1
 *
 * IMPORTANT: Must compile with --fmad=false to match Lua floating-point precision!
 *   nvcc --fmad=false -o erratic_search.exe erratic_search.cu
 */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <ctype.h>
#include "gpu_common.h"
#include "balatro_batch.cuh"
#include <chrono>

#ifdef _WIN32
#include <direct.h>
#include <signal.h>
#include <windows.h>
#else
#include <sys/stat.h>
#include <signal.h>
#endif

static volatile uint64_t g_current_batch_index = 0;
static volatile int g_current_seed_len = 0;
static volatile int g_current_batch_chars = 4;
static volatile int g_current_target_rank = -1;
static volatile int g_current_rank_threshold = 12;
static volatile bool g_interrupted = false;
static const char* g_current_output_file = NULL;

void print_resume_command(int sig) {
    (void)sig;  // Unused
    g_interrupted = true;
    const unsigned long long resume_batch_1based = (unsigned long long)g_current_batch_index + 1ULL;
    printf("\n\n=== INTERRUPTED (Ctrl+C) ===\n");
    printf("Current batch: %llu (seed_len=%d, batch_chars=%d)\n",
           resume_batch_1based, g_current_seed_len, g_current_batch_chars);
    printf("\nCOPY THIS COMMAND TO RESUME:\n");
    if (g_current_target_rank >= 0) {
        const char* rank_names[13] = {"2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q", "K", "A"};
        printf(".\\erratic_search.exe --full-scan --rank %s --rank-threshold %d --start-batch %llu --batch-chars %d",
               rank_names[g_current_target_rank], g_current_rank_threshold,
               resume_batch_1based, g_current_batch_chars);
    } else {
        printf(".\\erratic_search.exe --full-scan --rank-threshold %d --start-batch %llu --batch-chars %d",
               g_current_rank_threshold, resume_batch_1based, g_current_batch_chars);
    }
    if (g_current_output_file && g_current_output_file[0]) {
        printf(" --output-file %s", g_current_output_file);
    }
    printf("\n");
    printf("\n");
    exit(0);
}

#define M_PI_CUDA 3.14159265358979323846
#define M_E_CUDA  2.7182818284590452354
#define BLOCK_SIZE 256
#define BATCH_BUFFER_SIZE 1000000

struct ErraticResult {
    char seed_str[9];
    int count;
};


// ==================== LuaJIT RNG ====================

GPU_DEVICE __forceinline__ uint64_t lua_static_randint(double seed) {
    double d = seed;
    uint32_t r = 0x11090601;
    uint64_t randint = 0;

    #pragma unroll
    for (int state_idx = 0; state_idx < 4; state_idx++) {
        uint64_t m = 1ULL << (r & 255);
        r >>= 8;
        d = d * M_PI_CUDA + M_E_CUDA;
        union { double dd; uint64_t uu; } conv; conv.dd = d;
        uint64_t state = conv.uu;
        if (state < m) state += m;

        int shifts[4][3] = {{31, 45, 1}, {19, 30, 6}, {24, 48, 9}, {21, 39, 17}};
        int lmasks[4] = {18, 28, 7, 8};

        for (int i = 0; i < 11; i++) {
            state = (((state << shifts[state_idx][0]) ^ state) >> shifts[state_idx][1]) ^
                    ((state & (0xFFFFFFFFFFFFFFFFULL << shifts[state_idx][2])) << lmasks[state_idx]);
        }
        randint ^= state;
    }
    return randint;
}

GPU_DEVICE __forceinline__ double lua_static_random(double seed) {
    uint64_t u = (lua_static_randint(seed) & 4503599627370495ULL) | 4607182418800017408ULL;
    union { uint64_t uu; double dd; } conv; conv.uu = u;
    return conv.dd - 1.0;
}

GPU_DEVICE __forceinline__ int lua_static_randint_range(double seed, int min, int max) {
    return (int)(lua_static_random(seed) * (max - min)) + min;
}

// ==================== Balatro RNG ====================

GPU_DEVICE __forceinline__ double lua_mod(double a, double b) {
    return a - floor(a / b) * b;
}

GPU_DEVICE __forceinline__ double roundTo13(double f) {
    const double power = 10000000000000.0;
    return rint(f * power) / power;
}


struct ErraticRNG { double rngState; double hashedSeed; };

GPU_DEVICE void init_erratic_rng(ErraticRNG* e, const char* seed, int len) {
    e->hashedSeed = pseudohash(seed, len);
    char combined[24]; int clen = 0;
    const char* key = "erratic";
    for (int i = 0; key[i]; i++) combined[clen++] = key[i];
    for (int i = 0; i < len; i++) combined[clen++] = seed[i];
    e->rngState = pseudohash(combined, clen);
}

GPU_DEVICE double next_erratic_pseudoseed(ErraticRNG* e) {
    e->rngState = fabs(roundTo13(lua_mod(2.134453429141 + e->rngState * 1.72431234, 1.0)));
    return (e->rngState + e->hashedSeed) / 2.0;
}

GPU_DEVICE __forceinline__ int internal_rank_to_standard(int r) {
    if (r == 8) return 12; if (r == 9) return 9; if (r == 10) return 11;
    if (r == 11) return 10; if (r == 12) return 8; return r;
}

struct DeckStats { uint8_t rank_counts[13]; uint8_t suit_counts[4]; };

GPU_DEVICE void generate_erratic_deck_stats(const char* seed, int len, DeckStats* s) {
    for (int i = 0; i < 13; i++) s->rank_counts[i] = 0;
    for (int i = 0; i < 4; i++) s->suit_counts[i] = 0;
    ErraticRNG e; init_erratic_rng(&e, seed, len);
    
    // Vectorized: process 8 cards at a time (6 full iterations + 4 remaining)
    // Process 6 batches of 8 cards = 48 cards
    #pragma unroll
    for (int batch = 0; batch < 6; batch++) {
        // Generate 8 pseudoseeds and indices in parallel
        double ps0 = next_erratic_pseudoseed(&e);
        double ps1 = next_erratic_pseudoseed(&e);
        double ps2 = next_erratic_pseudoseed(&e);
        double ps3 = next_erratic_pseudoseed(&e);
        double ps4 = next_erratic_pseudoseed(&e);
        double ps5 = next_erratic_pseudoseed(&e);
        double ps6 = next_erratic_pseudoseed(&e);
        double ps7 = next_erratic_pseudoseed(&e);
        
        int idx0 = lua_static_randint_range(ps0, 0, 52);
        int idx1 = lua_static_randint_range(ps1, 0, 52);
        int idx2 = lua_static_randint_range(ps2, 0, 52);
        int idx3 = lua_static_randint_range(ps3, 0, 52);
        int idx4 = lua_static_randint_range(ps4, 0, 52);
        int idx5 = lua_static_randint_range(ps5, 0, 52);
        int idx6 = lua_static_randint_range(ps6, 0, 52);
        int idx7 = lua_static_randint_range(ps7, 0, 52);
        
        // Process all 8 cards
        s->rank_counts[internal_rank_to_standard(idx0 % 13)]++;
        s->suit_counts[idx0 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx1 % 13)]++;
        s->suit_counts[idx1 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx2 % 13)]++;
        s->suit_counts[idx2 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx3 % 13)]++;
        s->suit_counts[idx3 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx4 % 13)]++;
        s->suit_counts[idx4 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx5 % 13)]++;
        s->suit_counts[idx5 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx6 % 13)]++;
        s->suit_counts[idx6 / 13]++;
        s->rank_counts[internal_rank_to_standard(idx7 % 13)]++;
        s->suit_counts[idx7 / 13]++;
    }
    
    // Process remaining 4 cards (52 - 48 = 4)
    double ps0 = next_erratic_pseudoseed(&e);
    double ps1 = next_erratic_pseudoseed(&e);
    double ps2 = next_erratic_pseudoseed(&e);
    double ps3 = next_erratic_pseudoseed(&e);
    
    int idx0 = lua_static_randint_range(ps0, 0, 52);
    int idx1 = lua_static_randint_range(ps1, 0, 52);
    int idx2 = lua_static_randint_range(ps2, 0, 52);
    int idx3 = lua_static_randint_range(ps3, 0, 52);
    
    s->rank_counts[internal_rank_to_standard(idx0 % 13)]++;
    s->suit_counts[idx0 / 13]++;
    s->rank_counts[internal_rank_to_standard(idx1 % 13)]++;
    s->suit_counts[idx1 / 13]++;
    s->rank_counts[internal_rank_to_standard(idx2 % 13)]++;
    s->suit_counts[idx2 / 13]++;
    s->rank_counts[internal_rank_to_standard(idx3 % 13)]++;
    s->suit_counts[idx3 / 13]++;
}


// ==================== Variable-length seed conversion ====================

GPU_DEVICE void seed_index_to_string_varlen(uint64_t index, int len, char* out) {
    for (int i = len - 1; i >= 0; i--) {
        out[i] = SEED_CHARS[index % 35];
        index /= 35;
    }
    out[len] = 0;
}

// Host versions
void seed_index_to_string_host(uint64_t index, int len, char* out) {
    const char* chars = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    for (int i = len - 1; i >= 0; i--) {
        out[i] = chars[index % 35];
        index /= 35;
    }
    out[len] = 0;
}

uint64_t get_seed_space_size(int len) {
    uint64_t size = 1;
    for (int i = 0; i < len; i++) size *= 35;
    return size;
}

// ==================== Search Kernel ====================

GPU_KERNEL __launch_bounds__(256) void erratic_full_search_kernel(
    uint64_t batch_index,
    uint64_t local_start,
    uint64_t num,
    uint64_t suffix_multiplier,
    int seed_len,
    int rank_threshold, int suit_threshold,
    int target_rank,
    ErraticResult* results, int* count, int max_results
) {
    uint64_t tid = blockIdx.x * blockDim.x + threadIdx.x;
    uint64_t stride = gridDim.x * blockDim.x;

    for (uint64_t i = tid; i < num; i += stride) {
        uint64_t local_idx = local_start + i;
        uint64_t seed_idx = batch_index + local_idx * suffix_multiplier;
        char seed[9];
        seed_index_to_string_varlen(seed_idx, seed_len, seed);

        DeckStats s;
        generate_erratic_deck_stats(seed, seed_len, &s);

        // Check if any rank >= threshold or any suit >= threshold
        bool found = false;
        int found_count = 0;
        if (target_rank >= 0) {
            if (s.rank_counts[target_rank] >= rank_threshold) {
                found = true;
                found_count = s.rank_counts[target_rank];
            }
        } else {
            // Check all ranks - find highest
            int max_count = 0;
            for (int r = 0; r < 13; r++) {
                if (s.rank_counts[r] >= rank_threshold && s.rank_counts[r] > max_count) {
                    max_count = s.rank_counts[r];
                    found = true;
                }
            }
            // Also check suits if no target rank specified
            for (int su = 0; su < 4; su++) {
                if (s.suit_counts[su] >= suit_threshold) found = true;
            }
            found_count = max_count;
        }

        if (found) {
            int idx = GPU_ATOMIC_ADD(count, 1);
            if (idx < max_results) {
                for (int j = 0; j < 9; j++) results[idx].seed_str[j] = seed[j];
                results[idx].count = found_count;
            }
        }
    }
}


// ==================== Verify Kernel ====================

GPU_KERNEL void verify_erratic_kernel(const char* seed, int len) {
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        printf("=== Erratic Deck for seed: ");
        for (int i = 0; i < len; i++) printf("%c", seed[i]);
        printf(" ===\n\n");
        DeckStats s; generate_erratic_deck_stats(seed, len, &s);
        const char* ranks = "23456789TJQKA";
        printf("Rank Counts:\n");
        for (int r = 0; r < 13; r++) printf("  %c: %d\n", ranks[r], s.rank_counts[r]);
        printf("\nSuit Counts:\n  Clubs: %d\n  Diamonds: %d\n  Hearts: %d\n  Spades: %d\n",
               s.suit_counts[0], s.suit_counts[1], s.suit_counts[2], s.suit_counts[3]);
        printf("\n=== Summary ===\nRanks: ");
        for (int r = 12; r >= 0; r--) if (s.rank_counts[r] > 0) printf("%c:%d ", ranks[r], s.rank_counts[r]);
        printf("\nSuits: S:%d H:%d C:%d D:%d\n", s.suit_counts[3], s.suit_counts[2], s.suit_counts[0], s.suit_counts[1]);
    }
}

// ==================== Main ====================

int parse_rank_name(const char* name) {
    if (!name) return -1;
    char lower[16];
    int len = 0;
    for (int i = 0; name[i] && i < 15; i++) {
        lower[i] = (char)tolower((unsigned char)name[i]);
        len++;
    }
    lower[len] = '\0';
    
    // Single char: 2,3,4,5,6,7,8,9,T,J,Q,K,A
    if (len == 1) {
        if (lower[0] == '2') return 0;
        if (lower[0] == '3') return 1;
        if (lower[0] == '4') return 2;
        if (lower[0] == '5') return 3;
        if (lower[0] == '6') return 4;
        if (lower[0] == '7') return 5;
        if (lower[0] == '8') return 6;
        if (lower[0] == '9') return 7;
        if (lower[0] == 't') return 8;
        if (lower[0] == 'j') return 9;
        if (lower[0] == 'q') return 10;
        if (lower[0] == 'k') return 11;
        if (lower[0] == 'a') return 12;
    }
    
    // Full names
    if (strcmp(lower, "two") == 0 || strcmp(lower, "2s") == 0) return 0;
    if (strcmp(lower, "three") == 0 || strcmp(lower, "3s") == 0) return 1;
    if (strcmp(lower, "four") == 0 || strcmp(lower, "4s") == 0) return 2;
    if (strcmp(lower, "five") == 0 || strcmp(lower, "5s") == 0) return 3;
    if (strcmp(lower, "six") == 0 || strcmp(lower, "6s") == 0) return 4;
    if (strcmp(lower, "seven") == 0 || strcmp(lower, "7s") == 0) return 5;
    if (strcmp(lower, "eight") == 0 || strcmp(lower, "8s") == 0) return 6;
    if (strcmp(lower, "nine") == 0 || strcmp(lower, "9s") == 0) return 7;
    if (strcmp(lower, "ten") == 0 || strcmp(lower, "10s") == 0) return 8;
    if (strcmp(lower, "jack") == 0 || strcmp(lower, "jacks") == 0) return 9;
    if (strcmp(lower, "queen") == 0 || strcmp(lower, "queens") == 0) return 10;
    if (strcmp(lower, "king") == 0 || strcmp(lower, "kings") == 0) return 11;
    if (strcmp(lower, "ace") == 0 || strcmp(lower, "aces") == 0) return 12;
    
    return -1;
}

void check_gpu_error(GPUError err, const char* msg) {
    if (err != GPU_SUCCESS) { fprintf(stderr, "GPU Error at %s: %d\n", msg, (int)err); exit(1); }
}

void print_help() {
    printf("Balatro Erratic Deck CUDA Searcher v3.0\n");
    printf("========================================\n\n");
    printf("Usage:\n");
    printf("  erratic_search.exe --full-scan\n");
    printf("  erratic_search.exe --verify ALEEB\n");
    printf("  erratic_search.exe --benchmark\n\n");
    printf("Full scan options:\n");
    printf("  --full-scan              Scan all seed lengths (1-8 chars)\n");
    printf("  --rank-threshold <n>     Min rank count to save (default: 17)\n");
    printf("  --suit-threshold <n>     Min suit count to save (default: 31)\n");
    printf("  --rank <name>            Only search for specific rank (2,3,4,5,6,7,8,9,T,J,Q,K,A or Two,Three,etc)\n");
    printf("  --seed-len <n>           Only scan seeds of this length\n");
    printf("  --start-index <n>        Start from this seed index (for resume)\n");
    printf("  --start-batch <n>        Start from this batch index (preferred)\n");
    printf("  --batch-chars <n>        Batch size in characters (default: 4)\n");
    printf("  --output-file <file>     Output to single file instead of rank/suit directories\n\n");
    printf("Output: ./ranks/2s.txt ... Aces.txt, ./suits/Clubs.txt ... Spades.txt\n");
    printf("  (or single file if --output-file specified)\n");
}

int main(int argc, char** argv) {
    char verify_seed[16] = "";
    bool benchmark_mode = false;
    bool full_scan = false;
    int rank_threshold = 17;
    int suit_threshold = 31;
    int target_rank = -1;  // -1 = any rank, 0-12 = specific rank
    int specific_len = 0;  // 0 = scan all lengths
    uint64_t start_index = 0;
    int64_t start_batch_i64 = -1;  // -1 = use start_index, otherwise use batch index
    int batch_chars = 4;
    const char* output_file = NULL;

    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--verify") == 0 && i+1 < argc) { strncpy(verify_seed, argv[++i], 15); }
        else if (strcmp(argv[i], "--benchmark") == 0) benchmark_mode = true;
        else if (strcmp(argv[i], "--full-scan") == 0) full_scan = true;
        else if (strcmp(argv[i], "--rank-threshold") == 0 && i+1 < argc) rank_threshold = atoi(argv[++i]);
        else if (strcmp(argv[i], "--suit-threshold") == 0 && i+1 < argc) suit_threshold = atoi(argv[++i]);
        else if (strcmp(argv[i], "--rank") == 0 && i+1 < argc) target_rank = parse_rank_name(argv[++i]);
        else if (strcmp(argv[i], "--seed-len") == 0 && i+1 < argc) specific_len = atoi(argv[++i]);
        else if (strcmp(argv[i], "--start-index") == 0 && i+1 < argc) start_index = strtoull(argv[++i], NULL, 10);
        else if (strcmp(argv[i], "--start-batch") == 0 && i+1 < argc) start_batch_i64 = strtoll(argv[++i], NULL, 10);
        else if (strcmp(argv[i], "--batch-chars") == 0 && i+1 < argc) batch_chars = atoi(argv[++i]);
        else if (strcmp(argv[i], "--output-file") == 0 && i+1 < argc) output_file = argv[++i];
        else if (strcmp(argv[i], "--help") == 0) { print_help(); return 0; }
    }
    
    if (target_rank < 0 && full_scan) {
        fprintf(stderr, "Warning: No --rank specified, will scan all ranks (large output!)\n");
    }

    int device; GPU_GET_DEVICE(&device);
    GPUDeviceProp prop; GPU_GET_DEVICE_PROPERTIES(&prop, device);
    printf("GPU: %s (SM %d.%d)\n\n", prop.name, prop.major, prop.minor);

    // ========== VERIFY MODE ==========
    if (verify_seed[0]) {
        char* d_seed; GPU_MALLOC((void**)&d_seed, 16);
        GPU_MEMCPY(d_seed, verify_seed, 16, GPU_MEMCPY_HOST_TO_DEVICE);
        verify_erratic_kernel<<<1, 1>>>(d_seed, strlen(verify_seed));
        GPU_DEVICE_SYNCHRONIZE(); GPU_FREE(d_seed);
        return 0;
    }

    int num_blocks = prop.multiProcessorCount * 32;

    

    // ========== FULL SCAN MODE ==========
    if (full_scan) {
        FILE* out_file = NULL;
        bool single_file_mode = (output_file != NULL || target_rank >= 0);
        
        if (single_file_mode) {
            const char* filename = output_file ? output_file : "results.txt";
            g_current_output_file = filename;
            bool is_resuming = (start_index > 0 || start_batch_i64 >= 0);
            const char* mode = is_resuming ? "a" : "w";
            out_file = fopen(filename, mode);
            if (!out_file) { fprintf(stderr, "Cannot open %s\n", filename); return 1; }
            if (is_resuming) {
                if (start_batch_i64 >= 0) {
                    fprintf(stderr, "Resuming from batch %lld - appending to %s\n", 
                            (long long)start_batch_i64, filename);
                } else {
                    fprintf(stderr, "Resuming from index %llu - appending to %s\n", 
                            (unsigned long long)start_index, filename);
                }
                fseek(out_file, 0, SEEK_END);
                // Don't write header when resuming - file already exists
            } else {
                // Only write header for new files
                // (No header needed for CSV - just seed,count)
            }
        }
        
        // Allocate GPU buffers
        ErraticResult* d_results;
        int* d_count;
        GPU_MALLOC((void**)&d_results, sizeof(ErraticResult) * BATCH_BUFFER_SIZE);
        GPU_MALLOC((void**)&d_count, sizeof(int));
        ErraticResult* h_results = (ErraticResult*)malloc(sizeof(ErraticResult) * BATCH_BUFFER_SIZE);

        printf("=== FULL ERRATIC DECK SCAN ===\n");
        if (target_rank >= 0) {
            const char* rank_names[13] = {"2", "3", "4", "5", "6", "7", "8", "9", "T", "J", "Q", "K", "A"};
            printf("Target rank: %s\n", rank_names[target_rank]);
        }
        printf("Rank threshold: >= %d\n", rank_threshold);
        if (target_rank < 0) {
            printf("Suit threshold: >= %d\n", suit_threshold);
        }
        if (single_file_mode) {
            printf("Output: %s\n\n", output_file ? output_file : "results.txt");
        } else {
            printf("Output: ./ranks/*.txt, ./suits/*.txt\n\n");
        }


        // Setup signal handler for Ctrl+C (print resume command)
        signal(SIGINT, print_resume_command);
        #ifdef _WIN32
        SetConsoleCtrlHandler((PHANDLER_ROUTINE)print_resume_command, TRUE);
        #endif
        
        // Store current config for resume command
        g_current_batch_chars = batch_chars;
        g_current_target_rank = target_rank;
        g_current_rank_threshold = rank_threshold;

        auto total_start = std::chrono::high_resolution_clock::now();

        // Scan each seed length (8 down to 1, or specific length)
        int start_len = specific_len ? specific_len : 8;
        int end_len = specific_len ? specific_len : 1;

        for (int seed_len = start_len; seed_len >= end_len; seed_len--) {
            uint64_t space_size = get_seed_space_size(seed_len);

            printf("\n--- Scanning %d-char seeds (space: %llu) ---\n", seed_len, (unsigned long long)space_size);

            // Batch processing: effective batch_chars for this seed length
            int effective_batch_chars = (batch_chars > seed_len) ? seed_len : batch_chars;
            uint64_t seeds_per_batch = calculate_seeds_per_batch(effective_batch_chars);
            // Exact for base-35: 35^seed_len / 35^effective_batch_chars = 35^(seed_len-effective_batch_chars)
            uint64_t total_batches = (seeds_per_batch > 0) ? (space_size / seeds_per_batch) : 0;
            if (total_batches == 0) total_batches = 1;
            uint64_t suffix_multiplier = total_batches;
            
            printf("Batch chars: %d (effective: %d) | Seeds/batch: %llu | Total batches: %llu\n",
                   batch_chars, effective_batch_chars, (unsigned long long)seeds_per_batch, 
                   (unsigned long long)total_batches);
            
            auto len_start = std::chrono::high_resolution_clock::now();
            
            // Simple: flush every batch, show progress every batch
            

            // Start position (only applies to the first scanned length)
            uint64_t start_batch = 0;
            uint64_t start_local = 0;
            if (seed_len == start_len) {
                if (start_batch_i64 >= 0) {
                    // User-facing batches are 1-based (so copy/paste from progress works). 0 also means "start".
                    uint64_t user_batch = (uint64_t)start_batch_i64;
                    start_batch = (user_batch > 0) ? (user_batch - 1) : 0;
                } else if (start_index > 0) {
                    if (start_index >= space_size) {
                        start_batch = total_batches;
                        start_local = 0;
                    } else {
                        start_batch = start_index % suffix_multiplier;
                        start_local = start_index / suffix_multiplier;
                    }
                }
            }
            
            for (uint64_t batch = start_batch; batch < total_batches; batch++) {
                if (g_interrupted) break;
                
                uint64_t local_start = (batch == start_batch) ? start_local : 0;
                if (local_start >= seeds_per_batch) continue;
                uint64_t batch_count = seeds_per_batch - local_start;
                
                g_current_batch_index = batch;
                g_current_seed_len = seed_len;

                int zero = 0;
                GPU_MEMCPY_ASYNC(d_count, &zero, sizeof(int), GPU_MEMCPY_HOST_TO_DEVICE, 0);

                erratic_full_search_kernel<<<num_blocks, BLOCK_SIZE>>>(
                    batch, local_start, batch_count, suffix_multiplier, seed_len, rank_threshold, suit_threshold,
                    target_rank, d_results, d_count, BATCH_BUFFER_SIZE
                );
                check_gpu_error(GPU_GET_LAST_ERROR(), "kernel launch");
                
                // Don't sync immediately - let GPU keep working
                // Only sync when we need results (every N batches or at end)
                auto sync_start = std::chrono::high_resolution_clock::now();
                GPU_DEVICE_SYNCHRONIZE();
                auto sync_end = std::chrono::high_resolution_clock::now();

                int result_count;
                GPU_MEMCPY(&result_count, d_count, sizeof(int), GPU_MEMCPY_DEVICE_TO_HOST);

                int actual_count = (result_count < BATCH_BUFFER_SIZE) ? result_count : BATCH_BUFFER_SIZE;
                if (actual_count > 0) {
                    GPU_MEMCPY(h_results, d_results, sizeof(ErraticResult) * actual_count, GPU_MEMCPY_DEVICE_TO_HOST);
                    
                    for (int i = 0; i < actual_count; i++) {
                        h_results[i].seed_str[8] = '\0';
                        if (single_file_mode && out_file) {
                            fprintf(out_file, "%s,%d\n", h_results[i].seed_str, h_results[i].count);
                        } else {
                            printf("%s,%d\n", h_results[i].seed_str, h_results[i].count);
                        }
                    }
                    
                    if (single_file_mode && out_file) fflush(out_file);
                    else fflush(stdout);
                }

                // Progress every batch
                auto elapsed_ms = std::chrono::duration_cast<std::chrono::milliseconds>(sync_end - sync_start);
                double elapsed_sec = elapsed_ms.count() / 1000.0;
                double speed = (elapsed_sec > 0.001) ? (double)batch_count / elapsed_sec / 1000000.0 : 0.0;
                uint64_t processed = (batch + 1) * seeds_per_batch;
                if (processed > space_size) processed = space_size;
                double progress = (double)processed / space_size * 100.0;

                char seed_display[9];
                uint64_t display_seed_idx = batch + local_start * suffix_multiplier;
                seed_index_to_string_host(display_seed_idx, seed_len, seed_display);

                fprintf(stderr, "\r  [%d-char] %.2f%% | Batch: %llu/%llu | Seed: %s | Speed: %.1f M/s    ",
                       seed_len, progress, (unsigned long long)(batch + 1), (unsigned long long)total_batches,
                       seed_display, speed);
                fflush(stderr);

            }
            
            if (single_file_mode && out_file) fflush(out_file);
            else fflush(stdout);
            fprintf(stderr, "\n");
        }

        auto total_end = std::chrono::high_resolution_clock::now();
        auto total_dur = std::chrono::duration_cast<std::chrono::seconds>(total_end - total_start);

        fprintf(stderr, "\n=== SCAN COMPLETE ===\n");
        fprintf(stderr, "Time: %lld seconds (%.2f hours)\n", (long long)total_dur.count(), total_dur.count() / 3600.0);

        if (single_file_mode && out_file) {
            fclose(out_file);
        }
        free(h_results);
        GPU_FREE(d_results);
        GPU_FREE(d_count);
        return 0;
    }

    // Default: show help
    print_help();
    return 0;
}

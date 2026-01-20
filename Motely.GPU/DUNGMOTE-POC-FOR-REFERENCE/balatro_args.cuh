/**
 * @file balatro_args.cuh
 * @brief Shared command-line argument parsing utilities
 */

#ifndef BALATRO_ARGS_CUH
#define BALATRO_ARGS_CUH

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>

/**
 * @brief Parse GPU tuning flags from command-line arguments
 * 
 * Looks for --block-size N and --blocks-per-sm N flags anywhere in argv
 * 
 * @param argc Argument count
 * @param argv Argument vector
 * @param block_size Output: threads per block (default: 256)
 * @param blocks_per_sm Output: blocks per SM (default: 32)
 */
__host__ void parse_gpu_flags(int argc, char** argv, int* block_size, int* blocks_per_sm) {
    *block_size = 256;
    *blocks_per_sm = 32;
    
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--block-size") == 0 && i + 1 < argc) {
            *block_size = atoi(argv[++i]);
        } else if (strcmp(argv[i], "--blocks-per-sm") == 0 && i + 1 < argc) {
            *blocks_per_sm = atoi(argv[++i]);
        }
    }
}

/**
 * @brief Check if an argument is a flag (starts with --)
 */
__host__ bool is_flag(const char* arg) {
    return arg && arg[0] == '-' && arg[1] == '-';
}

/**
 * @brief Get the value for a flag that takes an argument, supporting both
 *        "--flag value" and "--flag=value" forms.
 */
__host__ const char* get_flag_value(int argc, char** argv, const char* flag) {
    size_t flag_len = strlen(flag);
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], flag) == 0 && i + 1 < argc) {
            return argv[i + 1];
        }
        if (strncmp(argv[i], flag, flag_len) == 0 && argv[i][flag_len] == '=') {
            return argv[i] + flag_len + 1;
        }
    }
    return NULL;
}

/**
 * @brief Get positional argument at index, skipping flags
 * 
 * @param argc Argument count
 * @param argv Argument vector
 * @param index Positional index (0-based)
 * @return Pointer to argument string, or NULL if not found
 */
__host__ const char* get_positional_arg(int argc, char** argv, int index) {
    int pos = 0;
    for (int i = 1; i < argc; i++) {
        if (is_flag(argv[i])) {
            i++; // Skip flag value
            continue;
        }
        if (pos == index) {
            return argv[i];
        }
        pos++;
    }
    return NULL;
}

/**
 * @brief Parse comma-separated integer list
 * 
 * @param str Input string like "1,2,3"
 * @param out Output array
 * @param max_count Maximum number of integers to parse
 * @return Number of integers parsed
 */
__host__ int parse_int_list(const char* str, int* out, int max_count) {
    if (!str) return 0;
    
    int count = 0;
    const char* start = str;
    
    while (*str && count < max_count) {
        if (*str == ',') {
            *out++ = atoi(start);
            count++;
            start = str + 1;
        }
        str++;
    }
    if (*start && count < max_count) {
        *out++ = atoi(start);
        count++;
    }
    
    return count;
}

/**
 * @brief Parse batch processing flags from command-line arguments
 *
 * Looks for --batch-chars N, --start-batch N, and --end-batch N flags anywhere in argv
 *
 * @param argc Argument count
 * @param argv Argument vector
 * @param batch_chars Output: number of prefix characters in a batch (default: 1)
 * @param start_batch Output: starting batch index (default: 0)
 * @param end_batch Output: ending batch index (default: -1 if not set, meaning "process all")
 */
__host__ void parse_batch_flags(int argc, char** argv, int* batch_chars, int64_t* start_batch, int64_t* end_batch) {
    *batch_chars = 1;
    *start_batch = 0;
    *end_batch = -1;

    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--batch-chars") == 0 && i + 1 < argc) {
            *batch_chars = atoi(argv[++i]);
            if (*batch_chars < 1) *batch_chars = 1;
            if (*batch_chars > 8) *batch_chars = 8;
        } else if (strcmp(argv[i], "--start-batch") == 0 && i + 1 < argc) {
            *start_batch = (int64_t)strtoll(argv[++i], NULL, 10);
        } else if (strcmp(argv[i], "--end-batch") == 0 && i + 1 < argc) {
            *end_batch = (int64_t)strtoll(argv[++i], NULL, 10);
        }
    }
}

__host__ const char* parse_jaml_flag(int argc, char** argv) {
    return get_flag_value(argc, argv, "--jaml-file");
}

__host__ const char* parse_json_flag(int argc, char** argv) {
    return get_flag_value(argc, argv, "--json-file");
}

__host__ bool parse_json_stdin_flag(int argc, char** argv) {
    for (int i = 1; i < argc; i++) {
        if (strcmp(argv[i], "--json-stdin") == 0 || strcmp(argv[i], "--json") == 0) {
            return true;
        }
    }
    return false;
}

#endif // BALATRO_ARGS_CUH


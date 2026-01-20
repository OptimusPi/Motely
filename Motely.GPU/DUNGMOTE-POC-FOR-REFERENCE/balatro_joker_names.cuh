/**
 * @file balatro_joker_names.cuh
 * @brief Map Motely-style joker names to our joker IDs
 */

#ifndef BALATRO_JOKER_NAMES_CUH
#define BALATRO_JOKER_NAMES_CUH

#include "balatro_enums.cuh"
#include <string.h>
#include <ctype.h>

__host__ __forceinline__ int joker_name_to_id(const char* name) {
    if (!name) return -1;

    char lower[64];
    int len = 0;
    for (int i = 0; name[i] && i < 63; i++) {
        lower[i] = (char)tolower((unsigned char)name[i]);
        len++;
    }
    lower[len] = '\0';

    char clean[64];
    int clean_len = 0;
    for (int i = 0; i < len; i++) {
        if (isalnum((unsigned char)lower[i])) clean[clean_len++] = lower[i];
    }
    clean[clean_len] = '\0';

#define MAKE_JOKER_ID(rarity, index) ((int)(rarity) | ((index) & JOKER_INDEX_MASK))

    // Minimal set for our main use-cases (expand later if needed)
    if (strcmp(clean, "oopsall6s") == 0 || strcmp(clean, "oopsall6") == 0 || strcmp(clean, "oopsall") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_OOPS_ALL_6S);
    }
    if (strcmp(clean, "blueprint") == 0) return MAKE_JOKER_ID(RARITY_RARE, J_BLUEPRINT);
    if (strcmp(clean, "brainstorm") == 0) return MAKE_JOKER_ID(RARITY_RARE, J_BRAINSTORM);
    if (strcmp(clean, "invisiblejoker") == 0) return MAKE_JOKER_ID(RARITY_RARE, J_INVISIBLE_JOKER);
    if (strcmp(clean, "turtlebean") == 0 || strcmp(clean, "turtle") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_TURTLE_BEAN);
    }
    if (strcmp(clean, "showman") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_SHOWMAN);
    }
    if (strcmp(clean, "juggler") == 0) {
        return MAKE_JOKER_ID(RARITY_COMMON, J_JUGGLER);
    }
    if (strcmp(clean, "troubadour") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_TROUBADOUR);
    }
    if (strcmp(clean, "hangingchad") == 0 || strcmp(clean, "hanging") == 0) {
        return MAKE_JOKER_ID(RARITY_COMMON, J_HANGING_CHAD);
    }
    if (strcmp(clean, "sockandbuskin") == 0 || strcmp(clean, "sockbuskin") == 0 || strcmp(clean, "buskin") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_SOCK_AND_BUSKIN);
    }
    if (strcmp(clean, "dusk") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_DUSK);
    }
    if (strcmp(clean, "baron") == 0 || strcmp(clean, "duskbaron") == 0) {
        return MAKE_JOKER_ID(RARITY_RARE, J_BARON);
    }
    if (strcmp(clean, "mine") == 0 || strcmp(clean, "mime") == 0) {
        return MAKE_JOKER_ID(RARITY_UNCOMMON, J_MIME);
    }
    if (strcmp(clean, "goldenticket") == 0 || strcmp(clean, "goldent") == 0) {
        return MAKE_JOKER_ID(RARITY_COMMON, J_GOLDEN_TICKET);
    }

#undef MAKE_JOKER_ID
    return -1;
}

#endif // BALATRO_JOKER_NAMES_CUH



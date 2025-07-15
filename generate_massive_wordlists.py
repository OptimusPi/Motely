#!/usr/bin/env python3
"""
Massive wordlist generator for Balatro seed searching
Generates thousands of 8-character combinations for each category
ONLY USES: 123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ (no 0!)
ALL SEEDS EXACTLY 8 CHARACTERS!
"""

import itertools
from typing import List, Set

VALID_CHARS = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"

# Padding patterns - NO ZEROS!
NUMBERS = ["1", "2", "3", "4", "5", "6", "7", "8", "9"]
POPULAR_NUMS = ["69", "42", "666", "777", "888", "999", "123", "321", "111", "222", "333", "444", "555", "1337", "9999", "8888", "7777", "1234", "4321", "2222", "3333", "4444", "5555", "6666", "1111"]

# Cool words (3-7 chars MAX to leave room for padding!)
COOL_WORDS = [
    # Gaming (3-7 chars)
    "GAME", "PLAY", "WIN", "LOSS", "EPIC", "MEGA", "GIGA", "TERA",
    "BOSS", "HERO", "GOD", "KING", "QUEEN", "ACE", "JOKER", "CARD", "DECK", "HAND",
    "BET", "FOLD", "CALL", "RAISE", "BLUFF", "POKER", "FLUSH", "ROYAL", "FULL",
    "PAIR", "TRIPS", "QUADS", "HIGH", "LOW", "WILD", "DRAW", "DEAL",
    
    # Power words (3-7 chars)
    "POWER", "FORCE", "ULTRA", "SUPER", "HYPER", "TURBO", "NITRO", "BOOST", "SPEED",
    "FAST", "QUICK", "SWIFT", "RAPID", "FLASH", "BOLT", "SHOCK", "SPARK", "FLAME",
    "FIRE", "ICE", "FROST", "FREEZE", "BURN", "BLAZE", "STORM", "WIND", "GALE",
    
    # Epic/Cool (3-7 chars)
    "CHAD", "SIGMA", "ALPHA", "BETA", "OMEGA", "DELTA", "GAMMA", "PRIME", "ELITE",
    "PRO", "MASTER", "LEGEND", "MYTH", "FABLE", "SAGA", "TALE", "LORE", "DOOM",
    
    # Tech/Cyber (3-7 chars)
    "TECH", "CYBER", "HACK", "CODE", "DATA", "BYTE", "BIT", "PIXEL", "MATRIX",
    "GRID", "NET", "WEB", "LINK", "NODE", "PORT", "GATE", "CORE", "SYSTEM",
    
    # Animals (3-7 chars)
    "DRAGON", "PHOENIX", "WOLF", "BEAR", "LION", "TIGER", "EAGLE", "HAWK", "RAVEN",
    "SHARK", "WHALE", "VIPER", "COBRA", "PYTHON", "BEAST", "MONSTER",
    
    # Short power words (3-5 chars)
    "MAX", "MAXX", "NEO", "ZEN", "APEX", "PEAK", "TOP", "BEST", "PURE", "TRUE",
    "REAL", "FAKE", "ANTI", "DARK", "LIGHT", "VOID", "NULL", "ZERO", "ONE", "TWO",
    "RED", "BLUE", "GREEN", "GOLD", "BLACK", "WHITE", "GRAY", "NEON", "GLOW"
]

# LOL/Meme words (3-7 chars)
LOL_WORDS = [
    "LOL", "LMAO", "ROFL", "KEK", "HAHA", "HEHE", "JAJA", "LULZ", "LAWL",
    "MEME", "DANK", "BASED", "CRINGE", "BRUH", "YEET", "YOLO", "SWAG", "LIT",
    "FAM", "SALTY", "TOXIC", "NOOB", "REKT", "PWNED", "OWNED", "GG", "EZ",
    "MLG", "SNIPE", "SCOPE", "TRICK", "SHOT", "COMBO", "CHAIN", "SPREE",
    "TROLL", "BAIT", "TRAP", "PRANK", "JOKE", "JEST", "GAG", "FAIL", "OOPS",
    "DERP", "DUMB", "STUPID", "SILLY", "GOOFY", "WACKY", "CRAZY", "WEIRD", "ODD",
    "SUS", "AMONG", "VENT", "TASK", "VOTE", "EJECT", "CREW", "IMP",
    "POG", "POGGERS", "CHAMP", "HYPE", "VIBE", "MOOD", "FEELS", "COPE", "MALD",
    "SIMP", "STAN", "FLEX", "DRIP", "SAUCE", "BUSSIN", "SHEESH", "OOF", "RIP",
    "PEPEGA", "KAPPA", "OMEGALUL", "KEKW", "MONKAS", "PEPE", "SADGE", "COPIUM",
    "BOOMER", "ZOOMER", "DOOMER", "COOMER", "GOATED", "RATIO", "CLAPPED", "DIFF"
]

# Gross words (3-7 chars)
GROSS_WORDS = [
    "GROSS", "NASTY", "YUCK", "EWW", "BLEH", "BARF", "PUKE", "VOMIT", "HURL",
    "GAG", "SICK", "ILL", "GERM", "VIRUS", "FUNGUS", "MOLD", "ROT", "DECAY",
    "STINK", "STENCH", "REEK", "SMELL", "ODOR", "BOOGER", "SNOT", "MUCUS",
    "SPIT", "DROOL", "SLIME", "OOZE", "GOO", "GUNK", "CRUD", "CRUST", "SCAB",
    "PUS", "BLOOD", "GORE", "FLESH", "MEAT", "BONE", "FAT", "GREASE", "OIL",
    "STICKY", "GUMMY", "PASTE", "GLUE", "TAR", "WAX", "FOAM", "DUST", "DIRT",
    "MUD", "TRASH", "GARBAGE", "WASTE", "JUNK", "TOXIC", "POISON", "VENOM",
    "ACID", "BILE", "PHLEGM", "DISCHARGE", "SEEP", "LEAK", "DRIP", "SQUIRT"
]

# NSFW words (3-7 chars)
NSFW_WORDS = [
    # Basic adult terms
    "SEXY", "HOT", "DAMN", "THICC", "THICK", "JUICY", "BUSTY", "CURVY", "FINE",
    "BANG", "SMASH", "POUND", "SLAM", "THRUST", "GRIND", "RUB", "TOUCH", "FEEL",
    "LICK", "SUCK", "BITE", "KISS", "BLOW", "SWALLOW", "EAT", "TASTE", "DADDY",
    "MOMMY", "BABY", "HONEY", "SUGAR", "SPICE", "NAUGHTY", "BAD", "DIRTY", "NASTY",
    "KINKY", "FETISH", "TEASE", "TEMPT", "SEDUCE", "FLIRT", "HORNY", "LUSTY",
    "PASSION", "DESIRE", "HEAT", "FIRE", "BURN", "WET", "MOIST", "DRIP", "LEAK",
    "HARD", "SOFT", "TIGHT", "LOOSE", "BIG", "HUGE", "TINY", "THICK", "LONG",
    "DEEP", "WIDE", "SPREAD", "OPEN", "CLOSE", "CLAP", "SPANK", "SLAP", "CHOKE",
    "TIE", "BIND", "CUFF", "WHIP", "PADDLE", "PLUG", "TOY", "PLAY", "FUN",
    "STRIP", "NUDE", "BARE", "SKIN", "FLESH", "BODY", "BOOBS", "TITS", "ASS",
    "BUTT", "BOOTY", "CHEEKS", "RACK", "MELONS", "PEACH", "BANANA", "WOOD",
    "BONE", "PIPE", "POLE", "ROD", "STICK", "SHAFT", "TIP", "HEAD", "BALLS",
    "NUTS", "SACK", "PACKAGE", "JUNK", "MEMBER", "UNIT", "TOOL", "PIECE",
    
    # Explicit terms
    "DICK", "COCK", "PENIS", "DONG", "WANG", "PRICK", "BONER", "STIFFY",
    "PUSSY", "CUNT", "VAGINA", "TWAT", "SNATCH", "COOCH", "MUFF", "POON",
    "CUM", "JIZZ", "SPERM", "LOAD", "NUT", "CREAM", "SPOOGE", "SEED",
    "FUCK", "SCREW", "SHAG", "HUMP", "BONE", "RAIL", "PLOW", "DRILL",
    "SHIT", "PISS", "CRAP", "DUMP", "TURD", "POOP", "DOOKIE", "FART",
    "BITCH", "SLUT", "WHORE", "HOE", "SKANK", "THOT", "TRAMP", "SLAG",
    "BASTARD", "ASSHOLE", "DOUCHE", "PRICK", "FUCKER", "WANKER", "TOSSER",
    
    # Body parts & actions
    "NIPPLE", "TIT", "BREAST", "CLIT", "LABIA", "VULVA", "ANUS", "HOLE",
    "FINGER", "FIST", "TONGUE", "MOUTH", "LIPS", "THROAT", "GAG", "CHOKE",
    "SQUIRT", "SPRAY", "SHOOT", "BLAST", "PUMP", "THROB", "PULSE", "SWELL",
    "INSERT", "ENTER", "FILL", "STUFF", "STRETCH", "GAP", "SPLIT", "TEAR",
    
    # Descriptive terms
    "RAW", "ROUGH", "WILD", "FERAL", "PRIMAL", "BEAST", "ANIMAL", "SAVAGE",
    "FILTHY", "RAUNCHY", "VULGAR", "LEWD", "CRUDE", "SMUTTY", "PORN", "XXX",
    "TABOO", "FORBID", "NAUGHTY", "SINFUL", "WICKED", "EVIL", "PERV", "FREAK",
    
    # Common adult slang
    "MILF", "DILF", "GILF", "COUGAR", "PAWG", "BBW", "BBC", "BWC",
    "DTF", "FWB", "ONS", "NSA", "BDSM", "DOM", "SUB", "SWITCH",
    "EDGING", "CLIMAX", "ORGASM", "FINISH", "RELEASE", "EXPLODE", "ERUPT", "PEAK"
]

def generate_padded_combinations(word: str, max_results: int = 200) -> Set[str]:
    """Generate all possible 8-char combinations from a word"""
    results = set()
    word = word.upper()
    
    # Remove invalid chars
    word = ''.join(c for c in word if c in VALID_CHARS)
    
    if not word or len(word) > 8:
        return results
    
    if len(word) == 8:
        results.add(word)
        return results
    
    needed = 8 - len(word)
    
    # Number padding
    for num in POPULAR_NUMS:
        if len(num) <= needed:
            # Pad at end
            if len(word + num) <= 8:
                remaining = 8 - len(word) - len(num)
                if remaining == 0:
                    results.add(word + num)
                elif remaining > 0:
                    for filler in ['1', '9', 'X', 'Z']:
                        results.add(word + num + filler * remaining)
            
            # Pad at start
            if len(num + word) <= 8:
                remaining = 8 - len(num) - len(word)
                if remaining == 0:
                    results.add(num + word)
                elif remaining > 0:
                    for filler in ['1', '9', 'X', 'Z']:
                        results.add(num + word + filler * remaining)
    
    # Character repetition
    results.add(word + word[-1] * needed)  # LOVE -> LOVEEEE
    results.add(word[0] * needed + word)   # LOVE -> LLLLLOVE
    
    # Simple padding
    for pad_char in ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'X', 'Z', 'Q']:
        results.add(word + pad_char * needed)
    
    # Limit results
    return set(list(results)[:max_results])

def generate_leet_variations(word: str) -> List[str]:
    """Generate l33t speak variations"""
    leet_map = {
        'A': '4', 'E': '3', 'I': '1', 'O': '0',  # Note: O->0 won't be used since 0 isn't valid
        'S': '5', 'T': '7', 'G': '6', 'B': '8', 'L': '1'
    }
    
    # Only do simple substitutions to avoid explosion
    variations = [word]
    for char, leet in leet_map.items():
        if char in word and leet in VALID_CHARS:
            variations.append(word.replace(char, leet))
    
    return variations

def generate_word_combinations(words: List[str], output_file: str, category: str):
    """Generate massive combinations and save to file"""
    all_seeds = set()
    
    print(f"Generating {category} seeds...")
    
    for word in words:
        if len(word) > 8:
            continue
            
        # Add original padded versions
        padded = generate_padded_combinations(word)
        all_seeds.update(padded)
        
        # Add leet variations
        for leet_word in generate_leet_variations(word):
            padded_leet = generate_padded_combinations(leet_word)
            all_seeds.update(padded_leet)
    
    # Add pure number patterns
    number_patterns = [
        "69696969", "42424242", "66666666", "77777777", "88888888", "99999999",
        "12345678", "87654321", "11111111", "22222222", "33333333", "44444444",
        "55555555", "13371337", "42694269", "69426942", "11223344", "44332211",
        "12121212", "21212121", "13131313", "31313131", "14141414", "41414141",
        "69696942", "42424269", "69694242", "42426969", "13376969", "69691337",
        "42421337", "13374242", "66669999", "99996666", "77778888", "88887777",
        "12344321", "43211234", "11119999", "99991111", "66664242", "42426666",
        "69997777", "77776999", "88884242", "42428888", "99994242", "42429999"
    ]
    
    for pattern in number_patterns:
        if len(pattern) == 8 and all(c in VALID_CHARS for c in pattern):
            all_seeds.add(pattern)
    
    # Two word combinations (if they fit in 8 chars)
    for word1 in words[:50]:  # Limit to avoid explosion
        for word2 in words[:50]:
            if len(word1) + len(word2) <= 8:
                combined = word1 + word2
                if len(combined) < 8:
                    all_seeds.update(generate_padded_combinations(combined))
                elif len(combined) == 8:
                    all_seeds.add(combined)
    
    # Word + popular number combinations
    for word in words:
        for num in ["69", "42", "666", "777", "888", "999", "123", "321", "111"]:
            if len(word) + len(num) <= 8:
                # Try different arrangements
                all_seeds.add((word + num + "1" * (8 - len(word) - len(num)))[:8])
                all_seeds.add((num + word + "1" * (8 - len(word) - len(num)))[:8])
                all_seeds.add((word + num + "X" * (8 - len(word) - len(num)))[:8])
                all_seeds.add((num + word + "Z" * (8 - len(word) - len(num)))[:8])
    
    # Filter to ensure all are exactly 8 chars and valid
    valid_seeds = [s for s in all_seeds if len(s) == 8 and all(c in VALID_CHARS for c in s)]
    
    # Sort and write
    valid_seeds.sort()
    with open(output_file, 'w') as f:
        f.write('\n'.join(valid_seeds))
    
    print(f"Generated {len(valid_seeds)} {category} seeds")
    return len(valid_seeds)

# Generate all wordlists
if __name__ == "__main__":
    import os
    
    wordlist_dir = "X:/Motely/WordLists"
    
    total = 0
    total += generate_word_combinations(COOL_WORDS, os.path.join(wordlist_dir, "cool.txt"), "cool")
    total += generate_word_combinations(LOL_WORDS, os.path.join(wordlist_dir, "lol.txt"), "lol")
    total += generate_word_combinations(GROSS_WORDS, os.path.join(wordlist_dir, "gross.txt"), "gross")
    total += generate_word_combinations(NSFW_WORDS, os.path.join(wordlist_dir, "nsfw.txt"), "nsfw")
    
    print(f"\nTotal seeds generated: {total:,}")
    print("All seeds are exactly 8 characters using only: 123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ")
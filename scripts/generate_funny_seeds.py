#!/usr/bin/env python3
"""
Generate funny/gross/NSFW/cool/13375P33K seeds for Balatro.
Seeds must be 1-8 characters, using dictionary: 123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ (no 0, no spaces)
"""

import random
import re
from pathlib import Path

# Balatro seed dictionary (35 chars, no 0)
SEED_DICT = "123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"
SEED_DICT_SET = set(SEED_DICT)

# Word lists for different categories
FUNNY_WORDS = [
    "LOL", "HAHA", "ROFL", "LMAO", "KEK", "POG", "MEGA", "EPIC", "GIGA", "CHAD",
    "BASED", "CRINGE", "SUS", "NOOB", "PRO", "GOD", "OP", "BRO", "DUDE", "YOLO",
    "WOW", "OMG", "WTF", "BRUH", "YEET", "BOOM", "POW", "ZAP", "BAM", "KAPOW",
    "FUNNY", "SILLY", "GOOFY", "WACKY", "ZANY", "WILD", "CRAZY", "NUTS", "BONKERS"
]

GROSS_WORDS = [
    "EWW", "YUCK", "ICK", "BLEH", "PUKE", "BARF", "GAG", "GROSS", "NASTY", "DISGUST",
    "SLIME", "GOOP", "MUCK", "GRIME", "FILTH", "DIRT", "MUD", "OOZE", "SLUDGE", "GUNK",
    "STINK", "STANK", "FUNK", "ROT", "DECAY", "MOLD", "RUST", "CRUD", "SCUM", "TRASH"
]

NSFW_WORDS = [
    "FUCK", "SHIT", "DAMN", "HELL", "ASS", "BUTT", "BOOB", "DICK", "COCK", "PUSSY",
    "CUNT", "TITS", "ASS", "FART", "POOP", "PISS", "CUM", "SEX", "ORGASM", "MASTURBATE"
]

COOL_WORDS = [
    "COOL", "RAD", "SICK", "DOPE", "LIT", "FIRE", "ICE", "FROST", "CHILL", "FRESH",
    "SWAG", "STYLE", "CLASS", "ELITE", "LEGEND", "HERO", "ACE", "BOSS", "KING", "QUEEN",
    "STAR", "SHINE", "GLOW", "SPARK", "FLASH", "BLAZE", "STORM", "THUNDER", "LIGHTNING", "NOVA"
]

LEETSPEAK_PATTERNS = [
    ("A", "4"), ("E", "3"), ("I", "1"), ("O", "0"), ("S", "5"), ("T", "7"),
    ("L", "1"), ("Z", "2"), ("G", "9"), ("B", "8")
]

# Seed Gods of Discord
SEED_GODS = {
    "PIFREAK": ["PIFREAK", "PIE", "PI314", "PITFREK", "PIFREK", "314", "PI3", "PIF"],
    "TACO": ["TACO", "TACOS", "TACOMAN", "TACOGOD", "TACOLORD"],
    "FRENCHY": ["FRENCHY", "FRENCH", "FRENCHIE", "FRENCHMAN"],
    "COBMASTER": ["COBMASTER", "COB", "COBMAN", "COBLORD"],
    "LOLAJEAN": ["LOLAJEAN", "LOLA", "LOLAJ", "LOLAJ67"]
}

def is_valid_balatro_seed(seed: str) -> bool:
    """Check if seed is valid Balatro seed (1-8 chars, valid dictionary, no 0)"""
    if not seed or len(seed) > 8:
        return False
    if "0" in seed:  # Zero is invalid
        return False
    if " " in seed:  # No spaces
        return False
    return all(c in SEED_DICT_SET for c in seed)

def generate_seed_with_word(word: str, min_length: int = 1, max_length: int = 8) -> str:
    """Generate a valid Balatro seed containing the word, padded/combined with random chars"""
    word_upper = word.upper()
    
    # Try to fit word directly
    if len(word_upper) <= max_length and is_valid_balatro_seed(word_upper):
        return word_upper
    
    # Try to create seed with word + random padding
    if len(word_upper) < max_length:
        padding_needed = random.randint(0, max_length - len(word_upper))
        # Try prefix
        if random.choice([True, False]):
            prefix = ''.join(random.choices(SEED_DICT, k=padding_needed))
            seed = prefix + word_upper
            if is_valid_balatro_seed(seed):
                return seed
        # Try suffix
        suffix = ''.join(random.choices(SEED_DICT, k=padding_needed))
        seed = word_upper + suffix
        if is_valid_balatro_seed(seed):
            return seed
        # Try both
        if padding_needed >= 2:
            prefix_len = random.randint(1, padding_needed - 1)
            prefix = ''.join(random.choices(SEED_DICT, k=prefix_len))
            suffix = ''.join(random.choices(SEED_DICT, k=padding_needed - prefix_len))
            seed = prefix + word_upper + suffix
            if is_valid_balatro_seed(seed):
                return seed
    
    # Fallback: random seed
    length = random.randint(min_length, max_length)
    return ''.join(random.choices(SEED_DICT, k=length))

def generate_leetseed(word: str) -> str:
    """Convert word to leetspeak and generate valid seed"""
    leet_word = word.upper()
    for char, num in LEETSPEAK_PATTERNS:
        if random.random() < 0.7:  # 70% chance to leet-ify each char
            leet_word = leet_word.replace(char, num)
    
    # Ensure valid (no 0, no invalid chars)
    leet_word = ''.join(c if c in SEED_DICT_SET else random.choice(SEED_DICT) for c in leet_word)
    
    if len(leet_word) <= 8 and is_valid_balatro_seed(leet_word):
        return leet_word
    
    # Pad if needed
    return generate_seed_with_word(leet_word)

def generate_seeds_category(category: str, count: int, target_file: str):
    """Generate seeds for a specific category"""
    print(f"Generating {count:,} {category} seeds...")
    
    seeds = set()  # Use set to avoid duplicates
    words = []
    
    if category == "FUNNY":
        words = FUNNY_WORDS
    elif category == "GROSS":
        words = GROSS_WORDS
    elif category == "NSFW":
        words = NSFW_WORDS
    elif category == "COOL":
        words = COOL_WORDS
    elif category == "13375P33K":
        words = FUNNY_WORDS + COOL_WORDS  # Mix for leetspeak
    
    # Generate seeds with words
    attempts = 0
    max_attempts = count * 10
    
    while len(seeds) < count and attempts < max_attempts:
        attempts += 1
        
        if category == "13375P33K":
            # Leetspeak variants
            word = random.choice(words)
            seed = generate_leetseed(word)
        else:
            word = random.choice(words)
            seed = generate_seed_with_word(word)
        
        if seed and is_valid_balatro_seed(seed):
            seeds.add(seed)
        
        # Also generate some random seeds that might contain word fragments
        if random.random() < 0.3:  # 30% chance
            length = random.randint(1, 8)
            seed = ''.join(random.choices(SEED_DICT, k=length))
            if is_valid_balatro_seed(seed):
                seeds.add(seed)
    
    # Fill remaining with random valid seeds
    while len(seeds) < count:
        length = random.randint(1, 8)
        seed = ''.join(random.choices(SEED_DICT, k=length))
        if is_valid_balatro_seed(seed):
            seeds.add(seed)
    
    # Write to file
    seeds_list = sorted(list(seeds))[:count]  # Sort and limit
    output_path = Path("SeedSources") / target_file
    output_path.parent.mkdir(parents=True, exist_ok=True)
    
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("seed\n")  # CSV header
        for seed in seeds_list:
            f.write(f"{seed}\n")
    
    print(f"✅ Generated {len(seeds_list):,} {category} seeds -> {output_path}")

def main():
    """Generate all funny seed categories"""
    print("🎲 Generating Funny Seeds for Balatro!")
    print("=" * 60)
    
    # Generate 1 million seeds per category (adjust count as needed)
    count = 1_000_000
    
    generate_seeds_category("FUNNY", count, "_FunnySeeds__FUNNY.csv")
    generate_seeds_category("GROSS", count, "_FunnySeeds__GROSS.csv")
    generate_seeds_category("NSFW", count, "_FunnySeeds__NSFW.csv")
    generate_seeds_category("COOL", count, "_FunnySeeds__COOL.csv")
    generate_seeds_category("13375P33K", count, "_FunnySeeds__13375P33K.csv")
    
    print("=" * 60)
    print("✅ All funny seeds generated!")

if __name__ == "__main__":
    main()

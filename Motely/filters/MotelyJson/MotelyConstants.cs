namespace Motely.Filters;

/// <summary>
/// Constants for the MotelyJson filter system to eliminate magic numbers
/// </summary>
public static class MotelyConstants
{
    // Shop slot limits
    public const int ANTE_1_BASE_SHOP_SLOTS = 2;
    public const int ANTE_2_BASE_SHOP_SLOTS = 3;
    public const int ANTE_1_MAX_SHOP_SLOTS = 4;  // 2 base + 2 consumable slots
    public const int ANTE_2_MAX_SHOP_SLOTS = 6;  // 3 base + 3 consumable slots (includes potential voucher slots)
    public const int MAX_SHOP_SLOTS_TO_CHECK = 16; // Generous max for extended checks
    
    // Pack limits
    public const int ANTE_1_MAX_PACKS = 4;
    public const int ANTE_2_MAX_PACKS = 6;
    public const int DEFAULT_PACK_CHECK_COUNT = 4;
    
    // Pack sizes
    public const int STANDARD_PACK_SIZE = 2;
    public const int JUMBO_PACK_SIZE = 3;
    public const int MEGA_PACK_SIZE = 5;
    
    // Buffoon pack sizes
    public const int BUFFOON_PACK_SIZE = 2; // 2 jokers
    public const int BUFFOON_MEGA_PACK_SIZE = 3; // 3 jokers in mega
    
    // Scoring
    public const int MAX_SHOULD_CLAUSES = 32;
    public const int MAX_REROLL_ATTEMPTS = 5; // For duplicate legendary jokers
    
    // Performance
    public const int VECTOR_SIZE = 8; // Vector512 has 8 lanes for double
    
    // Default antes
    public static readonly int[] DEFAULT_ANTES = [1, 2, 3, 4, 5, 6, 7, 8];
}

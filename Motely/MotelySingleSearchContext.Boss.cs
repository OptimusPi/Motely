using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelySingleBossStream
{
    public MotelySinglePrngStream BossPrngStream;
    public Dictionary<MotelyBossBlind, int> BossesUsed;
    public int CurrentAnte;

    public MotelySingleBossStream(MotelySinglePrngStream bossPrngStream, int ante)
    {
        BossPrngStream = bossPrngStream;
        CurrentAnte = ante;
        // Initialize bosses_used tracking like Balatro does
        BossesUsed = new Dictionary<MotelyBossBlind, int>();

        // Initialize all bosses with 0 uses
        int i = 0;
        foreach (var MotelyBossBlind in Enum.GetValues(typeof(MotelyBossBlind)).Cast<MotelyBossBlind>())
        {
            BossesUsed[MotelyBossBlind] = i++ < 2 ? 1 : 0;
        }
    }
}

ref partial struct MotelySingleSearchContext
{
    // Showdown bosses (finishers) that appear only at ante % 8 == 0
    private static readonly HashSet<MotelyBossBlind> ShowdownBosses = new HashSet<MotelyBossBlind>
    {
        MotelyBossBlind.AmberAcorn,
        MotelyBossBlind.CeruleanBell,
        MotelyBossBlind.CrimsonHeart,
        MotelyBossBlind.VerdantLeaf,
        MotelyBossBlind.VioletVessel
    };

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleBossStream CreateBossStream(int ante = 1)
    {
        // Boss RNG uses "boss" as the key in Balatro
        var bossPrng = CreatePrngStream("boss");
        return new MotelySingleBossStream(bossPrng, ante);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetNextBoss(ref MotelySingleBossStream bossStream)
    {
        var ante = bossStream.CurrentAnte;
        var eligibleBosses = new Dictionary<MotelyBossBlind, bool>();
        foreach (var MotelyBossBlind in Enum.GetValues(typeof(MotelyBossBlind)).Cast<MotelyBossBlind>())
        {
            var (minAnte, maxAnte, isShowdown) = GetBossMetadata(MotelyBossBlind);

            if (!isShowdown)
            {
                // Regular bosses: min <= max(1,ante) AND (max(1,ante) % win_ante != 0 OR ante < 2)
                if (minAnte <= Math.Max(1, ante) && 
                    (Math.Max(1, ante) % 8 != 0 || ante < 2))
                {
                    eligibleBosses[MotelyBossBlind] = true;
                }
            }
            else
            {
                // Showdown bosses: ante % win_ante == 0 AND ante >= 2
                if (ante % 8 == 0 && ante >= 2)
                {
                    eligibleBosses[MotelyBossBlind] = true;
                }
            }
        }
        
        // Step 3: Remove banned bosses (TODO: Implement banned keys system)
        
        // Step 4: Apply usage-based fairness (EXACT BALATRO LOGIC lines 2363-2378)
        var minUsage = 100;
        
        // Replace boolean true with actual usage counts (line 2366: eligible_bosses[k] = v)
        foreach (var boss in eligibleBosses.Keys.ToList())
        {
            if (eligibleBosses[boss]) // Only process bosses that were eligible
            {
                var usage = bossStream.BossesUsed[boss];
                // This is the key difference - we're now tracking counts, not booleans
                if (usage <= minUsage)
                {
                    minUsage = usage;
                }
            }
        }
        
        // Remove bosses with usage > minimum (lines 2372-2378)
        foreach (var boss in eligibleBosses.Keys.ToList())
        {
            if (eligibleBosses[boss]) // Only check eligible bosses
            {
                var usage = bossStream.BossesUsed[boss];
                if (usage > minUsage)
                {
                    eligibleBosses.Remove(boss); // Remove high-usage bosses
                }
                // Keep bosses with minimum usage
            }
        }

        // Step 5: Use BossOrder array sequence (matches Balatro P_BLINDS order exactly)
        var finalBosses = eligibleBosses.Keys.OrderBy(boss => Array.IndexOf(Enum.GetValues(typeof(MotelyBossBlind)), boss)).ToList();

        // DEBUG: Show eligible bosses with their usage counts
        Console.WriteLine($"[DEBUG BOSS] Ante {ante}: All eligible bosses with usage:");
        foreach (var boss in finalBosses)
        {
            Console.WriteLine($"  {boss}: usage = {bossStream.BossesUsed[boss]}");
        }
        Console.WriteLine($"[DEBUG BOSS] Min usage: {minUsage}, Final count: {finalBosses.Count}");
        
        // Step 6: Select using Balatro's exact random logic
        int selectedIndex = GetNextRandomInt(ref bossStream.BossPrngStream, 0, finalBosses.Count - 1);
        var selectedBoss = finalBosses[selectedIndex];
        
        Console.WriteLine($"[DEBUG BOSS] Selected index {selectedIndex} out of {finalBosses.Count}, boss: {selectedBoss}");

        // Step 7: Update usage tracking (CRITICAL for fairness)
        bossStream.BossesUsed[selectedBoss]++;

        // Increment ante for next call
        bossStream.CurrentAnte++;

        return selectedBoss;
    }
    
    // Get boss metadata (min/max ante, showdown flag) from Balatro P_BLINDS data
    private static (int min, int max, bool showdown) GetBossMetadata(MotelyBossBlind boss)
    {
        return boss switch
        {
            // Regular bosses (from game.lua P_BLINDS)
            MotelyBossBlind.TheHook => (1, 10, false),
            MotelyBossBlind.TheClub => (1, 10, false),
            MotelyBossBlind.ThePsychic => (1, 10, false),
            MotelyBossBlind.TheGoad => (1, 10, false),
            MotelyBossBlind.TheHead => (1, 10, false),
            MotelyBossBlind.ThePillar => (1, 10, false),
            MotelyBossBlind.TheWindow => (1, 10, false),
            MotelyBossBlind.TheMouth => (2, 10, false),
            MotelyBossBlind.TheFish => (2, 10, false),
            MotelyBossBlind.TheWall => (2, 10, false),
            MotelyBossBlind.TheHouse => (2, 10, false),
            MotelyBossBlind.TheWater => (2, 10, false),
            MotelyBossBlind.TheFlint => (2, 10, false),
            MotelyBossBlind.TheNeedle => (2, 10, false),
            MotelyBossBlind.TheMark => (2, 10, false),
            MotelyBossBlind.TheTooth => (3, 10, false),
            MotelyBossBlind.TheEye => (3, 10, false),
            MotelyBossBlind.ThePlant => (4, 10, false),
            MotelyBossBlind.TheSerpent => (5, 10, false),
            MotelyBossBlind.TheOx => (6, 10, false),
            
            // Showdown bosses (finale)
            MotelyBossBlind.AmberAcorn => (10, 10, true),
            MotelyBossBlind.CeruleanBell => (10, 10, true),
            MotelyBossBlind.CrimsonHeart => (10, 10, true),
            MotelyBossBlind.VerdantLeaf => (10, 10, true),
            MotelyBossBlind.VioletVessel => (10, 10, true),
            
            _ => (1, 10, false) // Default fallback
        };
    }
    
    // Get boss order property from Balatro P_BLINDS data
    private static int GetBossOrder(MotelyBossBlind boss)
    {
        return boss switch
        {
            // Order values from Balatro P_BLINDS
            MotelyBossBlind.TheHook => 3,
            MotelyBossBlind.TheOx => 4,
            MotelyBossBlind.TheHouse => 5,
            MotelyBossBlind.TheWall => 6,
            MotelyBossBlind.TheWheel => 7,
            MotelyBossBlind.TheArm => 8,
            MotelyBossBlind.TheClub => 9,
            MotelyBossBlind.TheFish => 10,
            MotelyBossBlind.ThePsychic => 11,
            MotelyBossBlind.TheGoad => 12,
            MotelyBossBlind.TheWater => 13,
            MotelyBossBlind.TheWindow => 14,
            MotelyBossBlind.TheManacle => 15,
            MotelyBossBlind.TheEye => 16,
            MotelyBossBlind.TheMouth => 17,
            MotelyBossBlind.ThePlant => 18,
            MotelyBossBlind.TheSerpent => 19,
            MotelyBossBlind.ThePillar => 20,
            MotelyBossBlind.TheNeedle => 21,
            MotelyBossBlind.TheHead => 22,
            MotelyBossBlind.TheTooth => 23,
            MotelyBossBlind.TheFlint => 24,
            MotelyBossBlind.TheMark => 25,
            MotelyBossBlind.AmberAcorn => 26,
            MotelyBossBlind.VerdantLeaf => 27,
            MotelyBossBlind.VioletVessel => 28,
            MotelyBossBlind.CrimsonHeart => 29,
            MotelyBossBlind.CeruleanBell => 30,
            _ => 999 // Unknown bosses go last
        };
    }

    // Simple method for getting boss for a specific ante without tracking state
    // This is what's currently used in MotelyJsonSeedScoreDesc.cs
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetBossForAnte(int ante)
    {
        // Create a fresh boss stream for each call to ensure consistency
        var bossStream = CreateBossStream(1);

        // Get bosses for each ante up to the requested one
        // This simulates the game progression
        MotelyBossBlind boss = MotelyBossBlind.TheArm;
        for (int i = 1; i <= ante; i++)
        {
            bossStream.CurrentAnte = i;
            boss = GetNextBoss(ref bossStream);
        }

        return boss;
    }
}
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq; // For OrderBy/Where/ToList

namespace Motely;

public ref struct MotelySingleBossStream
{
    public MotelySinglePrngStream PrngStream;
    public MotelySingleResampleStream ResampleStream;
    public HashSet<MotelyBossBlind> UsedBosses;
    
    public MotelySingleBossStream(MotelySinglePrngStream prngStream, MotelySingleResampleStream resampleStream, HashSet<MotelyBossBlind> usedBosses)
    {
        PrngStream = prngStream;
        ResampleStream = resampleStream;
        UsedBosses = usedBosses;
    }
}

ref partial struct MotelySingleSearchContext
{
    // Boss ordering from OpenCL BOSSES array (28 bosses total, excluding Small/Big blinds)
    // In OpenCL, these have large enum values like 301, 302, etc. but we use 0-27
    private static readonly MotelyBossBlind[] BOSSES = new[]
    {
        MotelyBossBlind.TheArm,
        MotelyBossBlind.TheClub,
        MotelyBossBlind.TheEye,
        MotelyBossBlind.AmberAcorn,
        MotelyBossBlind.CeruleanBell,
        MotelyBossBlind.CrimsonHeart,
        MotelyBossBlind.VerdantLeaf,
        MotelyBossBlind.VioletVessel,
        MotelyBossBlind.TheFish,
        MotelyBossBlind.TheFlint,
        MotelyBossBlind.TheGoad,
        MotelyBossBlind.TheHead,
        MotelyBossBlind.TheHook,
        MotelyBossBlind.TheHouse,
        MotelyBossBlind.TheManacle,
        MotelyBossBlind.TheMark,
        MotelyBossBlind.TheMouth,
        MotelyBossBlind.TheNeedle,
        MotelyBossBlind.TheOx,
        MotelyBossBlind.ThePillar,
        MotelyBossBlind.ThePlant,
        MotelyBossBlind.ThePsychic,
        MotelyBossBlind.TheSerpent,
        MotelyBossBlind.TheTooth,
        MotelyBossBlind.TheWall,
        MotelyBossBlind.TheWater,
        MotelyBossBlind.TheWheel,
        MotelyBossBlind.TheWindow
    };

    // Helper method to check if a boss is a finisher type
    private static bool IsFinisherBoss(MotelyBossBlind boss)
    {
        // Finisher bosses have enum values 3-7
        int value = (int)boss;
        return value >= 3 && value <= 7;
    }

    // Get minimum ante requirement for a boss
    private static int GetBossMinAnte(MotelyBossBlind boss)
    {
        return boss switch
        {
            MotelyBossBlind.TheMouth or MotelyBossBlind.TheFish or MotelyBossBlind.TheWall or
            MotelyBossBlind.TheHouse or MotelyBossBlind.TheMark or MotelyBossBlind.TheWheel or
            MotelyBossBlind.TheArm or MotelyBossBlind.TheWater or MotelyBossBlind.TheNeedle or
            MotelyBossBlind.TheFlint => 2,
            
            MotelyBossBlind.TheTooth or MotelyBossBlind.TheEye => 3,
            MotelyBossBlind.ThePlant => 4,
            MotelyBossBlind.TheSerpent => 5,
            MotelyBossBlind.TheOx => 6,
            _ => 1
        };
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleBossStream CreateBossStream(ref MotelyRunState runState, int ante)
    {
        // Create resample stream for handling locked bosses
        var resampleStream = CreateResampleStream(MotelyPrngKeys.Boss, false);
        
        return new MotelySingleBossStream(runState.BossPrngStream, resampleStream, runState.UsedBosses);
    }


#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetNextBoss(ref MotelySingleBossStream bossStream, int ante)
    {
        bool isFinisherAnte = (ante % 8 == 0);
        
        // Build available boss pool - ONLY including bosses that meet ALL requirements
        List<MotelyBossBlind> bossPool = new();
        
        for (int i = 0; i < BOSSES.Length; i++)
        {
            var boss = BOSSES[i];
            
            // Check all requirements
            bool isFinisher = IsFinisherBoss(boss);
            bool correctType = (isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher);
            bool meetsAnteRequirement = ante >= GetBossMinAnte(boss);
            bool notAlreadyUsed = !bossStream.UsedBosses.Contains(boss);
            
            if (correctType && meetsAnteRequirement && notAlreadyUsed)
            {
                bossPool.Add(boss);
            }
        }
        
        // If pool is empty, reset the used bosses of this type
        if (bossPool.Count == 0)
        {
            // Clear only the bosses of the current type
            bossStream.UsedBosses.RemoveWhere(b => isFinisherAnte ? IsFinisherBoss(b) : !IsFinisherBoss(b));
            
            // Rebuild pool
            for (int i = 0; i < BOSSES.Length; i++)
            {
                var boss = BOSSES[i];
                
                bool isFinisher = IsFinisherBoss(boss);
                bool correctType = (isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher);
                bool meetsAnteRequirement = ante >= GetBossMinAnte(boss);
                
                if (correctType && meetsAnteRequirement)
                {
                    bossPool.Add(boss);
                }
            }
        }
        
        if (bossPool.Count == 0)
        {
            throw new InvalidOperationException($"No valid bosses available for ante {ante}");
        }
        
        // Sort pool by Lua key for consistent ordering (matching OpenCL behavior)
        bossPool.Sort((a, b) => GetLuaKey(a).CompareTo(GetLuaKey(b)));
        
        // Select boss from pool using PRNG
        int bossIndex = GetNextRandomInt(ref bossStream.PrngStream, 0, bossPool.Count);
        MotelyBossBlind selectedBoss = bossPool[bossIndex];
        
        // Mark boss as used
        bossStream.UsedBosses.Add(selectedBoss);
        
        return selectedBoss;
    }

    private static string GetLuaKey(MotelyBossBlind boss)
    {
        return boss switch
        {
            MotelyBossBlind.TheArm => "bl_arm",
            MotelyBossBlind.TheClub => "bl_club",
            MotelyBossBlind.TheEye => "bl_eye",
            MotelyBossBlind.TheFish => "bl_fish",
            MotelyBossBlind.TheFlint => "bl_flint",
            MotelyBossBlind.TheGoad => "bl_goad",
            MotelyBossBlind.TheHead => "bl_head",
            MotelyBossBlind.TheHook => "bl_hook",
            MotelyBossBlind.TheHouse => "bl_house",
            MotelyBossBlind.TheManacle => "bl_manacle",
            MotelyBossBlind.TheMark => "bl_mark",
            MotelyBossBlind.TheMouth => "bl_mouth",
            MotelyBossBlind.TheNeedle => "bl_needle",
            MotelyBossBlind.TheOx => "bl_ox",
            MotelyBossBlind.ThePillar => "bl_pillar",
            MotelyBossBlind.ThePlant => "bl_plant",
            MotelyBossBlind.ThePsychic => "bl_psychic",
            MotelyBossBlind.TheSerpent => "bl_serpent",
            MotelyBossBlind.TheTooth => "bl_tooth",
            MotelyBossBlind.TheWall => "bl_wall",
            MotelyBossBlind.TheWater => "bl_water",
            MotelyBossBlind.TheWheel => "bl_wheel",
            MotelyBossBlind.TheWindow => "bl_window",
            MotelyBossBlind.AmberAcorn => "bl_final_acorn",
            MotelyBossBlind.CeruleanBell => "bl_final_bell",
            MotelyBossBlind.CrimsonHeart => "bl_final_heart",
            MotelyBossBlind.VerdantLeaf => "bl_final_leaf",
            MotelyBossBlind.VioletVessel => "bl_final_vessel",
            _ => "bl_unknown"
        };
    }

    public MotelyBossBlind GetBossForAnte(int ante, ref MotelyRunState runState)
    {
        // Initialize if needed
        if (runState.UsedBosses == null)
        {
            runState.UsedBosses = new HashSet<MotelyBossBlind>();
            runState.LastProcessedBossAnte = 0;
            runState.BossPrngStream = CreatePrngStream(MotelyPrngKeys.Boss);
        }
        
        // Check if we need to catch up to the requested ante
        if (ante <= runState.LastProcessedBossAnte)
        {
            throw new InvalidOperationException($"Cannot get boss for ante {ante} - already processed up to ante {runState.LastProcessedBossAnte}. Bosses must be generated sequentially.");
        }

        // Process antes sequentially to maintain state
        MotelyBossBlind selectedBoss = MotelyBossBlind.TheHook;
        while (runState.LastProcessedBossAnte < ante)
        {
            runState.LastProcessedBossAnte++;
            var bossStream = CreateBossStream(ref runState, runState.LastProcessedBossAnte);
            selectedBoss = GetNextBoss(ref bossStream, runState.LastProcessedBossAnte);
            // Update persistent PRNG state
            runState.BossPrngStream = bossStream.PrngStream;
        }

        return selectedBoss;
    }
}
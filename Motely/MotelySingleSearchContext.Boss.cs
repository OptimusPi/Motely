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
        foreach (var boss in BossOrder)
        {
            BossesUsed[boss] = 0;
        }
    }

    // Boss order from Balatro P_BLINDS - sorted by key names for consistency
    private static readonly MotelyBossBlind[] BossOrder = [
        MotelyBossBlind.AmberAcorn,
        MotelyBossBlind.CeruleanBell,
        MotelyBossBlind.CrimsonHeart,
        MotelyBossBlind.TheArm,
        MotelyBossBlind.TheClub,
        MotelyBossBlind.TheEye,
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
        MotelyBossBlind.TheWindow,
        MotelyBossBlind.VerdantLeaf,
        MotelyBossBlind.VioletVessel
    ];
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
        var isShowdownAnte = (ante % 8 == 0);

        // Build list of eligible bosses based on ante type
        var eligibleBosses = new List<MotelyBossBlind>();

        if (isShowdownAnte)
        {
            // Showdown bosses for ante 8, 16, etc.
            eligibleBosses.AddRange(ShowdownBosses);
        }
        else
        {
            // Regular bosses for non-showdown antes
            foreach (var boss in bossStream.BossesUsed.Keys)
            {
                if (!ShowdownBosses.Contains(boss))
                {
                    eligibleBosses.Add(boss);
                }
            }
        }

        // Sort bosses alphabetically for consistent ordering
        eligibleBosses.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

        // Simple random selection without usage tracking (like Immolate might do)
        int selectedIndex = GetNextRandomInt(ref bossStream.BossPrngStream, 0, eligibleBosses.Count - 1);
        var selectedBoss = eligibleBosses[selectedIndex];

        // Increment ante for next call
        bossStream.CurrentAnte++;

        return selectedBoss;
    }

    // Simple method for getting boss for a specific ante without tracking state
    // This is what's currently used in MotelyJsonFinalTallyScoresDescDesc.cs
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
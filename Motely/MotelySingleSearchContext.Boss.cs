using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public ref struct MotelySingleBossStream
{
    public MotelySinglePrngStream BossPrngStream;
    public bool[] LockedBosses;
    public int CurrentAnte;
    
    public MotelySingleBossStream(MotelySinglePrngStream bossPrngStream, int ante)
    {
        BossPrngStream = bossPrngStream;
        CurrentAnte = ante;
        // Initialize locked bosses array - 28 bosses total
        LockedBosses = new bool[28];
    }
}

ref partial struct MotelySingleSearchContext
{
    // Boss order from Immolate BOSSES array
    private static readonly MotelyBossBlind[] BossOrder = [
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
    ];
    
    // Finisher bosses start at index 3 (AmberAcorn) in the BossOrder array
    private const int FinisherBossStartIndex = 3;
    private const int FinisherBossEndIndex = 8; // Exclusive (5 finisher bosses total)

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleBossStream CreateBossStream(int ante = 1)
    {
        // Boss RNG uses "boss" as the key in Immolate
        var bossPrng = CreatePrngStream("boss");
        return new MotelySingleBossStream(bossPrng, ante);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetNextBoss(ref MotelySingleBossStream bossStream)
    {
        var ante = bossStream.CurrentAnte;
        var isFinisherAnte = (ante % 8 == 0);
        
        // Build pool of available bosses
        var availableBosses = new System.Collections.Generic.List<int>();
        
        for (int i = 0; i < BossOrder.Length; i++)
        {
            // Skip locked bosses
            if (bossStream.LockedBosses[i])
                continue;
                
            // Check if boss is appropriate for this ante type
            bool isFinisherBoss = (i >= FinisherBossStartIndex && i < FinisherBossEndIndex);
            
            if (isFinisherAnte && isFinisherBoss)
            {
                availableBosses.Add(i);
            }
            else if (!isFinisherAnte && !isFinisherBoss)
            {
                availableBosses.Add(i);
            }
        }
        
        // If no bosses available, reset the appropriate pool
        if (availableBosses.Count == 0)
        {
            if (isFinisherAnte)
            {
                // Reset finisher boss pool
                for (int i = FinisherBossStartIndex; i < FinisherBossEndIndex; i++)
                {
                    bossStream.LockedBosses[i] = false;
                }
            }
            else
            {
                // Reset regular boss pool
                for (int i = 0; i < BossOrder.Length; i++)
                {
                    if (i < FinisherBossStartIndex || i >= FinisherBossEndIndex)
                    {
                        bossStream.LockedBosses[i] = false;
                    }
                }
            }
            
            // Rebuild available bosses after reset
            availableBosses.Clear();
            for (int i = 0; i < BossOrder.Length; i++)
            {
                if (bossStream.LockedBosses[i])
                    continue;
                    
                bool isFinisherBoss = (i >= FinisherBossStartIndex && i < FinisherBossEndIndex);
                
                if (isFinisherAnte && isFinisherBoss)
                {
                    availableBosses.Add(i);
                }
                else if (!isFinisherAnte && !isFinisherBoss)
                {
                    availableBosses.Add(i);
                }
            }
        }
        
        // Select a random boss from available pool
        int selectedIndex = GetNextRandomInt(ref bossStream.BossPrngStream, 0, availableBosses.Count - 1);
        int bossIndex = availableBosses[selectedIndex];
        
        // Lock the selected boss
        bossStream.LockedBosses[bossIndex] = true;
        
        // Increment ante for next call
        bossStream.CurrentAnte++;
        
        return BossOrder[bossIndex];
    }
    
    // Simple method for getting boss for a specific ante without tracking state
    // This is what's currently used in OuijaJsonFilterDesc.cs
#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetBossForAnte(int ante)
    {
        // Create a temporary boss stream
        var bossStream = CreateBossStream(ante);
        
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
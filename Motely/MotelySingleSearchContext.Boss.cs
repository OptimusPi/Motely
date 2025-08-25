using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq; // For OrderBy/Where/ToList

namespace Motely;

public ref struct MotelySingleBossStream
{
    public MotelySinglePrngStream PrngStream;
    public int CurrentAnte;
    public int[] Locked; // 0 = unlocked, 1 = locked, -1 = non-boss (small/big blinds)

    public MotelySingleBossStream(MotelySinglePrngStream prngStream, int ante)
    {
        PrngStream = prngStream;
        CurrentAnte = ante;
        var values = Enum.GetValues<MotelyBossBlind>();
        Locked = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            var b = values[i];
            Locked[i] = (b == MotelyBossBlind.SmallBlind || b == MotelyBossBlind.BigBlind) ? -1 : 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLocked(MotelyBossBlind boss) => Locked[(int)boss] == 1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Lock(MotelyBossBlind boss) => Locked[(int)boss] = 1;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unlock(MotelyBossBlind boss) { if (Locked[(int)boss] >= 0) Locked[(int)boss] = 0; }
}

ref partial struct MotelySingleSearchContext
{
    // Boss ordering from OpenCL BOSSES array (28 bosses total, excluding Small/Big blinds)
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
    
    // Index where finisher bosses begin in BOSSES array (matches B_F_BEGIN)
    private const int FINISHER_START_INDEX = 3; // AmberAcorn is at index 3

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleBossStream CreateBossStream(int ante = 1) => new MotelySingleBossStream(CreateBossPrngStream(), ante);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MotelySinglePrngStream CreateBossPrngStream()
    {
        // Balatro initial per-key state: pseudohash(key..seed)
        return new MotelySinglePrngStream(PseudoHash("boss" + GetSeed()));
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetNextBoss(ref MotelySingleBossStream bossStream)
    {
        var ante = bossStream.CurrentAnte;
        var bossPool = new List<MotelyBossBlind>();
        
        // Build pool following OpenCL logic exactly
        for (int i = 0; i < BOSSES.Length; i++)
        {
            var boss = BOSSES[i];
            if (!bossStream.IsLocked(boss))
            {
                bool isFinisher = i >= FINISHER_START_INDEX;
                bool isFinisherAnte = (ante % 8 == 0);
                
                // Match OpenCL condition: (ante % 8 == 0 && BOSSES[i] > B_F_BEGIN) || (ante % 8 != 0 && BOSSES[i] < B_F_BEGIN)
                if ((isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher))
                {
                    bossPool.Add(boss);
                }
            }
        }
        
        // If pool is empty, unlock appropriate bosses
        if (bossPool.Count == 0)
        {
            if (ante % 8 == 0)
            {
                // Unlock finisher bosses (from FINISHER_START_INDEX onward)
                for (int i = FINISHER_START_INDEX; i < BOSSES.Length; i++)
                {
                    bossStream.Unlock(BOSSES[i]);
                }
            }
            else
            {
                // Unlock regular bosses (before FINISHER_START_INDEX)
                for (int i = 0; i < FINISHER_START_INDEX; i++)
                {
                    bossStream.Unlock(BOSSES[i]);
                }
            }
            
            // Rebuild pool after unlocking
            for (int i = 0; i < BOSSES.Length; i++)
            {
                var boss = BOSSES[i];
                if (!bossStream.IsLocked(boss))
                {
                    bool isFinisher = i >= FINISHER_START_INDEX;
                    bool isFinisherAnte = (ante % 8 == 0);
                    
                    if ((isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher))
                    {
                        bossPool.Add(boss);
                    }
                }
            }
        }
        
        if (bossPool.Count == 0) throw new InvalidOperationException("Boss pool empty after unlock");
        
        // Get random boss using next pseudo seed
        double pseudoSeed = GetNextPseudoSeed(ref bossStream.PrngStream);
        double frac = pseudoSeed - Math.Floor(pseudoSeed);
        int idx = (int)(frac * bossPool.Count);
        if (idx >= bossPool.Count) idx = bossPool.Count - 1;
        if (idx < 0) idx = 0;
        
        var selected = bossPool[idx];
        bossStream.Lock(selected);
        
        // Debug output
        // Console.WriteLine($"[DEBUG BOSS] Ante {ante} Pool={bossPool.Count} Selected={selected}");
        
        bossStream.CurrentAnte++;
        return selected;
    }

    // Method for getting boss for a specific ante with run state tracking
    // Similar to how vouchers work - caller maintains the run state
    public MotelyBossBlind GetBossForAnte(int ante, ref MotelyRunState runState)
    {
        // Initialize boss state if needed (like Balatro init_game_object)
        if (runState.BossLocked == null)
        {
            var values = Enum.GetValues<MotelyBossBlind>();
            runState.BossLocked = new int[values.Length];
            runState.BossPrngStream = CreateBossPrngStream();
            runState.LastProcessedBossAnte = 0;
            for (int i = 0; i < values.Length; i++)
            {
                var b = values[i];
                runState.BossLocked[i] = (b == MotelyBossBlind.SmallBlind || b == MotelyBossBlind.BigBlind) ? -1 : 0;
            }
        }

        // Process antes sequentially to maintain state
        MotelyBossBlind selectedBoss = MotelyBossBlind.TheHook;
        while (runState.LastProcessedBossAnte < ante)
        {
            runState.LastProcessedBossAnte++;
            var tempStream = new MotelySingleBossStream(runState.BossPrngStream.Value, runState.LastProcessedBossAnte)
            {
                Locked = runState.BossLocked
            };
            selectedBoss = GetNextBoss(ref tempStream);
            runState.BossLocked = tempStream.Locked; // array mutated in place
            runState.BossPrngStream = tempStream.PrngStream;
        }

        return selectedBoss;
    }
}
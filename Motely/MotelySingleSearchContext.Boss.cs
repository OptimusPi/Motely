using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Linq; // For OrderBy/Where/ToList

namespace Motely;

public ref struct MotelySingleBossStream
{
    public MotelySinglePrngStream PrngStream;
    public int[] Locked; // 0 = unlocked, 1 = locked by ante requirement, 2 = locked because already selected, -1 = non-boss

    public MotelySingleBossStream(MotelySinglePrngStream prngStream, int[] locked)
    {
        PrngStream = prngStream;
        Locked = locked;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsLocked(MotelyBossBlind boss) => Locked[(int)boss] > 0; // Both 1 and 2 are considered locked
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Lock(MotelyBossBlind boss) => Locked[(int)boss] = 2; // Mark as selected/used
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unlock(MotelyBossBlind boss) { if (Locked[(int)boss] == 1) Locked[(int)boss] = 0; } // Only unlock ante-locked bosses, not selected ones
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

    // Index where finisher bosses begin in BOSSES array
    // In our new enum order: AmberAcorn=5, but in BOSSES array it's at index 3
    private const int FINISHER_START_INDEX = 3; // AmberAcorn is at index 3 in BOSSES array (also enum value 3)

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelySingleBossStream CreateBossStream(ref MotelyRunState runState, int ante)
    {
        // Initialize boss state if needed
        if (runState.BossLocked == null)
        {
            // Need array large enough for all boss indices including SmallBlind/BigBlind at 100/101
            runState.BossLocked = new int[102]; // Up to BigBlind = 101
            runState.LastProcessedBossAnte = 0;

            // Initialize all boss slots to unlocked (0)
            for (int i = 0; i < BOSSES.Length; i++)
            {
                runState.BossLocked[(int)BOSSES[i]] = 0;
            }

            // Mark SmallBlind and BigBlind as non-boss (-1)
            runState.BossLocked[(int)MotelyBossBlind.SmallBlind] = -1;
            runState.BossLocked[(int)MotelyBossBlind.BigBlind] = -1;

            // Lock bosses based on ante requirements (from OpenCL init_locks)
            // These are locked until ante 2
            runState.BossLocked[(int)MotelyBossBlind.TheMouth] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheFish] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheWall] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheHouse] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheMark] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheWheel] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheArm] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheWater] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheNeedle] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheFlint] = 1;

            // These are locked until ante 3
            runState.BossLocked[(int)MotelyBossBlind.TheTooth] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheEye] = 1;

            // Locked until ante 4
            runState.BossLocked[(int)MotelyBossBlind.ThePlant] = 1;

            // Locked until ante 5
            runState.BossLocked[(int)MotelyBossBlind.TheSerpent] = 1;

            // Locked until ante 6
            runState.BossLocked[(int)MotelyBossBlind.TheOx] = 1;

            // Initialize the persistent PRNG stream for boss generation
            // This stream must persist across all antes, matching Balatro's behavior
            runState.BossPrngStream = CreatePrngStream(MotelyPrngKeys.Boss);
        }

        // Return a boss stream using the persistent PRNG stream
        // Note: The caller must update both runState.BossPrngStream AND runState.BossLocked after use to maintain state
        return new MotelySingleBossStream(runState.BossPrngStream, runState.BossLocked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MotelySinglePrngStream CreateBossPrngStream(int ante)
    {
        // Use fixed key 'boss' matching game/OpenCL behavior (no ante concatenation)
        return CreatePrngStream(MotelyPrngKeys.Boss);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public MotelyBossBlind GetNextBoss(ref MotelySingleBossStream bossStream, int ante)
    {

        // Unlock bosses based on ante (from OpenCL init_unlocks)
        if (ante >= 2)
        {
            bossStream.Unlock(MotelyBossBlind.TheMouth);
            bossStream.Unlock(MotelyBossBlind.TheFish);
            bossStream.Unlock(MotelyBossBlind.TheWall);
            bossStream.Unlock(MotelyBossBlind.TheHouse);
            bossStream.Unlock(MotelyBossBlind.TheMark);
            bossStream.Unlock(MotelyBossBlind.TheWheel);
            bossStream.Unlock(MotelyBossBlind.TheArm);
            bossStream.Unlock(MotelyBossBlind.TheWater);
            bossStream.Unlock(MotelyBossBlind.TheNeedle);
            bossStream.Unlock(MotelyBossBlind.TheFlint);
        }
        if (ante >= 3)
        {
            bossStream.Unlock(MotelyBossBlind.TheTooth);
            bossStream.Unlock(MotelyBossBlind.TheEye);
        }
        if (ante >= 4)
        {
            bossStream.Unlock(MotelyBossBlind.ThePlant);
        }
        if (ante >= 5)
        {
            bossStream.Unlock(MotelyBossBlind.TheSerpent);
        }
        if (ante >= 6)
        {
            bossStream.Unlock(MotelyBossBlind.TheOx);
        }

        var bossPool = new List<MotelyBossBlind>();

        // Build pool following OpenCL logic exactly
        for (int i = 0; i < BOSSES.Length; i++)
        {
            var boss = BOSSES[i];
            if (!bossStream.IsLocked(boss))
            {
                // Finisher bosses are enum values 3-7 (AmberAcorn through VioletVessel)
                bool isFinisher = (int)boss >= 3 && (int)boss <= 7;
                bool isFinisherAnte = (ante % 8 == 0);

                // Match OpenCL condition: (ante % 8 == 0 && boss is finisher) || (ante % 8 != 0 && boss is not finisher)
                if ((isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher))
                {
                    bossPool.Add(boss);
                    // Console.WriteLine($"[DEBUG POOL] Added {boss} ({(int)boss}) to pool - isFinisher={isFinisher}, isFinisherAnte={isFinisherAnte}");
                }
            }
        }

        // If pool is empty, unlock appropriate bosses
        if (bossPool.Count == 0)
        {
            if (ante % 8 == 0)
            {
                // Unlock finisher bosses only
                bossStream.Unlock(MotelyBossBlind.AmberAcorn);
                bossStream.Unlock(MotelyBossBlind.CeruleanBell);
                bossStream.Unlock(MotelyBossBlind.CrimsonHeart);
                bossStream.Unlock(MotelyBossBlind.VerdantLeaf);
                bossStream.Unlock(MotelyBossBlind.VioletVessel);
            }
            else
            {
                // Unlock all regular bosses (non-finishers)
                for (int i = 0; i < BOSSES.Length; i++)
                {
                    var boss = BOSSES[i];
                    // Skip finisher bosses (enum values 3-7)
                    if ((int)boss < 3 || (int)boss > 7)
                    {
                        bossStream.Unlock(boss);
                    }
                }
            }

            // Rebuild pool after unlocking
            for (int i = 0; i < BOSSES.Length; i++)
            {
                var boss = BOSSES[i];
                if (!bossStream.IsLocked(boss))
                {
                    // Finisher bosses are enum values 3-7 (AmberAcorn through VioletVessel)
                    bool isFinisher = (int)boss >= 3 && (int)boss <= 7;
                    bool isFinisherAnte = (ante % 8 == 0);

                    if ((isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher))
                    {
                        bossPool.Add(boss);
                    }
                }
            }
        }

        if (bossPool.Count == 0) throw new InvalidOperationException("Boss pool empty after unlock");

        // Sort the pool alphabetically by Lua key name (bl_club, bl_head, etc.)
        // Map our enum to Lua keys for proper sorting
        var luaKeyMap = new Dictionary<MotelyBossBlind, string>
        {
            { MotelyBossBlind.TheArm, "bl_arm" },
            { MotelyBossBlind.TheClub, "bl_club" },
            { MotelyBossBlind.TheEye, "bl_eye" },
            { MotelyBossBlind.TheFish, "bl_fish" },
            { MotelyBossBlind.TheFlint, "bl_flint" },
            { MotelyBossBlind.TheGoad, "bl_goad" },
            { MotelyBossBlind.TheHead, "bl_head" },
            { MotelyBossBlind.TheHook, "bl_hook" },
            { MotelyBossBlind.TheHouse, "bl_house" },
            { MotelyBossBlind.TheManacle, "bl_manacle" },
            { MotelyBossBlind.TheMark, "bl_mark" },
            { MotelyBossBlind.TheMouth, "bl_mouth" },
            { MotelyBossBlind.TheNeedle, "bl_needle" },
            { MotelyBossBlind.TheOx, "bl_ox" },
            { MotelyBossBlind.ThePillar, "bl_pillar" },
            { MotelyBossBlind.ThePlant, "bl_plant" },
            { MotelyBossBlind.ThePsychic, "bl_psychic" },
            { MotelyBossBlind.TheSerpent, "bl_serpent" },
            { MotelyBossBlind.TheTooth, "bl_tooth" },
            { MotelyBossBlind.TheWall, "bl_wall" },
            { MotelyBossBlind.TheWater, "bl_water" },
            { MotelyBossBlind.TheWheel, "bl_wheel" },
            { MotelyBossBlind.TheWindow, "bl_window" },
            { MotelyBossBlind.AmberAcorn, "bl_final_acorn" },
            { MotelyBossBlind.CeruleanBell, "bl_final_bell" },
            { MotelyBossBlind.CrimsonHeart, "bl_final_heart" },
            { MotelyBossBlind.VerdantLeaf, "bl_final_leaf" },
            { MotelyBossBlind.VioletVessel, "bl_final_vessel" }
        };

        bossPool.Sort((a, b) => luaKeyMap[a].CompareTo(luaKeyMap[b]));

        // Use the continuous PRNG stream passed in, which maintains state across antes
        // This matches the OpenCL cache behavior where each call advances the state
        // In OpenCL: chosen_boss = boss_pool[l_randint(&(inst->rng), 0, num_available_bosses-1)];
        // l_randint generates an integer between min and max inclusive
        int idx = GetNextRandomInt(ref bossStream.PrngStream, 0, bossPool.Count);

        var selected = bossPool[idx];
        bossStream.Lock(selected);

        return selected;
    }

    // Method for getting boss for a specific ante with run state tracking
    // Similar to how vouchers work - caller maintains the run state
    public MotelyBossBlind GetBossForAnte(int ante, ref MotelyRunState runState)
    {
        // Initialize boss state if needed (first time this is called)
        if (runState.BossLocked == null)
        {
            // Need array large enough for all boss indices including SmallBlind/BigBlind at 100/101
            runState.BossLocked = new int[102]; // Up to BigBlind = 101
            runState.LastProcessedBossAnte = 0;

            // Initialize all boss slots to unlocked (0)
            for (int i = 0; i < BOSSES.Length; i++)
            {
                runState.BossLocked[(int)BOSSES[i]] = 0;
            }

            // Mark SmallBlind and BigBlind as non-boss (-1)
            runState.BossLocked[(int)MotelyBossBlind.SmallBlind] = -1;
            runState.BossLocked[(int)MotelyBossBlind.BigBlind] = -1;

            // Lock bosses based on ante requirements (from OpenCL init_locks)
            // These are locked until ante 2
            runState.BossLocked[(int)MotelyBossBlind.TheMouth] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheFish] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheWall] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheHouse] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheMark] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheWheel] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheArm] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheWater] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheNeedle] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheFlint] = 1;

            // These are locked until ante 3
            runState.BossLocked[(int)MotelyBossBlind.TheTooth] = 1;
            runState.BossLocked[(int)MotelyBossBlind.TheEye] = 1;

            // Locked until ante 4
            runState.BossLocked[(int)MotelyBossBlind.ThePlant] = 1;

            // Locked until ante 5
            runState.BossLocked[(int)MotelyBossBlind.TheSerpent] = 1;

            // Locked until ante 6
            runState.BossLocked[(int)MotelyBossBlind.TheOx] = 1;

            // Initialize the persistent PRNG stream for boss generation
            // This stream must persist across all antes, matching Balatro's behavior
            runState.BossPrngStream = CreatePrngStream(MotelyPrngKeys.Boss);
        }

        // Check if we need to catch up to the requested ante
        if (ante <= runState.LastProcessedBossAnte)
        {
            // Already processed this ante, return cached result would go here
            // But for now we'll just throw since we don't cache individual results
            throw new InvalidOperationException($"Cannot get boss for ante {ante} - already processed up to ante {runState.LastProcessedBossAnte}. Bosses must be generated sequentially.");
        }

        // Process antes sequentially to maintain state - bosses unlock at different antes and get locked after selection
        MotelyBossBlind selectedBoss = MotelyBossBlind.TheHook;
        while (runState.LastProcessedBossAnte < ante)
        {
            runState.LastProcessedBossAnte++;
            // CRITICAL FIX: Use the persistent PRNG stream, not a new one for each ante
            // The PRNG state must persist across all ante calls to match Balatro's behavior
            var bossStream = new MotelySingleBossStream(runState.BossPrngStream, runState.BossLocked);
            selectedBoss = GetNextBoss(ref bossStream, runState.LastProcessedBossAnte);
            // Update the persistent PRNG stream with the modified state
            runState.BossPrngStream = bossStream.PrngStream;
            // ALSO persist the locked state! Bosses that are selected get locked
            // This was the missing piece - we weren't persisting lock state changes
            runState.BossLocked = bossStream.Locked;
        }

        return selectedBoss;
    }
}
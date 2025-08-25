using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Motely;

public struct MotelyVectorBossStream
{
    public MotelyVectorPrngStream PrngStream;
    public int CurrentAnte;
    public Vector256<int>[] Locked; // Track lock status per boss per lane
    
    public MotelyVectorBossStream(MotelyVectorPrngStream prngStream, int ante)
    {
        PrngStream = prngStream;
        CurrentAnte = ante;
        var bossCount = Enum.GetValues<MotelyBossBlind>().Length;
        Locked = new Vector256<int>[bossCount];
        
        // Initialize: -1 for non-boss (small/big), 0 for unlocked bosses
        for (int i = 0; i < bossCount; i++)
        {
            var boss = (MotelyBossBlind)i;
            if (boss == MotelyBossBlind.SmallBlind || boss == MotelyBossBlind.BigBlind)
                Locked[i] = Vector256.Create(-1); // Non-boss marker
            else
                Locked[i] = Vector256<int>.Zero; // All lanes unlocked
        }
    }
}

ref partial struct MotelyVectorSearchContext
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
    
    public MotelyVectorBossStream CreateBossStream(int ante, bool isCached = false)
    {
        // Boss RNG uses "boss" as the key in Balatro (lowercase, no ante appended)
        return new MotelyVectorBossStream(CreatePrngStream("boss", isCached), ante);
    }

#if !DEBUG
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    public VectorEnum256<MotelyBossBlind> GetNextBoss(ref MotelyVectorBossStream stream)
    {
        var ante = stream.CurrentAnte;
        bool isFinisherAnte = (ante % 8 == 0);
        
        // Process each lane independently
        var results = new int[Vector256<int>.Count];
        
        for (int lane = 0; lane < Vector256<int>.Count; lane++)
        {
            var bossPool = new List<MotelyBossBlind>();
            
            // Build pool following OpenCL logic exactly
            for (int i = 0; i < BOSSES.Length; i++)
            {
                var boss = BOSSES[i];
                bool isLocked = stream.Locked[(int)boss][lane] == 1;
                
                if (!isLocked)
                {
                    bool isFinisher = i >= FINISHER_START_INDEX;
                    
                    // Match OpenCL condition
                    if ((isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher))
                    {
                        bossPool.Add(boss);
                    }
                }
            }
            
            // If pool is empty, unlock appropriate bosses
            if (bossPool.Count == 0)
            {
                if (isFinisherAnte)
                {
                    // Unlock finisher bosses
                    for (int i = FINISHER_START_INDEX; i < BOSSES.Length; i++)
                    {
                        stream.Locked[(int)BOSSES[i]] = stream.Locked[(int)BOSSES[i]].WithElement(lane, 0);
                    }
                }
                else
                {
                    // Unlock regular bosses
                    for (int i = 0; i < FINISHER_START_INDEX; i++)
                    {
                        stream.Locked[(int)BOSSES[i]] = stream.Locked[(int)BOSSES[i]].WithElement(lane, 0);
                    }
                }
                
                // Rebuild pool after unlocking
                for (int i = 0; i < BOSSES.Length; i++)
                {
                    var boss = BOSSES[i];
                    bool isLocked = stream.Locked[(int)boss][lane] == 1;
                    
                    if (!isLocked)
                    {
                        bool isFinisher = i >= FINISHER_START_INDEX;
                        
                        if ((isFinisherAnte && isFinisher) || (!isFinisherAnte && !isFinisher))
                        {
                            bossPool.Add(boss);
                        }
                    }
                }
            }
            
            // Select random boss from pool
            if (bossPool.Count > 0)
            {
                var randValue = GetNextRandom(ref stream.PrngStream)[lane];
                int idx = (int)(randValue * bossPool.Count);
                if (idx >= bossPool.Count) idx = bossPool.Count - 1;
                if (idx < 0) idx = 0;
                
                var selected = bossPool[idx];
                results[lane] = (int)selected;
                
                // Lock the selected boss
                stream.Locked[(int)selected] = stream.Locked[(int)selected].WithElement(lane, 1);
            }
            else
            {
                results[lane] = (int)MotelyBossBlind.TheArm; // Fallback to first boss
            }
        }
        
        stream.CurrentAnte++;
        return new VectorEnum256<MotelyBossBlind>(Vector256.Create(results));
    }
}
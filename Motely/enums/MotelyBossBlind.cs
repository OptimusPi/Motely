using System.Runtime.CompilerServices;

namespace Motely;

public enum MotelyBossBlindType
{
    Normal = 0 << MotelyCore.BossTypeOffset,
    Finisher = 1 << MotelyCore.BossTypeOffset,
}

internal enum MotelyBossBlindWithoutData
{
    AmberAcorn,
    CeruleanBell,
    CrimsonHeart,
    VerdantLeaf,
    VioletVessel,
    TheArm,
    TheClub,
    TheEye,
    TheFish,
    TheFlint,
    TheGoad,
    TheHead,
    TheHook,
    TheHouse,
    TheManacle,
    TheMark,
    TheMouth,
    TheNeedle,
    TheOx,
    ThePillar,
    ThePlant,
    ThePsychic,
    TheSerpent,
    TheTooth,
    TheWall,
    TheWater,
    TheWheel,
    TheWindow,
}

public enum MotelyBossBlind
{
    AmberAcorn = MotelyBossBlindWithoutData.AmberAcorn | MotelyBossBlindType.Finisher,
    CeruleanBell = MotelyBossBlindWithoutData.CeruleanBell | MotelyBossBlindType.Finisher,
    CrimsonHeart = MotelyBossBlindWithoutData.CrimsonHeart | MotelyBossBlindType.Finisher,
    VerdantLeaf = MotelyBossBlindWithoutData.VerdantLeaf | MotelyBossBlindType.Finisher,
    VioletVessel = MotelyBossBlindWithoutData.VioletVessel | MotelyBossBlindType.Finisher,

    TheArm =
        MotelyBossBlindWithoutData.TheArm
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheClub = MotelyBossBlindWithoutData.TheClub | MotelyBossBlindType.Normal,
    TheEye =
        MotelyBossBlindWithoutData.TheEye
        | MotelyBossBlindType.Normal
        | (3 << MotelyCore.BossRequiredAnteOffset),
    TheFish =
        MotelyBossBlindWithoutData.TheFish
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheFlint =
        MotelyBossBlindWithoutData.TheFlint
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheGoad = MotelyBossBlindWithoutData.TheGoad | MotelyBossBlindType.Normal,
    TheHead = MotelyBossBlindWithoutData.TheHead | MotelyBossBlindType.Normal,
    TheHook = MotelyBossBlindWithoutData.TheHook | MotelyBossBlindType.Normal,
    TheHouse =
        MotelyBossBlindWithoutData.TheHouse
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheManacle = MotelyBossBlindWithoutData.TheManacle | MotelyBossBlindType.Normal,
    TheMark =
        MotelyBossBlindWithoutData.TheMark
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheMouth =
        MotelyBossBlindWithoutData.TheMouth
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheNeedle =
        MotelyBossBlindWithoutData.TheNeedle
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheOx =
        MotelyBossBlindWithoutData.TheOx
        | MotelyBossBlindType.Normal
        | (6 << MotelyCore.BossRequiredAnteOffset),
    ThePillar = MotelyBossBlindWithoutData.ThePillar | MotelyBossBlindType.Normal,
    ThePlant =
        MotelyBossBlindWithoutData.ThePlant
        | MotelyBossBlindType.Normal
        | (4 << MotelyCore.BossRequiredAnteOffset),
    ThePsychic = MotelyBossBlindWithoutData.ThePsychic | MotelyBossBlindType.Normal,
    TheSerpent =
        MotelyBossBlindWithoutData.TheSerpent
        | MotelyBossBlindType.Normal
        | (5 << MotelyCore.BossRequiredAnteOffset),
    TheTooth =
        MotelyBossBlindWithoutData.TheTooth
        | MotelyBossBlindType.Normal
        | (3 << MotelyCore.BossRequiredAnteOffset),
    TheWall =
        MotelyBossBlindWithoutData.TheWall
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheWater =
        MotelyBossBlindWithoutData.TheWater
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheWheel =
        MotelyBossBlindWithoutData.TheWheel
        | MotelyBossBlindType.Normal
        | (2 << MotelyCore.BossRequiredAnteOffset),
    TheWindow = MotelyBossBlindWithoutData.TheWindow | MotelyBossBlindType.Normal,
}

public static class MotelyBossBlindExt
{
    public static readonly MotelyBossBlind[] FinisherBossBlinds =
    [
        MotelyBossBlind.AmberAcorn,
        MotelyBossBlind.CeruleanBell,
        MotelyBossBlind.CrimsonHeart,
        MotelyBossBlind.VerdantLeaf,
        MotelyBossBlind.VioletVessel,
    ];

    public static readonly MotelyBossBlind[] NormalBossBlinds =
    [
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
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBossIndex(this MotelyBossBlind blind) => ((int)blind) & 0xFF;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetBossMinAnte(this MotelyBossBlind blind) =>
        (((int)blind) & MotelyCore.BossRequiredAnteMask) >> MotelyCore.BossRequiredAnteOffset;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MotelyBossBlindType GetBossType(this MotelyBossBlind blind) =>
        (MotelyBossBlindType)(((int)blind) & MotelyCore.BossTypeMask);
}

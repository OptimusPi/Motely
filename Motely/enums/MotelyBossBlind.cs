namespace Motely;

public enum MotelyBossBlind
{
    // Order matches OpenCL BOSSES array for correct indexing
    TheArm,          // 0
    TheClub,         // 1
    TheEye,          // 2
    AmberAcorn,      // 3  - finisher boss
    CeruleanBell,    // 4  - finisher boss
    CrimsonHeart,    // 5  - finisher boss
    VerdantLeaf,     // 6  - finisher boss
    VioletVessel,    // 7  - finisher boss
    TheFish,         // 8
    TheFlint,        // 9
    TheGoad,         // 10
    TheHead,         // 11
    TheHook,         // 12
    TheHouse,        // 13
    TheManacle,      // 14
    TheMark,         // 15
    TheMouth,        // 16
    TheNeedle,       // 17
    TheOx,           // 18
    ThePillar,       // 19
    ThePlant,        // 20
    ThePsychic,      // 21
    TheSerpent,      // 22
    TheTooth,        // 23
    TheWall,         // 24
    TheWater,        // 25
    TheWheel,        // 26
    TheWindow,       // 27
    
    // Non-boss blinds (not in BOSSES array)
    SmallBlind = 100,  // Special value to avoid collision
    BigBlind = 101     // Special value to avoid collision
}
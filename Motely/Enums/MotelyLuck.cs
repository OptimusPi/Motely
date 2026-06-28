namespace Motely.Enums;

/// <summary>
/// Oops! All 6s luck multiplier. Each Oops doubles an event's probability, so the
/// backing value IS the multiplier applied as <c>baseLuck</c> in the event checks
/// (<c>GetNextRandom() &lt; baseLuck / Chance</c>).
/// <para>
/// For a 50/50 event (Chance = 2), <see cref="X2"/> already saturates: 2/2 = 1.0,
/// and <c>GetNextRandom()</c> is [0, 1), so it always fires — one Oops = guaranteed.
/// </para>
/// </summary>
public enum MotelyLuck
{
    /// <summary>No Oops — base odds.</summary>
    X1 = 1,

    /// <summary>1 Oops — double odds.</summary>
    X2 = 2,

    /// <summary>2 Oops — quadruple odds.</summary>
    X4 = 4,

    X5 = 5,

    /// <summary>3 Oops — octuple odds.</summary>
    X8 = 8,

    /// <summary>4 Oops — 16× odds.</summary>
    X16 = 16,

    /// <summary>5 Oops — 32× odds.</summary>
    X32 = 32,

    /// <summary>6 Oops — 64× odds.</summary>
    X64 = 64,
}

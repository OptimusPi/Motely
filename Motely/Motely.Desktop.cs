// Desktop-specific Motely constants
#if !BROWSER && !ANDROID && !IOS
namespace Motely;

/// <summary>
/// Desktop-specific Motely constants
/// Desktop uses Vector512<double> (8 lanes) for maximum performance
/// </summary>
public static partial class Motely
{
    public const int MaxVectorWidth = 8; // Vector512<double> for desktop
}
#endif


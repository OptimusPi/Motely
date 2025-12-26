// iOS-specific Motely constants
#if IOS
namespace Motely;

/// <summary>
/// iOS-specific Motely constants
/// iOS devices vary - newer devices may support wider vectors
/// Default to Vector256 (4 lanes) for compatibility
/// </summary>
public static partial class Motely
{
    // iOS devices vary - newer devices support wider vectors
    // For maximum compatibility, use Vector256 (4 lanes)
    // Can be optimized at runtime based on device capabilities
    public const int MaxVectorWidth = 4; // Vector256<double> for iOS compatibility
}
#endif


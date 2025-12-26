// Android-specific Motely constants
#if ANDROID
namespace Motely;

/// <summary>
/// Android-specific Motely constants
/// Android may support Vector256 or Vector512 depending on device capabilities
/// Default to Vector256 (4 lanes) for compatibility, can be optimized per device
/// </summary>
public static partial class Motely
{
    // Android devices vary - some support AVX2 (Vector256), some don't
    // For maximum compatibility, use Vector256 (4 lanes)
    // Can be optimized at runtime based on device capabilities
    public const int MaxVectorWidth = 4; // Vector256<double> for Android compatibility
}
#endif


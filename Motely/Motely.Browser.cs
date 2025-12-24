// Browser-specific Motely constants
#if BROWSER
namespace Motely;

/// <summary>
/// Browser-specific Motely constants
/// Browser SIMD can do 8 - Microsoft compiler stacks 4x Vector128 together for us
/// </summary>
public static partial class Motely
{
    public const int MaxVectorWidth = 8; // Compiler handles vectorization automatically
}
#endif


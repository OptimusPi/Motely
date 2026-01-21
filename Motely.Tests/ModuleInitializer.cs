using System.Runtime.CompilerServices;
using System.Text;
using VerifyTests;

namespace Motely.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyDiffPlex.Initialize();

        // Optional: Configure Verify settings
        VerifierSettings.TreatAsString<StringBuilder>();
    }
}

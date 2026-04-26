namespace Motely.Tests;

internal static class GoldenDirectory
{
    private static readonly string TestProjectDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));

    internal static string Resolve() => Path.Combine(TestProjectDir, "golden");

    internal static string ResolveGoldenJamlFiles() => Path.Combine(TestProjectDir, "GoldenJamlFiles");
}

#if !BROWSER

namespace Motely.Datalake;

public static class SeedStoragePaths
{
    public static string StandardLakeDirectory => Path.GetFullPath("./Seeds");
}

#endif

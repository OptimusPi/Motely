namespace Motely.Tests;

/// <summary>
/// Resolves <c>Motely.Tests/golden/</c> in the source tree by walking up from the test
/// binary directory looking for <c>Motely.sln</c>. Goldens live in source so updates
/// flow through git diff, not <c>bin/</c>.
/// </summary>
internal static class GoldenDirectory
{
  internal static string Resolve()
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
      if (File.Exists(Path.Combine(dir.FullName, "Motely.sln")))
        return Path.Combine(dir.FullName, "Motely.Tests", "golden");
      dir = dir.Parent;
    }
    throw new DirectoryNotFoundException(
      $"Could not locate Motely.sln walking up from {AppContext.BaseDirectory}."
    );
  }

  internal static string ResolveRepoSubdir(string subdir)
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
      if (File.Exists(Path.Combine(dir.FullName, "Motely.sln")))
        return Path.Combine(dir.FullName, subdir);
      dir = dir.Parent;
    }
    throw new DirectoryNotFoundException(
      $"Could not locate Motely.sln walking up from {AppContext.BaseDirectory}."
    );
  }
}

namespace Motely.Datalake;

/// <summary>
/// Where the Parquet seed-lake lives on disk: <c>&lt;root&gt;/&lt;filterId&gt;/</c>, one folder
/// per filter, accumulating one <c>*.parquet</c> file per run. Root defaults to
/// <see cref="DefaultRoot"/>; override with the env var or the CLI <c>--results-path</c>.
/// </summary>
public static class MotelyLakePaths
{
    public const string DefaultRoot = ".seeds";

    public const string RootEnvVar = "MOTELY_DATALAKE_PATH";

    /// <summary>Resolve the lake root: explicit arg, else env var, else <see cref="DefaultRoot"/>.</summary>
    public static string ResolveRoot(string? root)
    {
        if (!string.IsNullOrWhiteSpace(root))
            return root;

        var env = Environment.GetEnvironmentVariable(RootEnvVar);
        return string.IsNullOrWhiteSpace(env) ? DefaultRoot : env;
    }

    /// <summary>The per-filter lake directory the writer fills and the drown provider globs.</summary>
    public static string LakeDir(string? root, string filterId) =>
        Path.Combine(ResolveRoot(root), filterId);
}

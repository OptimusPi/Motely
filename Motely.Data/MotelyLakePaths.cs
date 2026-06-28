namespace Motely.Data;

public static class MotelyLakePaths
{
    public const string DefaultRoot = ".seeds";

    public const string RootEnvVar = "MOTELY_DATALAKE_PATH";

    public static string ResolveRoot(string? root)
    {
        if (!string.IsNullOrWhiteSpace(root))
            return root;

        var env = Environment.GetEnvironmentVariable(RootEnvVar);
        return string.IsNullOrWhiteSpace(env) ? DefaultRoot : env;
    }

    public static string LakeDir(string? root, string filterId) =>
        Path.Combine(ResolveRoot(root), filterId);
}

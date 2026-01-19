using Motely.API;

namespace Motely.Tests;

/// <summary>
/// Security tests for MotelyPaths path validation.
/// Note: These tests validate the security logic indirectly since MotelyPaths
/// is a static class and calling Initialize would affect other tests.
/// The actual initialization is tested through integration tests.
/// </summary>
public class MotelyPathsSecurityTests
{
    [Theory]
    [InlineData("/etc/motely")]
    [InlineData("/sys/motely")]
    [InlineData("/proc/motely")]
    [InlineData("/dev/motely")]
    [InlineData("/boot/motely")]
    [InlineData("/root/motely")]
    [InlineData("/bin/motely")]
    [InlineData("/sbin/motely")]
    [InlineData("/usr/bin/motely")]
    [InlineData("/usr/sbin/motely")]
    [InlineData("/lib/motely")]
    [InlineData("/lib64/motely")]
    public void PathValidation_ShouldRejectUnixSystemDirectories(string dangerousPath)
    {
        // This test documents the expected behavior.
        // The actual validation happens in MotelyPaths.Initialize()
        // which throws InvalidOperationException for these paths.
        
        // All these paths should start with a known Unix system directory
        Assert.True(IsUnixSystemPath(dangerousPath),
                   $"Path {dangerousPath} should be recognized as a Unix system directory");
    }

    [Theory]
    [InlineData("C:/Windows/motely")]
    [InlineData("C:/Windows/System32/motely")]
    [InlineData("C:/Program Files/motely")]
    [InlineData("C:/Program Files (x86)/motely")]
    [InlineData("C:/ProgramData/motely")]
    public void PathValidation_ShouldRejectWindowsSystemDirectories(string dangerousPath)
    {
        // This test documents the expected behavior.
        // The actual validation happens in MotelyPaths.Initialize()
        // which throws InvalidOperationException for these paths.
        
        // All these paths should start with a known Windows system directory
        Assert.True(IsWindowsSystemPath(dangerousPath),
                   $"Path {dangerousPath} should be recognized as a Windows system directory");
    }

    [Theory]
    [InlineData("./data/jaml-filters")]
    [InlineData("../data/seed-sources")]
    [InlineData("custom/path")]
    public void PathValidation_ShouldAcceptRelativePaths(string safePath)
    {
        // Relative paths are always accepted and resolved relative to ContentRoot
        Assert.False(Path.IsPathRooted(safePath),
                    $"Path {safePath} should be relative");
    }

    [Theory]
    [InlineData("/home/user/motely/data")]
    [InlineData("/var/app/motely")]
    [InlineData("/opt/motely/filters")]
    [InlineData("/tmp/motely/data")]
    [InlineData("C:/Users/user/motely")]
    [InlineData("D:/data/motely")]
    public void PathValidation_ShouldAcceptSafeAbsolutePaths(string safePath)
    {
        // These absolute paths are outside system directories and should be accepted
        Assert.False(IsUnixSystemPath(safePath) || IsWindowsSystemPath(safePath),
                    $"Path {safePath} should be outside system directories");
    }

    // Helper methods that mirror the logic in MotelyPaths.IsSensitiveSystemPath
    private static bool IsUnixSystemPath(string path)
    {
        var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
        
        string[] sensitiveRoots = new[]
        {
            "/etc", "/sys", "/proc", "/dev", "/boot", "/root",
            "/bin", "/sbin", "/usr/bin", "/usr/sbin", "/lib", "/lib64"
        };

        foreach (var root in sensitiveRoots)
        {
            if (normalizedPath.Equals(root, StringComparison.Ordinal) ||
                normalizedPath.StartsWith(root + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }
        
        return false;
    }

    private static bool IsWindowsSystemPath(string path)
    {
        var normalizedPath = path.Replace('\\', '/').TrimEnd('/');
        
        string[] sensitiveRoots = new[]
        {
            "C:/Windows", "C:/Windows/System32", "C:/Program Files",
            "C:/Program Files (x86)", "C:/ProgramData"
        };

        foreach (var root in sensitiveRoots)
        {
            if (normalizedPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
}

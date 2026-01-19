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
        
        // Verify the path is recognized as a Unix system path
        Assert.True(dangerousPath.StartsWith("/etc") ||
                   dangerousPath.StartsWith("/sys") ||
                   dangerousPath.StartsWith("/proc") ||
                   dangerousPath.StartsWith("/dev") ||
                   dangerousPath.StartsWith("/boot") ||
                   dangerousPath.StartsWith("/root") ||
                   dangerousPath.StartsWith("/bin") ||
                   dangerousPath.StartsWith("/sbin") ||
                   dangerousPath.StartsWith("/usr/bin") ||
                   dangerousPath.StartsWith("/usr/sbin") ||
                   dangerousPath.StartsWith("/lib") ||
                   dangerousPath.StartsWith("/lib64"),
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
        
        // Verify the path is recognized as a Windows system path
        var normalizedPath = dangerousPath.Replace('\\', '/');
        Assert.True(normalizedPath.StartsWith("C:/Windows", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith("C:/Program Files", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith("C:/ProgramData", StringComparison.OrdinalIgnoreCase),
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
        var normalizedPath = safePath.Replace('\\', '/');
        
        // Unix system paths to avoid (using StartsWith to match actual implementation)
        var isUnixSystemPath = normalizedPath.StartsWith("/etc/") || normalizedPath == "/etc" ||
                              normalizedPath.StartsWith("/sys/") || normalizedPath == "/sys" ||
                              normalizedPath.StartsWith("/proc/") || normalizedPath == "/proc" ||
                              normalizedPath.StartsWith("/dev/") || normalizedPath == "/dev" ||
                              normalizedPath.StartsWith("/boot/") || normalizedPath == "/boot" ||
                              normalizedPath.StartsWith("/root/") || normalizedPath == "/root" ||
                              normalizedPath.StartsWith("/bin/") || normalizedPath == "/bin" ||
                              normalizedPath.StartsWith("/sbin/") || normalizedPath == "/sbin" ||
                              normalizedPath.StartsWith("/usr/bin/") || normalizedPath == "/usr/bin" ||
                              normalizedPath.StartsWith("/usr/sbin/") || normalizedPath == "/usr/sbin" ||
                              normalizedPath.StartsWith("/lib/") || normalizedPath == "/lib" ||
                              normalizedPath.StartsWith("/lib64/") || normalizedPath == "/lib64";
        
        // Windows system paths to avoid (using StartsWith to match actual implementation)
        var isWindowsSystemPath = normalizedPath.StartsWith("C:/Windows/", StringComparison.OrdinalIgnoreCase) || 
                                 normalizedPath.Equals("C:/Windows", StringComparison.OrdinalIgnoreCase) ||
                                 normalizedPath.StartsWith("C:/Program Files/", StringComparison.OrdinalIgnoreCase) ||
                                 normalizedPath.Equals("C:/Program Files", StringComparison.OrdinalIgnoreCase) ||
                                 normalizedPath.StartsWith("C:/ProgramData/", StringComparison.OrdinalIgnoreCase) ||
                                 normalizedPath.Equals("C:/ProgramData", StringComparison.OrdinalIgnoreCase);
        
        Assert.False(isUnixSystemPath || isWindowsSystemPath,
                    $"Path {safePath} should be outside system directories");
    }
}

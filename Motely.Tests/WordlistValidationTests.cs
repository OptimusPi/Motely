using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using VerifyXunit;

namespace Motely.Tests;

/// <summary>
/// Comprehensive wordlist validation tests using Verify() to catch filter logic bugs
/// These tests run actual CLI commands with wordlists and verify the complete output
/// </summary>
public sealed class WordlistValidationTests
{
    /// <summary>
    /// Helper method to run Motely CLI with wordlist and capture full output
    /// </summary>
    private static async Task<string> RunMotelyWithWordlist(string configFileName, string wordlistName)
    {
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -c Release -- --json {Path.GetFileNameWithoutExtension(configFileName)} --wordlist {wordlistName}",
            WorkingDirectory = motelyProjectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        
        var output = new StringBuilder();
        var error = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) error.AppendLine(e.Data);
        };
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var ctxSource = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3.14));
        var ctx = ctxSource.Token;
        await process.WaitForExitAsync(ctx);
        Assert.True(process.HasExited, "Process did not complete within timeout");
        
        var outputText = output.ToString();
        var errorText = error.ToString();
        
        // For debugging failed tests
        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Process failed with exit code: {process.ExitCode}");
            Console.WriteLine($"Standard Output:\n{outputText}");
            Console.WriteLine($"Standard Error:\n{errorText}");
        }
        
        Assert.Equal(0, process.ExitCode);
        return outputText;
    }

    /// <summary>
    /// Test the tag filter ante logic bug regression with known seeds
    /// This test would have caught the original bug where antes used AND instead of OR logic
    /// </summary>
    [Fact]
    public async Task TagFilter_AnteLogic_Regression()
    {
        // Create a test wordlist with known tag behavior seeds
        var testWordlistPath = Path.Combine("WordLists", "tag-ante-regression-test.txt");
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var fullWordlistPath = Path.Combine(motelyProjectDir, testWordlistPath);
        
        // Seeds with known tag patterns for testing ante OR logic
        var testSeeds = new[]
        {
            "ALEEB",      // Known to have multiple tags across different antes
            "5Q32K111",   // Known to match BullYourself config
            "UNITTEST",   // Test seed with predictable behavior
            "12345678"    // Another test seed
        };
        
        Directory.CreateDirectory(Path.GetDirectoryName(fullWordlistPath)!);
        await File.WriteAllLinesAsync(fullWordlistPath, testSeeds);
        
        try
        {
            // Test with a config that has multiple antes (should use OR logic)
            var output = await RunMotelyWithWordlist("aleeb-tag-test.json", "tag-ante-regression-test");
            
            // Verify the complete output - this will catch any logic changes
            await Verify(output)
                .UseFileName("tag_ante_regression")
                .ScrubLinesContaining("Duration:")
                .ScrubLinesContaining("Speed:");
        }
        finally
        {
            // Cleanup
            if (File.Exists(fullWordlistPath))
                File.Delete(fullWordlistPath);
        }
    }

    /// <summary>
    /// Test BullYourself config with wordlist to verify complex multi-filter logic
    /// </summary>
    [Fact] 
    public async Task BullYourself_Config_WordlistValidation()
    {
        // Create a test wordlist with seeds that should match the BullYourself criteria
        var testWordlistPath = Path.Combine("WordLists", "bullyourself-test.txt");
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var fullWordlistPath = Path.Combine(motelyProjectDir, testWordlistPath);
        
        var testSeeds = new[]
        {
            "5Q32K111",   // Known to match
            "ALEEB",      // Test seed
            "BADSEEDS",   // Should not match
            "UNITTEST",   // Control seed
            "NOMATCH1",   // Should not match
            "TESTCASE"    // Test case
        };
        
        Directory.CreateDirectory(Path.GetDirectoryName(fullWordlistPath)!);
        await File.WriteAllLinesAsync(fullWordlistPath, testSeeds);
        
        try
        {
            var output = await RunMotelyWithWordlist("BullYourself.json", "bullyourself-test");
            
            // This will catch any changes in the complex filter logic
            await Verify(output)
                .UseFileName("bullyourself_wordlist_validation")
                .ScrubLinesContaining("Duration:")
                .ScrubLinesContaining("Speed:");
        }
        finally
        {
            if (File.Exists(fullWordlistPath))
                File.Delete(fullWordlistPath);
        }
    }

    /// <summary>
    /// Test ante range edge cases with wordlist
    /// Validates that filters correctly handle ante boundaries (1-8)
    /// </summary>
    [Fact]
    public async Task AnteRange_EdgeCases_WordlistValidation()
    {
        var testWordlistPath = Path.Combine("WordLists", "ante-edge-cases-test.txt");
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var fullWordlistPath = Path.Combine(motelyProjectDir, testWordlistPath);
        
        // Create a larger set of test seeds for comprehensive testing
        var testSeeds = new[]
        {
            "ALEEB", "UNITTEST", "12345678", "5Q32K111", "KK1XD111",
            "TESTCASE", "EDGE001", "EDGE002", "BOUNDARY", "LIMITS99"
        };
        
        Directory.CreateDirectory(Path.GetDirectoryName(fullWordlistPath)!);
        await File.WriteAllLinesAsync(fullWordlistPath, testSeeds);
        
        try
        {
            // Test with a config that spans multiple ante ranges
            var output = await RunMotelyWithWordlist("aleeb-all-categories-test.json", "ante-edge-cases-test");
            
            await Verify(output)
                .UseFileName("ante_range_edge_cases")
                .ScrubLinesContaining("Duration:")
                .ScrubLinesContaining("Speed:");
        }
        finally
        {
            if (File.Exists(fullWordlistPath))
                File.Delete(fullWordlistPath);
        }
    }

    /// <summary>
    /// Test joker filter with multi-ante configuration using wordlist
    /// Ensures joker OR logic across antes works correctly
    /// </summary>
    [Fact]
    public async Task JokerFilter_MultiAnte_WordlistValidation()
    {
        var testWordlistPath = Path.Combine("WordLists", "joker-multi-ante-test.txt");
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var fullWordlistPath = Path.Combine(motelyProjectDir, testWordlistPath);
        
        var testSeeds = new[]
        {
            "ALEEB",      // Has Blueprint in ante 2
            "PERKEO01",   // Test Perkeo seeds
            "BLUEPRINT",  // Blueprint-focused
            "UNITTEST"    // Control
        };
        
        Directory.CreateDirectory(Path.GetDirectoryName(fullWordlistPath)!);
        await File.WriteAllLinesAsync(fullWordlistPath, testSeeds);
        
        try
        {
            var output = await RunMotelyWithWordlist("aleeb-joker-test.json", "joker-multi-ante-test");
            
            await Verify(output)
                .UseFileName("joker_multi_ante_validation")
                .ScrubLinesContaining("Duration:")
                .ScrubLinesContaining("Speed:");
        }
        finally
        {
            if (File.Exists(fullWordlistPath))
                File.Delete(fullWordlistPath);
        }
    }

    /// <summary>
    /// Test performance and correctness with larger wordlists
    /// Validates that wordlist processing scales correctly
    /// </summary>
    [Fact]
    public async Task LargeWordlist_Performance_Validation()
    {
        var testWordlistPath = Path.Combine("WordLists", "large-wordlist-test.txt");
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var fullWordlistPath = Path.Combine(motelyProjectDir, testWordlistPath);
        
        // Create a larger wordlist (50 seeds) to test performance and correctness
        var testSeeds = new List<string>();
        
        // Add known good seeds
        testSeeds.AddRange(new[] { "ALEEB", "UNITTEST", "12345678", "5Q32K111", "KK1XD111" });
        
        // Add generated test seeds
        for (int i = 1; i <= 45; i++)
        {
            testSeeds.Add($"TEST{i:D4}");
        }
        
        Directory.CreateDirectory(Path.GetDirectoryName(fullWordlistPath)!);
        await File.WriteAllLinesAsync(fullWordlistPath, testSeeds);
        
        try
        {
            // Use a simple config to focus on wordlist processing
            var output = await RunMotelyWithWordlist("aleeb-simple-working-test.json", "large-wordlist-test");
            
            await Verify(output)
                .UseFileName("large_wordlist_performance")
                .ScrubLinesContaining("Duration:")
                .ScrubLinesContaining("Speed:");
        }
        finally
        {
            if (File.Exists(fullWordlistPath))
                File.Delete(fullWordlistPath);
        }
    }
}
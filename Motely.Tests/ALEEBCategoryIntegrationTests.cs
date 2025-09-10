using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Motely.Tests;

/// <summary>
/// Integration tests that verify ALEEB seed matches single MUST clauses for each filter category
/// and a comprehensive test with all categories combined
/// </summary>
public sealed class ALEEBCategoryIntegrationTests
{
    private static string RunMotelyWithJsonConfig(string configFileName)
    {
        // Copy test JSON to the expected location
        var sourceJsonPath = Path.Combine("TestJsonConfigs", configFileName);
        Assert.True(File.Exists(sourceJsonPath), $"Test JSON file not found at: {sourceJsonPath}");

        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var targetDir = Path.Combine(motelyProjectDir, "JsonItemFilters");
        var targetPath = Path.Combine(targetDir, Path.GetFileNameWithoutExtension(configFileName) + "-temp.json");
        
        Assert.True(Directory.Exists(targetDir), $"JsonItemFilters directory not found at: {targetDir}");
        File.Copy(sourceJsonPath, targetPath, overwrite: true);
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run -c Release -- --seed ALEEB --json {Path.GetFileNameWithoutExtension(configFileName)}-temp",
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
            
            var completed = process.WaitForExit(30000);
            Assert.True(completed, "Process did not complete within timeout");
            
            var outputText = output.ToString();
            var errorText = error.ToString();
            
            // Debug output for troubleshooting
            if (!outputText.Contains("Seeds matched: 1"))
            {
                Console.WriteLine($"Test failed for config: {configFileName}");
                Console.WriteLine($"Exit code: {process.ExitCode}");
                Console.WriteLine($"Standard Output:\n{outputText}");
                Console.WriteLine($"Standard Error:\n{errorText}");
            }
            
            // The CLI should succeed (exit code 0)
            Assert.Equal(0, process.ExitCode);
            
            return outputText;
        }
        finally
        {
            // Clean up the test file
            if (File.Exists(targetPath))
                File.Delete(targetPath);
        }
    }
    
    [Fact]
    public void ALEEB_Matches_Joker_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-joker-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base Joker filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void ALEEB_Matches_Voucher_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-voucher-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base Voucher filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void ALEEB_Matches_Boss_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-boss-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base Boss filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void ALEEB_Matches_Tag_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-tag-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base Tag filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void ALEEB_Matches_Tarot_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-tarot-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base TarotCard filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void ALEEB_Matches_Planet_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-planet-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base PlanetCard filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void ALEEB_Matches_PlayingCard_Category()
    {
        var output = RunMotelyWithJsonConfig("aleeb-playingcard-test.json");
        
        // Verify the filter was created and matched
        Assert.Contains("+ Base PlayingCard filter: 1 clauses", output);
        Assert.Contains("ALEEB", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched: 1", output);
    }
    
    [Fact]
    public void Saturn_Appears_In_Ante2_ShopSlot0()
    {
        var output = RunMotelyWithJsonConfig("saturn-ante2-shop0-test.json");

        // Verify the filter was created and matched
        Assert.Contains("+ Base PlanetCard filter: 1 clauses", output);
        Assert.Contains("SEARCH COMPLETED", output);
        Assert.Contains("Seeds matched:", output);
    }
    
    [Fact]
    public void ALEEB_Matches_ALL_Categories_Combined()
    {
        var output = RunMotelyWithJsonConfig("aleeb-all-categories-test.json");

        // Verify ALEEB matches with all constraints
        Assert.Contains("ALEEB", output);
        Assert.Contains("Seeds matched: 1", output);
    }

    [Fact]
    public void ChainsWith_ALL_Categories_Combined()
    {
        var output = RunMotelyWithJsonConfig("aleeb-all-categories-test.json");
        
        // The comprehensive test has items from multiple categories
        // so we should see chaining happening
        Assert.Contains("Base Joker filter: 2 clauses", output);
        Assert.Contains("Chained Voucher filter: 2 clauses", output);
        Assert.Contains("Chained Boss filter: 3 clauses", output);
        Assert.Contains("Chained Tag filter: 2 clauses", output);
        Assert.Contains("Chained TarotCard filter: 2 clauses", output);
        Assert.Contains("Chained PlanetCard filter: 1 clauses", output);
        Assert.Contains("Chained PlayingCard filter: 2 clauses", output);
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Motely.Tests;

public sealed class JsonFilterSlicedChainIntegrationTests
{
    [Fact]
    public void SlicedFilterChain_CLI_RealJsonFile_VerifiesALEEBBlueprint()
    {
        // This integration test runs the actual Motely CLI with --json flag
        // to verify the sliced filter chain setup works end-to-end
        
        // The test runs from bin/Release/net9.0, and TestJsonConfigs is copied there
        var sourceJsonPath = Path.Combine("TestJsonConfigs", "aleeb-blueprint-test.json");
        Assert.True(File.Exists(sourceJsonPath), $"Test JSON file not found at: {sourceJsonPath}");
        
        // We need to find the Motely project directory from the test output directory
        // AppContext.BaseDirectory is bin/Release/net9.0
        // Go up to Motely.Tests, then to external/Motely, then to Motely project
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var targetDir = Path.Combine(motelyProjectDir, "JsonItemFilters");
        var targetPath = Path.Combine(targetDir, "test-aleeb-blueprint.json");
        
        Assert.True(Directory.Exists(targetDir), $"JsonItemFilters directory not found at: {targetDir}");
        File.Copy(sourceJsonPath, targetPath, overwrite: true);
        
        try
        {
            // Run the CLI from the Motely directory so it can find JsonItemFilters
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run -c Release -- --seed ALEEB --json test-aleeb-blueprint",
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
        
        var completed = process.WaitForExit(30000); // 30 second timeout
        Assert.True(completed, "Process did not complete within timeout");
        
        var outputText = output.ToString();
        var errorText = error.ToString();
        
        // Debug: Output what we actually got
        if (process.ExitCode != 0)
        {
            Console.WriteLine($"Process failed with exit code: {process.ExitCode}");
            Console.WriteLine($"Standard Output:\n{outputText}");
            Console.WriteLine($"Standard Error:\n{errorText}");
        }
        
        // The CLI should succeed (exit code 0)
        Assert.Equal(0, process.ExitCode);
        
            // Verify the output shows the sliced filter chain setup
            Assert.Contains("Motely Ouija Search Starting", outputText);
            Assert.Contains("Config: test-aleeb-blueprint", outputText);
            Assert.Contains("+ Base Joker filter: 1 clauses", outputText);
            
            // Verify the seed matches (CSV output)
            Assert.Contains("ALEEB", outputText);
            
            // Verify completion
            Assert.Contains("SEARCH COMPLETED", outputText);
            Assert.Contains("Seeds matched: 1", outputText);
            
            // Should not have errors
            Assert.DoesNotContain("Error", outputText);
            Assert.DoesNotContain("Exception", errorText);
        }
        finally
        {
            // Clean up the test file
            if (File.Exists(targetPath))
                File.Delete(targetPath);
        }
    }
    
    [Fact]
    public void SlicedFilterChain_CLI_MultipleCategories_TestChaining()
    {
        // Copy test JSON with multiple categories to the expected location
        var sourceJsonPath = Path.Combine("TestJsonConfigs", "multi-category-test.json");
        Assert.True(File.Exists(sourceJsonPath), $"Test JSON file not found at: {sourceJsonPath}");
        
        var motelyProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Motely"));
        var targetDir = Path.Combine(motelyProjectDir, "JsonItemFilters");
        var targetPath = Path.Combine(targetDir, "test-multi-category.json");
        
        Assert.True(Directory.Exists(targetDir), $"JsonItemFilters directory not found at: {targetDir}");
        File.Copy(sourceJsonPath, targetPath, overwrite: true);
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run -c Release -- --seed ALEEB --json test-multi-category",
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
            
            // The CLI should succeed
            Assert.Equal(0, process.ExitCode);
            
            // Verify the output shows both filters in the chain
            Assert.Contains("+ Base Joker filter: 1 clauses", outputText);
            Assert.Contains("+ Chained Voucher filter: 1 clauses", outputText);
            
            // ALEEB has Blueprint at slot 8 (index 7) in Ante 2
            // and Hieroglyph voucher in Ante 2
            Assert.Contains("ALEEB", outputText);
            Assert.Contains("SEARCH COMPLETED", outputText);
        }
        finally
        {
            // Clean up the test file
            if (File.Exists(targetPath))
                File.Delete(targetPath);
        }
    }
    
    [Fact]
    public void SlicedFilterChain_CLI_AnalyzeMode_VerifiesOutput()
    {
        // Test the --analyze flag to verify the seed data
        var projectPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "Motely", "Motely.csproj"));
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -c Release --project \"{projectPath}\" -- --analyze ALEEB",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        
        var output = new StringBuilder();
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        
        process.BeginOutputReadLine();
        
        var completed = process.WaitForExit(30000);
        Assert.True(completed, "Process did not complete within timeout");
        
        Assert.Equal(0, process.ExitCode);
        
        var outputText = output.ToString();
        
        // Verify key elements of ALEEB seed
        Assert.Contains("==ANTE 2==", outputText);
        Assert.Contains("Boss: The Arm", outputText);
        Assert.Contains("Voucher: Hieroglyph", outputText);
        Assert.Contains("8) Blueprint", outputText); // Blueprint at position 8
        
        // Verify the output format matches the expected structure
        Assert.Contains("Shop Queue:", outputText);
        Assert.Contains("Packs:", outputText);
        Assert.Contains("Tags:", outputText);
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Oracle.Models;

namespace Oracle.Services;

public interface IOuijaService
{
    Task StartSearchAsync(SearchCriteria criteria, string engine, int batchMultiplier, int threadGroups, int cpuThreads, int cpuBatchedDigits, IProgress<SearchProgress> progress);
    Task StopSearchAsync();
    bool IsRunning { get; }
}

public class OuijaService : IOuijaService, IDisposable
{
    private Process? _currentProcess;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private readonly ConfigService _configService;

    public bool IsRunning => _currentProcess?.HasExited == false;

    public OuijaService(ConfigService configService)
    {
        _configService = configService;
    }

    public async Task StartSearchAsync(
        SearchCriteria criteria, 
        string engine,
        int batchMultiplier,
        int threadGroups,
        int cpuThreads,
        int cpuBatchedDigits,
        IProgress<SearchProgress> progress)
    {
        await _processLock.WaitAsync();
        try
        {
            if (IsRunning)
            {
                await StopSearchAsync();
            }

            _cancellationTokenSource = new CancellationTokenSource();
            
            var startInfo = new ProcessStartInfo
            {
                FileName = GetOuijaPath(),
                Arguments = BuildArguments(criteria, engine, batchMultiplier, threadGroups, cpuThreads, cpuBatchedDigits),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _currentProcess = new Process { StartInfo = startInfo };
            
            // Set up output handlers
            var outputHandler = new OuijaOutputParser(progress);
            _currentProcess.OutputDataReceived += (s, e) => 
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputHandler.ParseLine(e.Data);
                }
            };
            
            _currentProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine($"Ouija stderr: {e.Data}");
                }
            };

            Console.WriteLine($"Starting Ouija search with command: {startInfo.FileName} {startInfo.Arguments}");

            _currentProcess.Start();
            _currentProcess.BeginOutputReadLine();
            _currentProcess.BeginErrorReadLine();

            // Wait for completion
            await _currentProcess.WaitForExitAsync(_cancellationTokenSource.Token);
            
            var exitCode = _currentProcess.ExitCode;
            Console.WriteLine($"Ouija search completed with exit code: {exitCode}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Search cancelled by user");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running Ouija search: {ex.Message}");
            throw;
        }
        finally
        {
            _processLock.Release();
        }
    }

    public async Task StopSearchAsync()
    {
        if (_currentProcess?.HasExited == false)
        {
            _cancellationTokenSource?.Cancel();
            
            try
            {
                // Try graceful shutdown first
                _currentProcess.CloseMainWindow();
                
                if (!_currentProcess.WaitForExit(5000))
                {
                    _currentProcess.Kill(entireProcessTree: true);
                    Console.WriteLine("Had to forcefully kill Ouija process");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping Ouija process: {ex.Message}");
            }
        }
        await Task.CompletedTask;
    }

    private string GetOuijaPath()
    {
        // Check configured path first
        if (!string.IsNullOrEmpty(_configService.Settings.OuijaPath) && 
            File.Exists(_configService.Settings.OuijaPath))
        {
            return _configService.Settings.OuijaPath;
        }

        // Check in application directory
        var appDir = Path.Combine(AppContext.BaseDirectory, "ouija-cli.exe");
        if (File.Exists(appDir))
            return appDir;

        // Check in PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (pathVar != null)
        {
            foreach (var path in pathVar.Split(Path.PathSeparator))
            {
                var fullPath = Path.Combine(path, "ouija-cli.exe");
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }
            
        throw new FileNotFoundException("ouija-cli.exe not found. Please ensure it's in the application directory or PATH.");
    }

    private string BuildArguments(SearchCriteria criteria, string engine, int batchMultiplier, int threadGroups, int cpuThreads, int cpuBatchedDigits)
    {
        var args = new List<string>();

        if (engine == "Motely (CPU)")
        {
            // CPU engine specific args
            args.AddRange(new[] { "--engine", "cpu" });
            args.AddRange(new[] { "--cpu-threads", cpuThreads.ToString() });
            args.AddRange(new[] { "--batched-digits", cpuBatchedDigits.ToString() });
        }
        else
        {
            // GPU engine specific args (default)
            args.AddRange(new[] { "--engine", "gpu" });
            args.AddRange(new[] { "--batch-multiplier", batchMultiplier.ToString() });
            args.AddRange(new[] { "--thread-groups", threadGroups.ToString() });
        }

        // Common args
        args.AddRange(new[] { "--seeds", criteria.SeedCount.ToString() });

        if (criteria.MinScore.HasValue)
            args.AddRange(new[] { "--min-score", criteria.MinScore.Value.ToString() });

        if (!string.IsNullOrEmpty(criteria.TargetPattern))
            args.AddRange(new[] { "--pattern", $"\"{criteria.TargetPattern}\"" });

        if (!string.IsNullOrEmpty(criteria.Deck))
            args.AddRange(new[] { "--deck", StripEmojis(criteria.Deck) });

        if (!string.IsNullOrEmpty(criteria.Stake))
            args.AddRange(new[] { "--stake", StripEmojis(criteria.Stake) });

        if (criteria.Needs.Any())
            args.AddRange(new[] { "--needs", string.Join(",", criteria.Needs) });

        if (criteria.Wants.Any())
            args.AddRange(new[] { "--wants", string.Join(",", criteria.Wants) });

        return string.Join(" ", args);
    }

    private static string StripEmojis(string input)
    {
        // Remove emojis and extra whitespace, keep just the text
        return Regex.Replace(input, @"[\p{So}\p{Cs}\uFE0F]", "").Trim();
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _currentProcess?.Dispose();
        _cancellationTokenSource?.Dispose();
        _processLock?.Dispose();
    }
}

// Output parser for real-time updates
public class OuijaOutputParser
{
    private readonly IProgress<SearchProgress> _progress;
    private readonly Regex _progressRegex = new(@"Progress: (\d+)%");
    private readonly Regex _resultRegex = new(@"Found seed: (\w+) score: ([\d.]+)");
    private readonly Regex _speedRegex = new(@"Speed: ([\d.]+) seeds/sec");
    private readonly DateTime _startTime = DateTime.UtcNow;

    public OuijaOutputParser(IProgress<SearchProgress> progress)
    {
        _progress = progress;
    }

    public void ParseLine(string line)
    {
        Console.WriteLine($"Ouija output: {line}");

        // Parse progress updates
        var progressMatch = _progressRegex.Match(line);
        if (progressMatch.Success)
        {
            var percent = int.Parse(progressMatch.Groups[1].Value);
            _progress.Report(new SearchProgress
            {
                PercentComplete = percent,
                Message = line,
                Elapsed = DateTime.UtcNow - _startTime
            });
            return;
        }

        // Parse speed updates
        var speedMatch = _speedRegex.Match(line);
        if (speedMatch.Success)
        {
            var speed = double.Parse(speedMatch.Groups[1].Value);
            _progress.Report(new SearchProgress
            {
                Message = line,
                SeedsPerSecond = speed,
                Elapsed = DateTime.UtcNow - _startTime
            });
            return;
        }

        // Parse results
        var resultMatch = _resultRegex.Match(line);
        if (resultMatch.Success)
        {
            var seed = resultMatch.Groups[1].Value;
            var score = double.Parse(resultMatch.Groups[2].Value);
            
            _progress.Report(new SearchProgress
            {
                Message = $"Found seed {seed}",
                NewResults = new[] 
                { 
                    new SeedResult 
                    { 
                        Seed = seed, 
                        Score = score,
                        FoundAt = DateTime.UtcNow,
                        SearchId = Guid.NewGuid().ToString("N")[..8]
                    } 
                },
                Elapsed = DateTime.UtcNow - _startTime
            });
        }
    }
}

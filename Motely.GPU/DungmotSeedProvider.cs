using System.Diagnostics;

namespace Motely.GPU;

/// <summary>
/// Seed provider that spawns dungmot.exe and reads seeds from stdout.
/// Implements IMotelySeedProvider for seamless integration with Motely search pipeline.
/// 
/// Flow:
///   1. Spawns dungmot.exe with --stream flag
///   2. Seeds stream to stdout (one per line)
///   3. Progress/stats go to stderr (forwarded to Console.Error)
///   4. NextSeed() reads from stdout pipe
/// </summary>
public sealed class DungmotSeedProvider : IMotelySeedProvider, IDisposable
{
    private readonly Process _process;
    private readonly StreamReader _stdout;
    private readonly object _lock = new();
    private bool _disposed;
    private int _seedCount;
    private bool _processExited;

    /// <summary>
    /// Number of seeds read so far. Returns -1 to indicate unknown total (streaming).
    /// </summary>
    public int SeedCount => _seedCount == 0 ? -1 : _seedCount;

    /// <summary>
    /// Whether the dungmot process is still running.
    /// </summary>
    public bool IsRunning => !_processExited && !_process.HasExited;

    /// <summary>
    /// The dungmot process exit code (only valid after process exits).
    /// </summary>
    public int? ExitCode => _processExited ? _process.ExitCode : null;

    public DungmotSeedProvider(DungmotConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = config.ExecutablePath,
                Arguments = config.ToArgumentString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        // Forward stderr to console for progress reporting
        _process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.Error.WriteLine(e.Data);
        };

        // Track process exit
        _process.Exited += (_, _) =>
        {
            _processExited = true;
        };

        Console.Error.WriteLine($"[Motely.GPU] Starting dungmot: {config}");
        Console.Error.WriteLine($"[Motely.GPU] Command: {config.ExecutablePath} {config.ToArgumentString()}");

        try
        {
            _process.Start();
            _process.BeginErrorReadLine();
            _stdout = _process.StandardOutput;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start dungmot at '{config.ExecutablePath}'. " +
                $"Ensure dungmot.exe is in PATH or specify full path. Error: {ex.Message}", 
                ex);
        }
    }

    /// <summary>
    /// Read the next seed from dungmot stdout.
    /// Returns empty span when no more seeds available.
    /// </summary>
    public ReadOnlySpan<char> NextSeed()
    {
        lock (_lock)
        {
            if (_disposed)
                return ReadOnlySpan<char>.Empty;

            try
            {
                string? line = _stdout.ReadLine();
                
                if (string.IsNullOrEmpty(line))
                {
                    // End of stream
                    return ReadOnlySpan<char>.Empty;
                }

                _seedCount++;
                return line.AsSpan();
            }
            catch (Exception)
            {
                // Process may have been killed or pipe closed
                return ReadOnlySpan<char>.Empty;
            }
        }
    }

    /// <summary>
    /// Gracefully stop the dungmot process.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_disposed || _processExited)
                return;

            try
            {
                if (!_process.HasExited)
                {
                    // Send Ctrl+C equivalent (on Windows, just kill; on Unix, send SIGINT)
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Process may have already exited
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(TimeSpan.FromSeconds(5));
                }
            }
            catch
            {
                // Best effort cleanup
            }

            _stdout.Dispose();
            _process.Dispose();

            Console.Error.WriteLine($"[Motely.GPU] dungmot finished. Seeds provided: {_seedCount}");
        }
    }
}

using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

namespace TheFool.Services;

public interface IProcessRunnerService
{
    event EventHandler<OutputReceivedEventArgs>? OutputReceived;
    event EventHandler<ProcessStateEventArgs>? ProcessStateChanged;
    
    Task<Guid> StartProcessAsync(string executable, string arguments, string? workingDirectory = null);
    Task StopProcessAsync(Guid processId);
    bool IsProcessRunning(Guid processId);
    ProcessInfo? GetProcessInfo(Guid processId);
}

public class ProcessRunnerService : IProcessRunnerService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ManagedProcess> _processes = new();
    
    public event EventHandler<OutputReceivedEventArgs>? OutputReceived;
    public event EventHandler<ProcessStateEventArgs>? ProcessStateChanged;

    public async Task<Guid> StartProcessAsync(string executable, string arguments, string? workingDirectory = null)
    {
        var processId = Guid.NewGuid();
        var managedProcess = new ManagedProcess
        {
            Id = processId,
            StartTime = DateTime.UtcNow,
            Executable = executable,
            Arguments = arguments
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process { StartInfo = startInfo };
        managedProcess.Process = process;

        // Wire up output handlers
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                OutputReceived?.Invoke(this, new OutputReceivedEventArgs
                {
                    ProcessId = processId,
                    Data = e.Data,
                    IsError = false,
                    Timestamp = DateTime.UtcNow
                });
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                OutputReceived?.Invoke(this, new OutputReceivedEventArgs
                {
                    ProcessId = processId,
                    Data = e.Data,
                    IsError = true,
                    Timestamp = DateTime.UtcNow
                });
            }
        };

        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) =>
        {
            managedProcess.EndTime = DateTime.UtcNow;
            managedProcess.ExitCode = process.ExitCode;
            ProcessStateChanged?.Invoke(this, new ProcessStateEventArgs
            {
                ProcessId = processId,
                State = ProcessState.Exited,
                ExitCode = process.ExitCode
            });
        };

        _processes[processId] = managedProcess;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            ProcessStateChanged?.Invoke(this, new ProcessStateEventArgs
            {
                ProcessId = processId,
                State = ProcessState.Running
            });

            return processId;
        }
        catch (Exception ex)
        {
            _processes.TryRemove(processId, out _);
            throw new InvalidOperationException($"Failed to start process: {ex.Message}", ex);
        }
    }

    public async Task StopProcessAsync(Guid processId)
    {
        if (_processes.TryGetValue(processId, out var managedProcess))
        {
            try
            {
                if (!managedProcess.Process.HasExited)
                {
                    // Try graceful shutdown first
                    managedProcess.Process.CloseMainWindow();
                    
                    // Wait up to 5 seconds for graceful exit
                    if (!managedProcess.Process.WaitForExit(5000))
                    {
                        // Force kill if necessary
                        managedProcess.Process.Kill(true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - process might have already exited
                Debug.WriteLine($"Error stopping process: {ex.Message}");
            }
        }
        await Task.CompletedTask;
    }

    public bool IsProcessRunning(Guid processId)
    {
        return _processes.TryGetValue(processId, out var managedProcess) 
               && managedProcess.Process != null 
               && !managedProcess.Process.HasExited;
    }

    public ProcessInfo? GetProcessInfo(Guid processId)
    {
        if (_processes.TryGetValue(processId, out var managedProcess))
        {
            return new ProcessInfo
            {
                Id = processId,
                Executable = managedProcess.Executable,
                Arguments = managedProcess.Arguments,
                StartTime = managedProcess.StartTime,
                EndTime = managedProcess.EndTime,
                IsRunning = !managedProcess.Process.HasExited,
                ExitCode = managedProcess.ExitCode
            };
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var process in _processes.Values)
        {
            try
            {
                if (!process.Process.HasExited)
                {
                    process.Process.Kill(true);
                }
                process.Process.Dispose();
            }
            catch { }
        }
        _processes.Clear();
    }

    private class ManagedProcess
    {
        public Guid Id { get; set; }
        public Process Process { get; set; } = null!;
        public string Executable { get; set; } = "";
        public string Arguments { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? ExitCode { get; set; }
    }
}

public class OutputReceivedEventArgs : EventArgs
{
    public Guid ProcessId { get; set; }
    public string Data { get; set; } = "";
    public bool IsError { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ProcessStateEventArgs : EventArgs
{
    public Guid ProcessId { get; set; }
    public ProcessState State { get; set; }
    public int? ExitCode { get; set; }
}

public enum ProcessState
{
    Starting,
    Running,
    Exited,
    Failed
}

public class ProcessInfo
{
    public Guid Id { get; set; }
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsRunning { get; set; }
    public int? ExitCode { get; set; }
}

using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using Oracle.Models;

namespace Oracle.Services;

public class PerformanceMonitor
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly PerformanceCounter? _memoryCounter;
    private readonly Process _currentProcess;

    public PerformanceMonitor()
    {
        _currentProcess = Process.GetCurrentProcess();
        
        // Performance counters are only available on Windows
        if (OperatingSystem.IsWindows())
        {
            try
            {
                _cpuCounter = CreateWindowsPerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = CreateWindowsPerformanceCounter("Memory", "Available MBytes");
            }
            catch
            {
                // Performance counters might not be available even on Windows systems
            }
        }
    }

    public PerformanceMetrics GetCurrentMetrics()
    {
        var metrics = new PerformanceMetrics();

        try
        {
            // CPU usage
            if (OperatingSystem.IsWindows() && _cpuCounter != null)
            {
                metrics.CpuUsage = GetWindowsPerformanceCounterValue(_cpuCounter);
            }

            // Memory usage (as percentage of total system memory)
            if (OperatingSystem.IsWindows() && _memoryCounter != null)
            {
                var availableMemory = GetWindowsPerformanceCounterValue(_memoryCounter) * 1024 * 1024; // Convert MB to bytes
                var totalMemory = GC.GetTotalMemory(false);
                var totalSystemMemory = availableMemory + _currentProcess.WorkingSet64;
                metrics.MemoryUsage = (double)_currentProcess.WorkingSet64 / totalSystemMemory * 100;
            }
            else
            {
                // Fallback to GC memory
                var totalMemory = GC.GetTotalMemory(false);
                metrics.MemoryUsage = totalMemory / (1024.0 * 1024.0); // MB
            }

            // GPU usage (placeholder - would need GPU-specific libraries)
            metrics.GpuUsage = 0; // TODO: Implement GPU monitoring
        }
        catch
        {
            // Return zero values if monitoring fails
        }

        return metrics;
    }

    [SupportedOSPlatform("windows")]
    private static PerformanceCounter CreateWindowsPerformanceCounter(string categoryName, string counterName, string? instanceName = null)
    {
        return new PerformanceCounter(categoryName, counterName, instanceName ?? string.Empty);
    }

    [SupportedOSPlatform("windows")]
    private static float GetWindowsPerformanceCounterValue(PerformanceCounter counter)
    {
        return counter.NextValue();
    }
}

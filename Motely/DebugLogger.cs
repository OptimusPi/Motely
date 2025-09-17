using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace Motely;

public static class DebugLogger
{
    public static bool IsEnabled { get; set; } = false;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log(string message)
    {
        if (IsEnabled)
        {
            Console.WriteLine($"[DEBUG {DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogBatch(long batchIdx, int threadId, string message)
    {
        if (IsEnabled)
        {
            Console.WriteLine($"[DEBUG BATCH {batchIdx} T{threadId}] {message}");
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void LogSeed(char* seed, int length, string context)
    {
        if (IsEnabled)
        {
            var seedStr = new string(seed, 0, length);
            Console.WriteLine($"[DEBUG SEED] {context}: {seedStr}");
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogVector(Vector512<double> vec, string name)
    {
        if (IsEnabled)
        {
            Console.WriteLine($"[DEBUG VEC] {name}: [{vec[0]:F2}, {vec[1]:F2}, {vec[2]:F2}, {vec[3]:F2}, {vec[4]:F2}, {vec[5]:F2}, {vec[6]:F2}, {vec[7]:F2}]");
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogMask(VectorMask mask, string context)
    {
        if (IsEnabled)
        {
            string maskStr = "";
            for (int i = 0; i < 8; i++)
            {
                maskStr += mask[i] ? "1" : "0";
            }
            Console.WriteLine($"[DEBUG MASK] {context}: {maskStr}");
        }
    }
}
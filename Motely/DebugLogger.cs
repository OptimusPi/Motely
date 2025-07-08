using System.Collections.Concurrent;
using System.Text;

namespace Motely;

public static class DebugLogger
{
    private static readonly ConcurrentQueue<string> _messageQueue = new();
    private static readonly object _flushLock = new();
    private static volatile bool _isEnabled = false;
    private static volatile bool _isFlushingEnabled = true;
    
    public static bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }
    
    public static bool IsFlushingEnabled
    {
        get => _isFlushingEnabled;
        set => _isFlushingEnabled = value;
    }
    
    public static void Log(string message)
    {
        if (!_isEnabled) return;
        
        _messageQueue.Enqueue($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        
        if (_isFlushingEnabled)
        {
            TryFlush();
        }
    }
    
    public static void LogFormat(string format, params object[] args)
    {
        if (!_isEnabled) return;
        
        Log(string.Format(format, args));
    }
    
    public static void TryFlush()
    {
        if (!Monitor.TryEnter(_flushLock, 0))
            return;
            
        try
        {
            var messages = new List<string>();
            while (_messageQueue.TryDequeue(out var message) && messages.Count < 100)
            {
                messages.Add(message);
            }
            
            if (messages.Count > 0)
            {
                foreach (var msg in messages)
                {
                    Console.WriteLine(msg);
                }
            }
        }
        finally
        {
            Monitor.Exit(_flushLock);
        }
    }
    
    public static void ForceFlush()
    {
        lock (_flushLock)
        {
            while (_messageQueue.TryDequeue(out var message))
            {
                Console.WriteLine(message);
            }
        }
    }
    
    public static int QueueCount => _messageQueue.Count;
}
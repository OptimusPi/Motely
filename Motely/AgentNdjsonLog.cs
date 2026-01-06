using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Motely;

internal static class AgentNdjsonLog
{
    private static readonly object Gate = new();
    private static FileStream? _stream;

    // NOTE: Debug-mode provisioned log path (NDJSON)
    private const string LogPath = @"x:\BalatroSeedOracle\external\Motely\.cursor\debug.log";

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Log(
        string hypothesisId,
        string location,
        string message,
        object? data = null,
        string runId = "pre-fix"
    )
    {
        try
        {
            var payload = new
            {
                sessionId = "debug-session",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            string json = JsonSerializer.Serialize(payload);
            byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");

            lock (Gate)
            {
                _stream ??= new FileStream(
                    LogPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite
                );
                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush(true);
            }
        }
        catch
        {
            // Never let logging affect runtime behavior/crash path
        }
    }
}


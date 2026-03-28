namespace Motely;

/// <summary>
/// Callbacks from C# to JS. Imported via Bootsharp [assembly: JSImport].
/// Methods starting with "Notify" become JS events named "On..." automatically.
/// </summary>
public interface IMotelyUI
{
    void NotifyProgress(long searched, long found, long elapsedMs);
    void NotifyResult(string seed, double score);
    void NotifyComplete(string status, int seedsFound, double highestScore);
}

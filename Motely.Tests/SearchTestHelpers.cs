using Motely;

namespace Motely.Tests;

/// <summary>
/// Helper methods for search tests to prevent hanging
/// </summary>
public static class SearchTestHelpers
{
    /// <summary>
    /// Waits for search completion with a timeout to prevent tests from hanging indefinitely
    /// </summary>
    /// <param name="search">The search to wait for</param>
    /// <param name="timeoutSeconds">Maximum time to wait in seconds (default: 30)</param>
    /// <exception cref="TimeoutException">Thrown if search doesn't complete within timeout</exception>
    public static void AwaitCompletionWithTimeout(
        this IMotelySearch search,
        int timeoutSeconds = 30
    )
    {
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var startTime = DateTime.UtcNow;

        // Poll for completion instead of blocking indefinitely
        while (search.Status == MotelySearchStatus.Running)
        {
            if (DateTime.UtcNow - startTime > timeout)
            {
                throw new TimeoutException(
                    $"Search did not complete within {timeoutSeconds} seconds. "
                        + $"Status: {search.Status}, Seeds searched: {search.TotalSeedsSearched}, "
                        + $"Matching: {search.MatchingSeeds}"
                );
            }

            Thread.Sleep(100); // Check every 100ms
        }

        // Calculate remaining time for thread join (give it at least 1 second)
        var elapsed = DateTime.UtcNow - startTime;
        var remainingTimeout = timeout - elapsed;
        var joinTimeout =
            remainingTimeout > TimeSpan.Zero ? remainingTimeout : TimeSpan.FromSeconds(1);

        // Wrap AwaitCompletion in a Task with timeout to prevent infinite blocking
        // Even if status shows Completed, Thread.Join() can still hang if threads are stuck
        var joinTask = Task.Run(() => search.AwaitCompletion());
        if (!joinTask.Wait(joinTimeout))
        {
            throw new TimeoutException(
                $"Search threads did not join within {joinTimeout.TotalSeconds:F1} seconds. "
                    + $"Status: {search.Status}, Seeds searched: {search.TotalSeedsSearched}, "
                    + $"Matching: {search.MatchingSeeds}. This may indicate a deadlock or stuck thread."
            );
        }
    }
}

using System.Net.Http.Json;

namespace Motely.DistributedWorker;

/// <summary>
/// HTTP client for the ambient work queue pool endpoint.
/// Workers connect with a shared pool token and get assigned work automatically.
/// No need to know specific session IDs.
/// </summary>
internal sealed class PoolClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _poolUrl;

    private static string NormalizePoolUrl(string poolUrl)
    {
        var trimmed = poolUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/api/search/helper", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/api/search/helper";
    }

    public PoolClient(string poolUrl)
    {
        _poolUrl = NormalizePoolUrl(poolUrl);
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>POST /api/search/helper action=request — claim one block.</summary>
    /// <param name="workerId">Worker identifier for heartbeat tracking.</param>
    /// <param name="filterId">Optional: target a specific filter. Null = any active filter ("GIMMIE WORK").</param>
    public async Task<PoolClaimResponseDto> ClaimAsync(string? workerId, string? filterId = null, CancellationToken ct = default)
    {
        var url = _poolUrl;
        var body = new PoolClaimRequestDto { WorkerId = workerId, FilterId = filterId };
        var resp = await _http.PostAsJsonAsync(url, body, WorkerJsonContext.Default.PoolClaimRequestDto, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Pool claim failed with {(int)resp.StatusCode} {resp.ReasonPhrase}: {errorBody}");
        }
        var result = await resp.Content.ReadFromJsonAsync(
            WorkerJsonContext.Default.PoolClaimResponseDto, ct
        );
        return result ?? throw new InvalidOperationException("Null pool claim response");
    }

    /// <summary>POST /api/search/helper action=submit — submit results for a completed batch range.</summary>
    public async Task<SubmitResponseDto> SubmitResultsAsync(string filterId, SubmitResultsDto results, CancellationToken ct = default)
    {
        var url = _poolUrl;
        results.FilterId = filterId; // Attach filterId to the DTO
        var resp = await _http.PostAsJsonAsync(url, results, WorkerJsonContext.Default.SubmitResultsDto, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Pool submit failed with {(int)resp.StatusCode} {resp.ReasonPhrase}: {errorBody}");
        }
        var result = await resp.Content.ReadFromJsonAsync(
            WorkerJsonContext.Default.SubmitResponseDto, ct
        );
        return result ?? throw new InvalidOperationException("Null submit response");
    }

    public void Dispose() => _http.Dispose();
}

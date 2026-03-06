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

    public PoolClient(string poolUrl, string poolToken)
    {
        _poolUrl = poolUrl.TrimEnd('/');
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", poolToken);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>POST /api/search/pool/claim — claim one block from the next filter needing help.</summary>
    public async Task<PoolClaimResponseDto> ClaimAsync(string? workerId, CancellationToken ct = default)
    {
        var url = $"{_poolUrl}/api/search/pool/claim";
        var body = new PoolClaimRequestDto { WorkerId = workerId };
        var resp = await _http.PostAsJsonAsync(url, body, WorkerJsonContext.Default.PoolClaimRequestDto, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync(
            WorkerJsonContext.Default.PoolClaimResponseDto, ct
        );
        return result ?? throw new InvalidOperationException("Null pool claim response");
    }

    /// <summary>POST /api/search/sessions/{sessionId}/results — submit results for a completed batch range.</summary>
    public async Task<SubmitResponseDto> SubmitResultsAsync(string sessionId, SubmitResultsDto results, CancellationToken ct = default)
    {
        var url = $"{_poolUrl}/api/search/sessions/{sessionId}/results";
        var resp = await _http.PostAsJsonAsync(url, results, WorkerJsonContext.Default.SubmitResultsDto, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync(
            WorkerJsonContext.Default.SubmitResponseDto, ct
        );
        return result ?? throw new InvalidOperationException("Null submit response");
    }

    public void Dispose() => _http.Dispose();
}

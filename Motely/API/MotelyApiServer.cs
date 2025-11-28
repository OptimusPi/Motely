using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.API;

/// <summary>
/// Simple HTTP API server for Motely seed searching
/// </summary>
public class MotelyApiServer
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly string _host;
    private readonly int _port;
    private readonly Action<string> _logCallback;

    public bool IsRunning => _listener?.IsListening ?? false;
    public string Url => $"http://{_host}:{_port}/";

    public MotelyApiServer(
        string host = "localhost",
        int port = 3141,
        Action<string>? logCallback = null
    )
    {
        _host = host;
        _port = port;
        _logCallback = logCallback ?? Console.WriteLine;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listener != null)
            throw new InvalidOperationException("Server is already running");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new HttpListener();
        _listener.Prefixes.Add(Url);

        try
        {
            _listener.Start();
            _logCallback($"[{DateTime.Now:HH:mm:ss}] API Server started on {Url}");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), _cts.Token);
                }
                catch (HttpListenerException)
                {
                    // GetContextAsync throws when Stop() is called
                    if (_cts.Token.IsCancellationRequested || !_listener.IsListening)
                        break;
                    throw; // Re-throw if it's a real error
                }
            }
        }
        finally
        {
            _listener.Stop();
            _listener.Close();
            _logCallback($"[{DateTime.Now:HH:mm:ss}] API Server stopped");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop(); // Force GetContextAsync() to throw and exit the loop
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Enable CORS
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            _logCallback($"[{DateTime.Now:HH:mm:ss}] {request.HttpMethod} {path}");

            if (request.HttpMethod == "GET" && path == "/")
            {
                await HandleIndexAsync(response);
            }
            else if (request.HttpMethod == "POST" && path == "/search")
            {
                response.ContentType = "application/json";
                await HandleSearchAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && path == "/analyze")
            {
                response.ContentType = "application/json";
                await HandleAnalyzeAsync(request, response);
            }
            else
            {
                response.ContentType = "application/json";
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "Not Found" });
            }
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleIndexAsync(HttpListenerResponse response)
    {
        response.ContentType = "text/html";
        response.StatusCode = 200;

        var html =
            @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Motely Seed Search</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: 'Courier New', monospace;
            background: #000;
            color: #fff;
            padding: 20px;
            line-height: 1.6;
        }
        h1 {
            color: #ff4c40;
            text-align: center;
            margin-bottom: 10px;
            font-size: 2em;
        }
        .subtitle {
            text-align: center;
            color: #0093ff;
            margin-bottom: 30px;
        }
        .container {
            max-width: 1200px;
            margin: 0 auto;
        }
        .section {
            background: #1a1a1a;
            border: 2px solid #ff4c40;
            padding: 20px;
            margin-bottom: 20px;
            border-radius: 8px;
        }
        h2 {
            color: #0093ff;
            margin-bottom: 15px;
        }
        label {
            display: block;
            color: #fff;
            margin-bottom: 5px;
            font-weight: bold;
        }
        textarea, input, select {
            width: 100%;
            background: #000;
            color: #fff;
            border: 1px solid #0093ff;
            padding: 10px;
            margin-bottom: 15px;
            font-family: 'Courier New', monospace;
            border-radius: 4px;
        }
        textarea {
            min-height: 150px;
            resize: vertical;
        }
        button {
            background: #ff4c40;
            color: #fff;
            border: none;
            padding: 12px 30px;
            font-size: 1em;
            font-family: 'Courier New', monospace;
            font-weight: bold;
            cursor: pointer;
            border-radius: 4px;
            transition: background 0.3s;
        }
        button:hover {
            background: #ff6c60;
        }
        button:active {
            background: #cc3c30;
        }
        .results {
            background: #000;
            border: 1px solid #0093ff;
            padding: 15px;
            margin-top: 15px;
            border-radius: 4px;
            min-height: 100px;
            max-height: 500px;
            overflow-y: auto;
        }
        .result-item {
            padding: 10px;
            margin: 5px 0;
            background: #1a1a1a;
            border-left: 3px solid #ff4c40;
        }
        .seed {
            color: #0093ff;
            font-weight: bold;
            font-size: 1.1em;
        }
        .score {
            color: #ff4c40;
        }
        .error {
            color: #ff4c40;
            background: #331a1a;
            padding: 10px;
            border-radius: 4px;
        }
        .loading {
            color: #0093ff;
            text-align: center;
            padding: 20px;
        }
        pre {
            white-space: pre-wrap;
            word-wrap: break-word;
        }
        .info {
            color: #888;
            font-size: 0.9em;
            margin-top: 5px;
        }
        .grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }
        @media (max-width: 768px) {
            .grid { grid-template-columns: 1fr; }
        }
        .tabs {
            display: flex;
            gap: 5px;
            margin-bottom: 10px;
        }
        .tab {
            background: #000;
            color: #888;
            border: 1px solid #0093ff;
            padding: 8px 16px;
            cursor: pointer;
            border-radius: 4px 4px 0 0;
            transition: all 0.3s;
        }
        .tab:hover {
            background: #1a1a1a;
            color: #fff;
        }
        .tab.active {
            background: #0093ff;
            color: #000;
            font-weight: bold;
        }
        .tab-content {
            display: none;
        }
        .tab-content.active {
            display: block;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>🃏 MOTELY SEED SEARCH 🃏</h1>
        <div class=""subtitle"">Balatro Seed Oracle - Find Your Perfect Run</div>

        <div class=""grid"">
            <div class=""section"">
                <h2>🔍 Search Seeds</h2>
                <label>Filter Format:</label>
                <div class=""tabs"">
                    <div class=""tab active"" onclick=""switchTab('json')"">JSON</div>
                    <div class=""tab"" onclick=""switchTab('jaml')"">JAML</div>
                </div>

                <div id=""jsonTab"" class=""tab-content active"">
                    <textarea id=""filterJson"">{
  ""must"": [
    {
      ""type"": ""Voucher"",
      ""value"": ""Telescope"",
      ""antes"": [1, 2]
    },
    {
      ""type"": ""Voucher"",
      ""value"": ""Observatory"",
      ""antes"": [2, 3]
    },
    {
      ""type"": ""SoulJoker"",
      ""value"": ""Perkeo"",
      ""antes"": [1, 2, 3],
      ""packSlots"": [0, 1, 2, 3]
    }
  ],
  ""should"": [
    {
      ""type"": ""SoulJoker"",
      ""value"": ""Perkeo"",
      ""Edition"": ""Negative"",
      ""antes"": [1, 2, 3]
    },
    {
      ""type"": ""Joker"",
      ""values"": [""Blueprint"", ""Brainstorm""],
      ""antes"": [1, 2, 3]
    },
    {
      ""type"": ""Joker"",
      ""values"": [""Blueprint"", ""Brainstorm""],
      ""Edition"": ""Negative"",
      ""antes"": [1, 2, 3]
    }
  ],
  ""deck"": ""Red"",
  ""stake"": ""White""
}</textarea>
                </div>

                <div id=""jamlTab"" class=""tab-content"">
                    <textarea id=""filterJaml"">must:
  - type: Voucher
    value: Telescope
    antes: [1, 2, 3]
  - type: Voucher
    value: Observatory
    antes: [1, 2, 3]
  - type: SoulJoker
    edition: Negative
    value: Perkeo
    antes: [1]
should:
  - type: SoulJoker
    value: Perkeo
    antes: [1]
  - type: SoulJoker
    value: Perkeo
    antes: [2]
  - type: SoulJoker
    value: Perkeo
    antes: [3]
  - type: Joker
    value: Blueprint
    antes: [1]
deck: Red
stake: White</textarea>
                </div>

                <button onclick=""searchSeeds()"">Search 1M Seeds</button>
                <div class=""info"">Note: Search uses JSON format. Types: Joker, SoulJoker, Voucher, TarotCard, SpectralCard, PlanetCard, PlayingCard, Boss, SmallBlindTag, BigBlindTag</div>
                <div id=""searchResults"" class=""results""></div>
            </div>

            <div class=""section"">
                <h2>🔬 Analyze Seed</h2>
                <label>Seed:</label>
                <input type=""text"" id=""analyzeSeed"" placeholder=""ABCD1234"" />
                <label>Deck:</label>
                <select id=""analyzeDeck"">
                    <option value=""Red"">Red</option>
                    <option value=""Blue"">Blue</option>
                    <option value=""Yellow"">Yellow</option>
                    <option value=""Green"">Green</option>
                    <option value=""Black"">Black</option>
                    <option value=""Magic"">Magic</option>
                    <option value=""Nebula"">Nebula</option>
                    <option value=""Ghost"">Ghost</option>
                    <option value=""Abandoned"">Abandoned</option>
                    <option value=""Checkered"">Checkered</option>
                    <option value=""Zodiac"">Zodiac</option>
                    <option value=""Painted"">Painted</option>
                    <option value=""Anaglyph"">Anaglyph</option>
                    <option value=""Plasma"">Plasma</option>
                    <option value=""Erratic"">Erratic</option>
                </select>
                <label>Stake:</label>
                <select id=""analyzeStake"">
                    <option value=""White"">White</option>
                    <option value=""Red"">Red</option>
                    <option value=""Green"">Green</option>
                    <option value=""Black"">Black</option>
                    <option value=""Blue"">Blue</option>
                    <option value=""Purple"">Purple</option>
                    <option value=""Orange"">Orange</option>
                    <option value=""Gold"">Gold</option>
                </select>
                <button onclick=""analyzeSeed()"">Analyze Seed</button>
                <div id=""analyzeResults"" class=""results""></div>
            </div>
        </div>
    </div>

    <script>
        async function searchSeeds() {
            const resultsDiv = document.getElementById('searchResults');
            const filterJson = document.getElementById('filterJson').value;

            resultsDiv.innerHTML = '<div class=""loading"">⏳ Searching 1,000,000 random seeds...</div>';

            try {
                const response = await fetch('/search', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: filterJson
                });

                const data = await response.json();

                if (!response.ok) {
                    resultsDiv.innerHTML = `<div class=""error"">Error: ${data.error || 'Search failed'}</div>`;
                    return;
                }

                if (data.results && data.results.length > 0) {
                    let html = '<h3>Top Results:</h3>';
                    data.results.forEach((result, i) => {
                        html += `<div class=""result-item"">
                            <div><span class=""seed"">#${i + 1}: ${result.seed}</span> - <span class=""score"">Score: ${result.score}</span></div>
                        </div>`;
                    });
                    resultsDiv.innerHTML = html;
                } else {
                    resultsDiv.innerHTML = '<div class=""info"">No results found. Try adjusting your filter or search again!</div>';
                }
            } catch (error) {
                resultsDiv.innerHTML = `<div class=""error"">Error: ${error.message}</div>`;
            }
        }

        async function analyzeSeed() {
            const resultsDiv = document.getElementById('analyzeResults');
            const seed = document.getElementById('analyzeSeed').value;
            const deck = document.getElementById('analyzeDeck').value;
            const stake = document.getElementById('analyzeStake').value;

            if (!seed) {
                resultsDiv.innerHTML = '<div class=""error"">Please enter a seed!</div>';
                return;
            }

            resultsDiv.innerHTML = '<div class=""loading"">⏳ Analyzing seed...</div>';

            try {
                const response = await fetch('/analyze', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ seed, deck, stake })
                });

                const data = await response.json();

                if (!response.ok) {
                    resultsDiv.innerHTML = `<div class=""error"">Error: ${data.error || 'Analysis failed'}</div>`;
                    return;
                }

                resultsDiv.innerHTML = `<pre>${data.analysis}</pre>`;
            } catch (error) {
                resultsDiv.innerHTML = `<div class=""error"">Error: ${error.message}</div>`;
            }
        }

        // Allow Enter key to submit
        document.getElementById('analyzeSeed').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') analyzeSeed();
        });

        // Tab switching
        function switchTab(format) {
            // Hide all tabs
            document.querySelectorAll('.tab-content').forEach(tab => tab.classList.remove('active'));
            document.querySelectorAll('.tab').forEach(tab => tab.classList.remove('active'));

            // Show selected tab
            document.getElementById(format + 'Tab').classList.add('active');
            event.target.classList.add('active');
        }
    </script>
</body>
</html>";

        var buffer = System.Text.Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

    private async Task HandleSearchAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream);
        var filterJson = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(filterJson))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "Request body cannot be empty" });
            return;
        }

        string? tempFilterFile = null;
        try
        {
            // Write filter JSON to temp file
            tempFilterFile = Path.Combine(
                Path.GetTempPath(),
                $"motely_filter_{Guid.NewGuid()}.json"
            );
            await File.WriteAllTextAsync(tempFilterFile, filterJson);

            var parameters = new JsonSearchParams
            {
                Threads = Environment.ProcessorCount,
                BatchSize = 36,  // Default batch size
                StartBatch = 0,
                EndBatch = 0,
                EnableDebug = false,
                NoFancy = true,
                Quiet = true,
                SpecificSeed = null,
                Wordlist = null,
                RandomSeeds = 1000000, // 1 million random seeds
                Cutoff = 0,
                AutoCutoff = true,
            };

            var results = new List<SearchResult>();

            // Create callback to capture results directly
            Action<MotelySeedScoreTally> resultCallback = (tally) =>
            {
                results.Add(new SearchResult { Seed = tally.Seed, Score = tally.Score });
            };

            var executor = new JsonSearchExecutor(tempFilterFile, parameters, "json", resultCallback);
            executor.Execute();

            // Return results immediately
            response.StatusCode = 200;
            await WriteJsonAsync(
                response,
                new { results = results.OrderByDescending(r => r.Score).Take(10) }
            );

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Search completed - {results.Count} results");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Search failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
        finally
        {
            // Clean up temp file
            if (tempFilterFile != null && File.Exists(tempFilterFile))
            {
                try
                {
                    File.Delete(tempFilterFile);
                }
                catch { }
            }
        }
    }

    private async Task HandleAnalyzeAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();

        var analyzeRequest = JsonSerializer.Deserialize<AnalyzeRequest>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (analyzeRequest == null || string.IsNullOrWhiteSpace(analyzeRequest.Seed))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "seed is required" });
            return;
        }

        try
        {
            var deck =
                string.IsNullOrEmpty(analyzeRequest.Deck)
                || !Enum.TryParse<MotelyDeck>(analyzeRequest.Deck, true, out var d)
                    ? MotelyDeck.Red
                    : d;

            var stake =
                string.IsNullOrEmpty(analyzeRequest.Stake)
                || !Enum.TryParse<MotelyStake>(analyzeRequest.Stake, true, out var s)
                    ? MotelyStake.White
                    : s;

            var config = new MotelySeedAnalysisConfig(analyzeRequest.Seed, deck, stake);
            var analysis = MotelySeedAnalyzer.Analyze(config);

            response.StatusCode = 200;
            await WriteJsonAsync(
                response,
                new
                {
                    seed = analyzeRequest.Seed,
                    deck = deck.ToString(),
                    stake = stake.ToString(),
                    analysis = analysis.ToString(),
                }
            );

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Analyzed seed {analyzeRequest.Seed}");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Analyze failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }
        );

        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

}

public class SearchResult
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public class AnalyzeRequest
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("stake")]
    public string? Stake { get; set; }
}

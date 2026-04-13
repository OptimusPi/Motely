#nullable enable
using Motely;
using Motely.Analysis;
using Motely.Filters;
using MotelyJaml;
using System.Runtime.InteropServices.JavaScript;

namespace Motely.BrowserWasm;

public static partial class MotelyBrowserApi
{
    private static MotelyJamlSearchBuilder _builder = null!;
    private static IMotelySearch? _currentSearch;
    private static readonly object _sync = new();

    public static void Initialize(MotelyJamlSearchBuilder builder)
    {
        _builder = builder;
    }

    [JSExport] public static string GetVersion() => _builder.GetVersion();

    [JSExport]
    public static void StartRandomSearch(string input, int randomSeedCount)
    {
        JamlConfig config;
        
        // Auto-detect Jummy vs JAML
        string trimmed = input.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("deck:"))
        {
            if (!JamlConfigLoader.TryLoad(input, out config!, out var error))
                throw new InvalidOperationException(error ?? "Invalid JAML.");
        }
        else
        {
            if (!JummyCompiler.TryCompile(input, out var jamlYaml, out var compileErr))
                throw new InvalidOperationException(compileErr ?? "Jummy compile failed.");
            if (!JamlConfigLoader.TryLoad(jamlYaml, out config!, out var loadErr))
                throw new InvalidOperationException(loadErr ?? "Invalid JAML after Jummy compile.");
        }

        JamlSearchBuilder.EnsureRunnablePlan(config);
        var settings = _builder.LoadConfig(config).Random(randomSeedCount).BuildSettings();
        
        StartSearchInternal(settings);
    }

    [JSExport]
    public static void StopSearch() { lock (_sync) { _currentSearch?.Cancel(); } }

    private static void StartSearchInternal(IMotelySearchSettings settings)
    {
        long lastSeedsSearched = 0;
        long lastMatchingSeeds = 0;

        settings.WithProgressCallback(p => {
            lastSeedsSearched = p.SeedsSearched;
            lastMatchingSeeds = p.MatchingSeeds;
            OnProgress(p.SeedsSearched, p.MatchingSeeds);
        });

        settings.WithScoredResultCallback(t => {
            OnResult(t.Seed, t.Score, t.TallyColumns.ToArray());
        });

        var search = settings.Start();
        lock (_sync) { _currentSearch?.Cancel(); _currentSearch = search; }
        
        _ = NotifyOnCompletionAsync(search, () => lastSeedsSearched, () => lastMatchingSeeds);
    }

    private static async Task NotifyOnCompletionAsync(IMotelySearch search, Func<long> getSeeds, Func<long> getMatches)
    {
        try 
        { 
            await search.WaitForCompletionAsync(); 
            OnComplete("completed", getSeeds(), getMatches()); 
        }
        catch (OperationCanceledException) 
        { 
            OnComplete("cancelled", getSeeds(), getMatches()); 
        }
        catch (Exception ex) 
        { 
            OnComplete($"error: {ex.Message}", getSeeds(), getMatches()); 
        }
        finally 
        { 
            lock (_sync) { if (ReferenceEquals(_currentSearch, search)) _currentSearch = null; } 
            search.Dispose(); 
        }
    }

    [JSImport("SearchEvents.onProgress", "Bootsharp")]
    private static partial void OnProgress([JSMarshalAs<JSType.BigInt>] long seedsSearched, [JSMarshalAs<JSType.BigInt>] long matchingSeeds);

    [JSImport("SearchEvents.onResult", "Bootsharp")]
    private static partial void OnResult(string seed, int score, int[] tally);

    [JSImport("SearchEvents.onComplete", "Bootsharp")]
    private static partial void OnComplete(string status, [JSMarshalAs<JSType.BigInt>] long totalSeedsSearched, [JSMarshalAs<JSType.BigInt>] long matchingSeeds);
}

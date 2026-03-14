using System.Text.Json;
using Microsoft.JavaScript.NodeApi;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using BlockSearchResultDto = global::Motely.BlockSearchResultDto;
using BlockSeedResultDto = global::Motely.BlockSeedResultDto;
using CapabilitiesDto = global::Motely.CapabilitiesDto;
using ErrorDto = global::Motely.ErrorDto;
using ValidateResultDto = global::Motely.ValidateResultDto;
using VersionDto = global::Motely.VersionDto;

namespace Motely.NodeAddon;

/// <summary>
/// Node addon exports for Motely - clean direct functions matching CLI pattern.
/// No optionsJson complexity - just simple async functions.
/// </summary>
[JSExport]
public static class MotelyNodeExports
{
    private static string? _cachedVersion;
    private static string[]? _cachedFeatures;

    [JSExport]
    public static Task<string> GetVersionAsync()
    {
        var dto = new VersionDto
        {
            Version = GetCachedVersion(),
            Runtime = "node-addon",
            Features = GetFeatureList(),
        };
        return Task.FromResult(JsonSerializer.Serialize(dto, MotelyJsonContext.Default.VersionDto));
    }

    [JSExport]
    public static Task<string> GetCapabilitiesAsync()
    {
        var dto = new CapabilitiesDto
        {
            Simd = IsSimdEnabled(),
            Threads = true,
            AvailableThreadCount = Environment.ProcessorCount,
            ProcessorCount = Environment.ProcessorCount,
            Runtime = "node-addon",
            Version = GetCachedVersion(),
            Timestamp = DateTime.UtcNow.ToString("O"),
        };
        return Task.FromResult(
            JsonSerializer.Serialize(dto, MotelyJsonContext.Default.CapabilitiesDto)
        );
    }

    private static bool IsSimdEnabled() => System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated;

    [JSExport]
    public static Task<string> AnalyzeSeedAsync(string seed, string deck, string stake)
    {
        var result = AnalyzeSeed(seed, deck, stake);
        return Task.FromResult(result);
    }

    private static string AnalyzeSeed(string seed, string deck, string stake)
    {
        try
        {
            if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown deck: {deck}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = $"Unknown stake: {stake}" },
                    MotelyJsonContext.Default.ErrorDto
                );

            var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
            var analysis = MotelySeedAnalyzer.Analyze(cfg);

            if (!string.IsNullOrEmpty(analysis.Error))
                return JsonSerializer.Serialize(
                    new ErrorDto { Error = analysis.Error },
                    MotelyJsonContext.Default.ErrorDto
                );

            var dto = MapAnalysisToDto(analysis, seed, deckEnum, stakeEnum);
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.SeedAnalysisDto);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ErrorDto { Error = ex.Message },
                MotelyJsonContext.Default.ErrorDto
            );
        }
    }

    [JSExport]
    public static Task<string> ValidateJamlAsync(string jamlContent)
    {
        var result = ValidateJaml(jamlContent);
        return Task.FromResult(result);
    }

    private static string ValidateJaml(string jamlContent)
    {
        try
        {
            if (
                !JamlConfigLoader.TryLoad(jamlContent, out var config, out var parseError)
                || config == null
            )
            {
                return JsonSerializer.Serialize(
                    new ValidateResultDto
                    {
                        Valid = false,
                        Error = parseError ?? "Failed to parse JAML",
                    },
                    MotelyJsonContext.Default.ValidateResultDto
                );
            }

            return JsonSerializer.Serialize(
                new ValidateResultDto
                {
                    Valid = true,
                    Name = config.Name,
                    Deck = config.Deck.ToString(),
                    Stake = config.Stake.ToString(),
                },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(
                new ValidateResultDto { Valid = false, Error = ex.Message },
                MotelyJsonContext.Default.ValidateResultDto
            );
        }
    }

    /// <summary>Run keyword search - single keyword padded to 8 chars</summary>
    [JSExport]
    public static async Task<string> RunKeywordSearchAsync(string jamlContent, string keyword, string? padding = null)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            return ErrorJson(error ?? "Invalid JAML");
        
        var results = new List<BlockSeedResultDto>();
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(4)
            .WithSeedMatchCallback(seed => results.Add(new BlockSeedResultDto { Seed = seed, Score = 0 }));
        
        var opts = new SearchOptionsDto { Keyword = keyword, Padding = padding };
        var (_, modeErr) = settings.ApplySearchMode(opts);
        if (modeErr != null) return ErrorJson(modeErr);
        
        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));
        
        return JsonSerializer.Serialize(new BlockSearchResultDto
        {
            BlockId = 0,
            SeedsSearched = (int)search.TotalSeedsSearched,
            SeedsFound = results.Count,
            Seeds = results.ToArray()
        }, MotelyJsonContext.Default.BlockSearchResultDto);
    }

    /// <summary>Run keyword search - multiple keywords</summary>
    [JSExport]
    public static async Task<string> RunKeywordsSearchAsync(string jamlContent, string[] keywords, string? padding = null)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            return ErrorJson(error ?? "Invalid JAML");
        
        var results = new List<BlockSeedResultDto>();
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(4)
            .WithSeedMatchCallback(seed => results.Add(new BlockSeedResultDto { Seed = seed, Score = 0 }));
        
        var opts = new SearchOptionsDto { Keywords = keywords, Padding = padding };
        var (_, modeErr) = settings.ApplySearchMode(opts);
        if (modeErr != null) return ErrorJson(modeErr);
        
        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));
        
        return JsonSerializer.Serialize(new BlockSearchResultDto
        {
            BlockId = 0,
            SeedsSearched = (int)search.TotalSeedsSearched,
            SeedsFound = results.Count,
            Seeds = results.ToArray()
        }, MotelyJsonContext.Default.BlockSearchResultDto);
    }

    /// <summary>Run random seed search</summary>
    [JSExport]
    public static async Task<string> RunRandomSearchAsync(string jamlContent, int count)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            return ErrorJson(error ?? "Invalid JAML");
        
        var results = new List<BlockSeedResultDto>();
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(4)
            .WithSeedMatchCallback(seed => results.Add(new BlockSeedResultDto { Seed = seed, Score = 0 }));
        
        var opts = new SearchOptionsDto { RandomSeeds = count };
        var (_, modeErr) = settings.ApplySearchMode(opts);
        if (modeErr != null) return ErrorJson(modeErr);
        
        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));
        
        return JsonSerializer.Serialize(new BlockSearchResultDto
        {
            BlockId = 0,
            SeedsSearched = (int)search.TotalSeedsSearched,
            SeedsFound = results.Count,
            Seeds = results.ToArray()
        }, MotelyJsonContext.Default.BlockSearchResultDto);
    }

    /// <summary>Run palindrome search</summary>
    [JSExport]
    public static async Task<string> RunPalindromeSearchAsync(string jamlContent)
    {
        if (!JamlConfigLoader.TryLoad(jamlContent, out var config, out var error))
            return ErrorJson(error ?? "Invalid JAML");
        
        var results = new List<BlockSeedResultDto>();
        var settings = JamlSearchBuilder.CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(Environment.ProcessorCount)
            .WithBatchCharacterCount(4)
            .WithSeedMatchCallback(seed => results.Add(new BlockSeedResultDto { Seed = seed, Score = 0 }));
        
        var opts = new SearchOptionsDto { Palindrome = true };
        var (_, modeErr) = settings.ApplySearchMode(opts);
        if (modeErr != null) return ErrorJson(modeErr);
        
        using var search = settings.CreateSearch();
        await Task.Run(() => search.Start(CancellationToken.None));
        
        return JsonSerializer.Serialize(new BlockSearchResultDto
        {
            BlockId = 0,
            SeedsSearched = (int)search.TotalSeedsSearched,
            SeedsFound = results.Count,
            Seeds = results.ToArray()
        }, MotelyJsonContext.Default.BlockSearchResultDto);
    }

    /// <summary>Run one block of sequential search. Returns JSON BlockSearchResultDto or ErrorDto.</summary>
    [JSExport]
    public static async Task<string> ProcessBlockAsync(string jamlContent, int blockId)
    {
        try
        {
            var result = await ProcessBlockRunner.ProcessBlockAsync(jamlContent, blockId).ConfigureAwait(false);
            if (result == null)
                return ErrorJson("Invalid JAML or blockId out of range");
            var dto = new BlockSearchResultDto
            {
                BlockId = result.BlockId,
                SeedsSearched = result.SeedsSearched,
                SeedsFound = result.SeedsFound,
                Seeds = result.Seeds
                    .Select(s => new BlockSeedResultDto { Seed = s.Seed, Score = s.Score })
                    .ToArray(),
            };
            return JsonSerializer.Serialize(dto, MotelyJsonContext.Default.BlockSearchResultDto);
        }
        catch (Exception ex)
        {
            return ErrorJson(ex.Message);
        }
    }


    private static string ErrorJson(string message) =>
        JsonSerializer.Serialize(
            new ErrorDto { Error = message },
            MotelyJsonContext.Default.ErrorDto
        );


    private static string GetCachedVersion() =>
        _cachedVersion ??= MotelyBuildVersion.For(typeof(MotelyNodeExports).Assembly);

    private static string[] GetFeatureList()
    {
        if (_cachedFeatures is not null)
            return _cachedFeatures;
        var features = new List<string> { "analyzer", "jaml-search", "jaml-validate" };
        if (IsSimdEnabled())
            features.Add("simd");
        features.Add("threads");
        _cachedFeatures = features.ToArray();
        return _cachedFeatures;
    }

    private static SeedAnalysisDto MapAnalysisToDto(
        MotelySeedAnalysis analysis,
        string seed,
        MotelyDeck deck,
        MotelyStake stake
    )
    {
        return new SeedAnalysisDto
        {
            Seed = seed,
            Deck = deck.ToString(),
            Stake = stake.ToString(),
            Error = analysis.Error,
            ErraticDeckComposition =
                analysis.ErraticDeckComposition?.Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ) ?? [],
            Antes = analysis
                .Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = a.Boss.ToString(),
                    Voucher = a.Voucher.ToString(),
                    SmallBlindTag = a.SmallBlindTag.ToString(),
                    BigBlindTag = a.BigBlindTag.ToString(),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a
                        .ShopQueue.Select(item => new ShopItemDto
                        {
                            Id = item.Type.ToString(),
                            Name = item.ToString(),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(p => new PackDto
                        {
                            Type = p.Type.ToString(),
                            Items = p.Items.Select(i => i.ToString()).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}

using System.Text.Json;
using Motely;
using Motely.Analysis;
using Motely.Filters;
using Motely.Wasi;

/// <summary>
/// WASI entry point: reads JSON-RPC from stdin, writes JSON to stdout (NDJSON).
/// Uses WasiJsonContext for AOT-safe serialization (no IL2026 warnings).
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--version")
        {
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            Console.WriteLine(
                JsonSerializer.Serialize(
                    new WasiCapabilitiesDto { Version = version, Runtime = "wasi-wasm" },
                    WasiJsonContext.Default.WasiCapabilitiesDto
                )
            );
            return;
        }

        // REPL mode: read NDJSON lines from stdin
        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            string result = ProcessCommand(line);
            Console.WriteLine(result);
            Console.Out.Flush();
        }
    }

    private static string ProcessCommand(string json)
    {
        try
        {
            var req = JsonSerializer.Deserialize(json, WasiJsonContext.Default.RpcRequest);
            if (req == null)
                return Error("Failed to parse request");

            return req.Method switch
            {
                "validate_jaml" => HandleValidateJaml(req.Params),
                "analyze_seed" => HandleAnalyzeSeed(req.Params),
                "get_capabilities" => HandleGetCapabilities(),
                "search" => HandleSearch(req.Params),
                _ => Error($"Unknown method: {req.Method}"),
            };
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    private static string HandleValidateJaml(JsonParamsDto? p)
    {
        var jaml = p?.Jaml ?? "";
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var parseError) || config == null)
            return Result(
                new WasiValidateResultDto
                {
                    Valid = false,
                    Error = parseError ?? "Failed to parse JAML",
                }
            );

        return Result(
            new WasiValidateResultDto
            {
                Valid = true,
                Name = config.Name,
                Deck = config.Deck.ToString(),
                Stake = config.Stake.ToString(),
            }
        );
    }

    private static string HandleAnalyzeSeed(JsonParamsDto? p)
    {
        var seed = p?.Seed ?? "";
        var deck = p?.Deck ?? "";
        var stake = p?.Stake ?? "";

        if (!Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum))
            return Error($"Unknown deck: {deck}");
        if (!Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum))
            return Error($"Unknown stake: {stake}");

        var cfg = new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum);
        var analysis = MotelySeedAnalyzer.Analyze(cfg);

        if (!string.IsNullOrEmpty(analysis.Error))
            return Error(analysis.Error);

        var dto = new WasiSeedAnalysisDto
        {
            Seed = seed,
            Deck = deck,
            Stake = stake,
            Antes = analysis
                .Antes.Select(a => new WasiAnteDto
                {
                    Ante = a.Ante,
                    Boss = a.Boss.ToString(),
                    Voucher = a.Voucher.ToString(),
                    SmallBlindTag = a.SmallBlindTag.ToString(),
                    BigBlindTag = a.BigBlindTag.ToString(),
                    ShopQueue = a
                        .ShopQueue.Select(i => new WasiShopItemDto
                        {
                            Id = i.Type.ToString(),
                            Name = i.ToString(),
                        })
                        .ToArray(),
                    Packs = a
                        .Packs.Select(pk => new WasiPackDto
                        {
                            Type = pk.Type.ToString(),
                            Items = pk.Items.Select(i => i.ToString()).ToArray(),
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
        return Result(dto);
    }

    private static string HandleSearch(JsonParamsDto? p)
    {
        var jaml = p?.Jaml ?? "";
        if (!JamlConfigLoader.TryLoad(jaml, out var config, out var parseError) || config == null)
            return Error(parseError ?? "Failed to parse JAML");

        var randomSeeds = p?.RandomSeeds ?? 1000;
        var searchId = "search-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int resultCount = 0;

        var settings = JamlSearchBuilder
            .CreateSettings(config)
            .WithDeck(config.Deck)
            .WithStake(config.Stake)
            .WithThreadCount(1) // WASI is single-threaded
            .WithBatchCharacterCount(1)
            .WithSeedMatchCallback(seed =>
            {
                resultCount++;
                var res = new WasiSearchResultDto { Seed = seed, Score = 100 };
                Console.WriteLine(
                    JsonSerializer.Serialize(res, WasiJsonContext.Default.WasiSearchResultDto)
                );
                Console.Out.Flush();
            })
            .WithProgressCallback(prog =>
            {
                var progDto = new WasiSearchProgressDto
                {
                    SeedsSearched = prog.SeedsSearched,
                    MatchingSeeds = resultCount,
                    ElapsedMs = (long)prog.ElapsedTime.TotalMilliseconds,
                    ResultCount = resultCount,
                };
                Console.WriteLine(
                    JsonSerializer.Serialize(progDto, WasiJsonContext.Default.WasiSearchProgressDto)
                );
                Console.Out.Flush();
            });

        if (p?.Cutoff != null && int.TryParse(p.Cutoff, out int cutoffInt))
        {
            // Simple cutoff interpretation as max random seeds (could also be specific seed search)
            settings = settings.WithRandomSearch(Math.Min(randomSeeds, cutoffInt));
        }
        else
        {
            settings = settings.WithRandomSearch(randomSeeds);
        }

        var search = settings.Start(CancellationToken.None);
        search.AwaitCompletion();

        // One final progress update
        var finalProg = new WasiSearchProgressDto
        {
            SeedsSearched = search.TotalSeedsSearched,
            MatchingSeeds = resultCount,
            ElapsedMs = (long)search.ElapsedTime.TotalMilliseconds,
            ResultCount = resultCount,
        };
        Console.WriteLine(
            JsonSerializer.Serialize(finalProg, WasiJsonContext.Default.WasiSearchProgressDto)
        );

        var completeDto = new WasiSearchCompleteDto { SearchId = searchId };
        Console.WriteLine(
            JsonSerializer.Serialize(completeDto, WasiJsonContext.Default.WasiSearchCompleteDto)
        );
        Console.Out.Flush();

        // Return empty string as we already streamed the result via stdout
        return "";
    }

    private static string HandleGetCapabilities()
    {
        return Result(
            new WasiCapabilitiesDto
            {
                Runtime = "wasi-wasm",
                Simd =
#if NET10_0_OR_GREATER
                System.Runtime.Intrinsics.Vector128.IsHardwareAccelerated,
#else
                    false,
#endif
                Threads = false,
                ProcessorCount = Environment.ProcessorCount,
                Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            }
        );
    }

    // ── Serialization helpers using WasiJsonContext ──

    private static string Result(WasiValidateResultDto dto) =>
        "{\"result\":"
        + JsonSerializer.Serialize(dto, WasiJsonContext.Default.WasiValidateResultDto)
        + "}";

    private static string Result(WasiSeedAnalysisDto dto) =>
        "{\"result\":"
        + JsonSerializer.Serialize(dto, WasiJsonContext.Default.WasiSeedAnalysisDto)
        + "}";

    private static string Result(WasiCapabilitiesDto dto) =>
        "{\"result\":"
        + JsonSerializer.Serialize(dto, WasiJsonContext.Default.WasiCapabilitiesDto)
        + "}";

    private static string Error(string message) =>
        JsonSerializer.Serialize(
            new WasiErrorDto { Error = message },
            WasiJsonContext.Default.WasiErrorDto
        );
}

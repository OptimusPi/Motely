using System.Text.Json;
using System.Text.Json.Serialization;
using McMaster.Extensions.CommandLineUtils;
using Motely.Analysis;
using Motely.Filters;

namespace Motely;

partial class Program
{
    private static readonly CancellationTokenSource _cts = new();

    static int Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cts.Cancel(); };

        var app = new CommandLineApplication
        {
            Name = "Motely",
            Description = "Motely - Balatro Seed Searcher",
            OptionsComparison = StringComparison.OrdinalIgnoreCase,
        };
        app.HelpOption("-?|-h|--help");

        var jamlOption = app.Option<string>("--jaml <JAML>", "JAML config file", CommandOptionType.SingleValue);
        var analyzeOption = app.Option<string>("--analyze <SEED>", "Analyze a specific seed", CommandOptionType.SingleValue);
        var outputJsonOption = app.Option("--output-json", "Output analysis as JSON", CommandOptionType.NoValue);
        var threadsOption = app.Option<int>("--threads <N>", "Thread count", CommandOptionType.SingleValue);
        var batchSizeOption = app.Option<int>("--batchSize <N>", "Batch character count", CommandOptionType.SingleValue);
        var startBatchOption = app.Option<long>("--startBatch <N>", "Starting batch index", CommandOptionType.SingleValue);
        var endBatchOption = app.Option<long>("--endBatch <N>", "Ending batch index", CommandOptionType.SingleValue);
        var randomOption = app.Option<int>("--random <N>", "Random seed count", CommandOptionType.SingleValue);
        var palindromeOption = app.Option("--palindrome", "Palindrome seeds", CommandOptionType.NoValue);
        var seedOption = app.Option<string>("--seed <SEED>", "Single seed to test", CommandOptionType.SingleValue);
        var seedsOption = app.Option<string>("--seeds <LIST>", "Comma-separated seeds", CommandOptionType.SingleValue);
        var deckOption = app.Option<string>("--deck <DECK>", "Deck override", CommandOptionType.SingleValue);
        var stakeOption = app.Option<string>("--stake <STAKE>", "Stake override", CommandOptionType.SingleValue);
        var cutoffOption = app.Option<string>("--cutoff <VALUE>", "Minimum score to print, or 'auto' to only show new highs", CommandOptionType.SingleValue);
        var keywordOption = app.Option<string>("--keyword <WORD>", "Search seeds containing this keyword (pads to 8 chars)", CommandOptionType.SingleValue);

        threadsOption.DefaultValue = Environment.ProcessorCount;
        batchSizeOption.DefaultValue = 2;

        app.OnExecute(() =>
        {
            if (args.Length == 0) { app.ShowHelp(); return 0; }

            // --analyze mode
            if (analyzeOption.HasValue())
                return ExecuteAnalyze(analyzeOption.ParsedValue,
                    deckOption.HasValue() ? deckOption.Value()! : "Red",
                    stakeOption.HasValue() ? stakeOption.Value()! : "White",
                    outputJsonOption.HasValue());

            // --jaml mode
            if (!jamlOption.HasValue()) { Console.Error.WriteLine("Error: --jaml <path> required."); return 1; }

            if (!JamlConfigLoader.TryLoadFromFile(jamlOption.ParsedValue, out var config, out var loadError))
            { Console.Error.WriteLine($"Error: {loadError}"); return 1; }

            if (!config.HasAnyClauses) { Console.Error.WriteLine("Error: no clauses in JAML."); return 1; }

            var deck = deckOption.HasValue() && Enum.TryParse<MotelyDeck>(deckOption.Value(), true, out var d) ? d : config.Deck;
            var stake = stakeOption.HasValue() && Enum.TryParse<MotelyStake>(stakeOption.Value(), true, out var s) ? s : config.Stake;
            int threads = threadsOption.HasValue() ? threadsOption.ParsedValue : Environment.ProcessorCount;
            int batchSize = batchSizeOption.HasValue() ? batchSizeOption.ParsedValue : 2;

            var plan = JamlSearchBuilder.CreatePlan(config);
            var settings = plan.Settings
                .WithDeck(deck).WithStake(stake)
                .WithThreadCount(threads).WithBatchCharacterCount(batchSize);

            if (startBatchOption.HasValue()) settings.WithStartBatchIndex(startBatchOption.ParsedValue);
            if (endBatchOption.HasValue()) settings.WithEndBatchIndex(endBatchOption.ParsedValue);

            if (seedOption.HasValue()) settings.WithListSearch([seedOption.ParsedValue.ToUpperInvariant()]);
            else if (seedsOption.HasValue()) settings.WithListSearch(
                seedsOption.ParsedValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.ToUpperInvariant()));
            else if (keywordOption.HasValue())
            {
                string kw = keywordOption.ParsedValue.ToUpperInvariant();
                int padLen = MotelyCore.MaxSeedLength - kw.Length;
                if (padLen < 0) { Console.Error.WriteLine($"Error: keyword '{kw}' is too long (max {MotelyCore.MaxSeedLength} chars)."); return 1; }
                settings.WithListSearch(MotelyCore.GeneratePaddedSeeds(kw, padLen));
            }
            else if (randomOption.HasValue()) settings.WithRandomSearch(randomOption.ParsedValue);
            else if (palindromeOption.HasValue()) settings.WithPalindromeSearch();
            else settings.WithSequentialSearch();

            // CLI output — seeds to stdout, progress to stderr
            // Parse cutoff: number = fixed threshold, "best" = only print new highs
            bool cutoffBest = false;
            int cutoffNum = 0;
            if (cutoffOption.HasValue())
            {
                string cutoffVal = cutoffOption.Value()!;
                if (cutoffVal.Equals("best", StringComparison.OrdinalIgnoreCase) ||
                    cutoffVal.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    cutoffBest = true;
                else if (!int.TryParse(cutoffVal, out cutoffNum))
                { Console.Error.WriteLine($"Error: --cutoff must be a number or 'auto', got '{cutoffVal}'."); return 1; }
            }

            int bestScoreSoFar = 0;
            object bestLock = new();

            settings.WithSeedMatchCallback(line =>
            {
                // Parse score from format: SEED,SCORE,...
                int firstComma = line.IndexOf(',');
                if (firstComma < 0) { Console.WriteLine(line); return; }

                int secondComma = line.IndexOf(',', firstComma + 1);
                var scoreSpan = secondComma >= 0
                    ? line.AsSpan(firstComma + 1, secondComma - firstComma - 1)
                    : line.AsSpan(firstComma + 1);

                if (!int.TryParse(scoreSpan, out int score)) { Console.WriteLine(line); return; }

                if (cutoffBest)
                {
                    lock (bestLock)
                    {
                        if (score >= bestScoreSoFar)
                        {
                            bestScoreSoFar = score;
                            Console.WriteLine(line);
                        }
                    }
                }
                else if (score >= cutoffNum)
                {
                    Console.WriteLine(line);
                }
            });
            settings.WithProgressMessageCallback(msg => Console.Error.WriteLine(msg));

            var header = "SEED,SCORE";
            foreach (var l in plan.MustLabels)   header += $",\"{l}\"";
            foreach (var l in plan.ShouldLabels) header += $",\"{l}\"";
            Console.WriteLine(header);

            Console.Error.WriteLine($"Motely: {config.Name ?? jamlOption.ParsedValue} | {deck} {stake} | threads={threads} batch={batchSize}");

            using var search = settings.Start(_cts.Token);
            search.AwaitCompletion();

            bool cancelled = _cts.Token.IsCancellationRequested;
            PrintSummary(search, batchSize, cancelled);
            return cancelled ? 1 : 0;
        });

        try { return app.Execute(args); }
        catch (UnrecognizedCommandParsingException ex) { Console.Error.WriteLine($"Error: {ex.Message}"); return 1; }
        catch (CommandParsingException ex) { Console.Error.WriteLine($"Error: {ex.Message}"); return 1; }
    }

    // ── Summary ──

    static void PrintSummary(IMotelySearch search, int batchSize, bool cancelled)
    {
        Console.Out.Flush();
        Console.WriteLine();
        Console.WriteLine(cancelled ? "STOPPED" : "COMPLETED");
        Console.WriteLine($"  Seeds: {search.TotalSeedsSearched:N0} searched, {search.MatchingSeeds} matched");
        Console.WriteLine($"  Time:  {search.ElapsedTime:hh\\:mm\\:ss\\.fff}");
        double speed = search.ElapsedTime.TotalSeconds > 0 ? search.TotalSeedsSearched / search.ElapsedTime.TotalSeconds : 0;
        Console.WriteLine($"  Speed: {speed:N0} seeds/sec");
        if (search.IsSequentialBatchSearch)
        {
            long max = (long)Math.Pow(35, 8 - batchSize);
            double pct = max > 0 ? (double)search.CompletedBatchCount * 100.0 / max : 0;
            Console.WriteLine($"  Batch: {search.CompletedBatchCount:N0} / {max:N0} ({pct:F4}%)");
            if (cancelled)
                Console.WriteLine($"  Resume: --startBatch {search.CompletedBatchCount}");
        }
    }

    // ── Analyze ──

    static int ExecuteAnalyze(string seed, string deckName, string stakeName, bool json)
    {
        if (!Enum.TryParse<MotelyDeck>(deckName, true, out var d)) { Console.Error.WriteLine($"Invalid deck: {deckName}"); return 1; }
        if (!Enum.TryParse<MotelyStake>(stakeName, true, out var s)) { Console.Error.WriteLine($"Invalid stake: {stakeName}"); return 1; }

        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, d, s));

        if (json)
        {
            var erratic = analysis.ErraticDeckComposition?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
            var dto = new SeedAnalysisDto
            {
                Seed = seed, Deck = d.ToString(), Stake = s.ToString(),
                ErraticDeckComposition = erratic,
                Twos = erratic.Count(c => c.StartsWith("2_")),
                Error = analysis.Error,
                Antes = analysis.Antes.Select(a => new AnteAnalysisDto
                {
                    Ante = a.Ante,
                    Boss = FormatUtils.FormatBoss(a.Boss),
                    Voucher = FormatUtils.FormatVoucher(a.Voucher),
                    SmallBlindTag = FormatUtils.FormatTag(a.SmallBlindTag),
                    BigBlindTag = FormatUtils.FormatTag(a.BigBlindTag),
                    DrawOrder = a.DrawOrder ?? "",
                    ShopQueue = a.ShopQueue.Select(item => new ShopItemDto { Id = item.ToString(), Name = FormatUtils.FormatItem(item) }).ToArray(),
                    Packs = a.Packs.Select(p => new PackDto { Type = FormatUtils.FormatPackName(p.Type), Items = p.Items.Select(FormatUtils.FormatItem).ToArray() }).ToArray(),
                }).ToArray(),
            };
            Console.WriteLine(JsonSerializer.Serialize(dto, AnalysisJsonContext.Default.SeedAnalysisDto));
        }
        else
        {
            Console.WriteLine($"Analyzing: {seed} | {d} {s}");
            Console.Write(analysis);
        }
        return 0;
    }
}

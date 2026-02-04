using System.Text.Json;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;

namespace Motely.MCP.McpProtocol;

/// <summary>
/// MCP Protocol Server - LOCAL tools only, no cloud dependencies
/// </summary>
public class McpProtocolServer
{
    private readonly ILogger<McpProtocolServer> _logger;
    private readonly string _jamlFiltersDir;

    public McpProtocolServer(ILogger<McpProtocolServer> logger)
    {
        _logger = logger;
        _jamlFiltersDir = FindJamlFiltersDirectory();
    }

    private static string FindJamlFiltersDirectory()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "JamlFilters"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "JamlFilters"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "JamlFilters"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "JamlFilters"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
                return fullPath;
        }

        var defaultPath = Path.Combine(Directory.GetCurrentDirectory(), "JamlFilters");
        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    public Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
    {
        try
        {
            var response = request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => HandleToolCall(request),
                "resources/list" => HandleResourcesList(request),
                "resources/read" => HandleResourceRead(request),
                _ => JsonRpcResponse.Error(request.Id, -32601, $"Method not found: {request.Method}"),
            };
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling request: {Method}", request.Method);
            return Task.FromResult(JsonRpcResponse.Error(request.Id, -32603, $"Internal error: {ex.Message}"));
        }
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        return JsonRpcResponse.Success(request.Id, new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { tools = new { }, resources = new { } },
            serverInfo = new { name = "motely-jaml", version = "1.0.0" }
        });
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new object[]
        {
            new {
                name = "list_jaml_filters",
                description = "List all JAML filter files",
                inputSchema = new { type = "object", properties = new { } }
            },
            new {
                name = "read_jaml_file",
                description = "Read a JAML filter file",
                inputSchema = new {
                    type = "object",
                    properties = new { path = new { type = "string", description = "Filename or path" } },
                    required = new[] { "path" }
                }
            },
            new {
                name = "write_jaml_file",
                description = "Write a JAML filter file (validates first)",
                inputSchema = new {
                    type = "object",
                    properties = new {
                        path = new { type = "string", description = "Filename or path" },
                        content = new { type = "string", description = "JAML content" }
                    },
                    required = new[] { "path", "content" }
                }
            },
            new {
                name = "validate_jaml",
                description = "Validate JAML content",
                inputSchema = new {
                    type = "object",
                    properties = new { content = new { type = "string", description = "JAML to validate" } },
                    required = new[] { "content" }
                }
            },
            new {
                name = "run_jaml_filter",
                description = "Run a JAML filter to find matching seeds",
                inputSchema = new {
                    type = "object",
                    properties = new {
                        jaml = new { type = "string", description = "JAML content or filename" },
                        maxResults = new { type = "integer", description = "Max results (default: 10)" },
                        seedCount = new { type = "integer", description = "Seeds to search (default: 100000)" }
                    },
                    required = new[] { "jaml" }
                }
            },
            new {
                name = "analyze_seed",
                description = "Analyze a Balatro seed - see all jokers, vouchers, tags, packs",
                inputSchema = new {
                    type = "object",
                    properties = new {
                        seed = new { type = "string", description = "Seed like 'ALEEB'" },
                        deck = new { type = "string", description = "Deck (default: Red)" },
                        stake = new { type = "string", description = "Stake (default: White)" }
                    },
                    required = new[] { "seed" }
                }
            },
            new {
                name = "verify_seed",
                description = "Check if a seed matches a JAML filter",
                inputSchema = new {
                    type = "object",
                    properties = new {
                        seed = new { type = "string", description = "Seed to check" },
                        jaml = new { type = "string", description = "JAML content or filename" }
                    },
                    required = new[] { "seed", "jaml" }
                }
            }
        };

        return JsonRpcResponse.Success(request.Id, new { tools });
    }

    private JsonRpcResponse HandleToolCall(JsonRpcRequest request)
    {
        var paramsJson = request.Params?.ToString() ?? "{}";
        var callParams = JsonSerializer.Deserialize<JsonElement>(paramsJson);
        
        var name = callParams.GetProperty("name").GetString() ?? "";
        var args = callParams.TryGetProperty("arguments", out var argsEl) ? argsEl : new JsonElement();

        try
        {
            object result = name switch
            {
                "list_jaml_filters" => ListJamlFilters(),
                "read_jaml_file" => ReadJamlFile(args),
                "write_jaml_file" => WriteJamlFile(args),
                "validate_jaml" => ValidateJaml(args),
                "run_jaml_filter" => RunJamlFilter(args),
                "analyze_seed" => AnalyzeSeed(args),
                "verify_seed" => VerifySeed(args),
                _ => throw new ArgumentException($"Unknown tool: {name}")
            };

            return JsonRpcResponse.Success(request.Id, new
            {
                content = new[] { new { type = "text", text = JsonSerializer.Serialize(result) } }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool error: {Name}", name);
            return JsonRpcResponse.Error(request.Id, -32603, ex.Message);
        }
    }

    private object ListJamlFilters()
    {
        var files = Directory.Exists(_jamlFiltersDir)
            ? Directory.GetFiles(_jamlFiltersDir, "*.jaml")
            : Array.Empty<string>();

        return new
        {
            directory = _jamlFiltersDir,
            count = files.Length,
            filters = files.Select(f => Path.GetFileName(f)).OrderBy(f => f).ToArray()
        };
    }

    private object ReadJamlFile(JsonElement args)
    {
        var path = GetString(args, "path");
        var fullPath = ResolvePath(path);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {path}");

        var content = File.ReadAllText(fullPath);
        var isValid = JamlConfigLoader.TryLoadFromJamlString(content, out var config, out var error);

        return new
        {
            path = fullPath,
            content,
            isValid,
            error,
            name = config?.Name,
            deck = config?.Deck,
            stake = config?.Stake
        };
    }

    private object WriteJamlFile(JsonElement args)
    {
        var path = GetString(args, "path");
        var content = GetString(args, "content");
        var fullPath = ResolvePath(path);

        // Validate first
        if (!JamlConfigLoader.TryLoadFromJamlString(content, out _, out var error))
            return new { success = false, error = $"Invalid JAML: {error}" };

        File.WriteAllText(fullPath, content);
        return new { success = true, path = fullPath };
    }

    private object ValidateJaml(JsonElement args)
    {
        var content = GetString(args, "content");
        var isValid = JamlConfigLoader.TryLoadFromJamlString(content, out var config, out var error);

        return new
        {
            isValid,
            error,
            name = config?.Name,
            deck = config?.Deck,
            stake = config?.Stake,
            clauseCount = (config?.Must?.Count ?? 0) + (config?.Should?.Count ?? 0) + (config?.MustNot?.Count ?? 0)
        };
    }

    private object RunJamlFilter(JsonElement args)
    {
        var jaml = GetString(args, "jaml");
        var maxResults = GetInt(args, "maxResults", 10);
        var seedCount = GetInt(args, "seedCount", 100000);

        // Load JAML (file or content)
        string jamlContent;
        if (jaml.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase))
        {
            var fullPath = ResolvePath(jaml);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File not found: {jaml}");
            jamlContent = File.ReadAllText(fullPath);
        }
        else
        {
            jamlContent = jaml;
        }

        if (!JamlConfigLoader.TryLoadFromJamlString(jamlContent, out var config, out var error) || config == null)
            throw new ArgumentException($"Invalid JAML: {error}");

        var results = new List<MotelySeedScoreTally>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var executor = new JsonSearchExecutor(config, new JsonSearchParams
        {
            RandomSeeds = seedCount,
            Quiet = true,
            Cutoff = 1,
            Threads = Environment.ProcessorCount,
            MaxResults = maxResults
        }, r => { if (results.Count < maxResults) results.Add(r); });

        executor.Execute();
        sw.Stop();

        return new
        {
            seedsSearched = seedCount,
            elapsedMs = sw.ElapsedMilliseconds,
            resultCount = results.Count,
            results = results.Select(r => new { seed = r.Seed, score = r.Score }).ToArray()
        };
    }

    private object AnalyzeSeed(JsonElement args)
    {
        var seed = GetString(args, "seed");
        var deck = GetString(args, "deck", "Red");
        var stake = GetString(args, "stake", "White");

        Enum.TryParse<MotelyDeck>(deck, true, out var deckEnum);
        Enum.TryParse<MotelyStake>(stake, true, out var stakeEnum);

        var analysis = MotelySeedAnalyzer.Analyze(new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum));

        return new
        {
            seed,
            deck,
            stake,
            antes = analysis.Antes.Select(a => new
            {
                ante = a.Ante,
                boss = FormatUtils.FormatBoss(a.Boss),
                voucher = FormatUtils.FormatVoucher(a.Voucher),
                tags = new[] { FormatUtils.FormatTag(a.SmallBlindTag), FormatUtils.FormatTag(a.BigBlindTag) },
                shop = a.ShopQueue.Select(FormatUtils.FormatItem).ToArray(),
                packs = a.Packs.Select(p => new
                {
                    type = FormatUtils.FormatPackName(p.Type),
                    items = p.Items.Select(FormatUtils.FormatItem).ToArray()
                }).ToArray()
            }).ToArray()
        };
    }

    private object VerifySeed(JsonElement args)
    {
        var seed = GetString(args, "seed");
        var jaml = GetString(args, "jaml");

        string jamlContent = jaml.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase)
            ? File.ReadAllText(ResolvePath(jaml))
            : jaml;

        if (!JamlConfigLoader.TryLoadFromJamlString(jamlContent, out var config, out var error) || config == null)
            throw new ArgumentException($"Invalid JAML: {error}");

        var results = new List<MotelySeedScoreTally>();
        var executor = new JsonSearchExecutor(config, new JsonSearchParams
        {
            SpecificSeed = seed,
            Quiet = true,
            Cutoff = 0,
            Threads = 1
        }, r => results.Add(r));

        executor.Execute();
        var matched = results.Count > 0;

        return new
        {
            seed,
            matched,
            score = results.FirstOrDefault().Score,
            message = matched ? $"✅ Seed matches (score: {results[0].Score})" : "❌ Seed does not match"
        };
    }

    private JsonRpcResponse HandleResourcesList(JsonRpcRequest request)
    {
        var resources = new[]
        {
            new { uri = "jaml://schema", name = "JAML Schema", mimeType = "application/json" }
        };
        return JsonRpcResponse.Success(request.Id, new { resources });
    }

    private JsonRpcResponse HandleResourceRead(JsonRpcRequest request)
    {
        var paramsJson = request.Params?.ToString() ?? "{}";
        var readParams = JsonSerializer.Deserialize<JsonElement>(paramsJson);
        var uri = readParams.GetProperty("uri").GetString() ?? "";

        if (uri == "jaml://schema")
        {
            var schemaPath = Path.Combine(_jamlFiltersDir, "..", "jaml.schema.json");
            var content = File.Exists(schemaPath) ? File.ReadAllText(schemaPath) : "{}";
            return JsonRpcResponse.Success(request.Id, new
            {
                contents = new[] { new { uri, mimeType = "application/json", text = content } }
            });
        }

        return JsonRpcResponse.Error(request.Id, -32602, $"Unknown resource: {uri}");
    }

    private string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        var full = Path.Combine(_jamlFiltersDir, path);
        if (!path.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase) && !File.Exists(full))
            full = Path.Combine(_jamlFiltersDir, path + ".jaml");
        return full;
    }

    private static string GetString(JsonElement args, string key, string defaultValue = "")
    {
        return args.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? defaultValue
            : defaultValue;
    }

    private static int GetInt(JsonElement args, string key, int defaultValue = 0)
    {
        return args.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetInt32()
            : defaultValue;
    }
}

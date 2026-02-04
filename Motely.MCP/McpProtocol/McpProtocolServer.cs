using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.Analysis;
using Motely.API;
using Motely.Executors;
using Motely.Filters;

namespace Motely.MCP.McpProtocol;

public class McpProtocolServer
{
    private const string MCP_PROTOCOL_VERSION = "2024-11-05";
    private const string MCP_METHOD_INITIALIZE = "initialize";
    private const string MCP_METHOD_TOOLS_LIST = "tools/list";
    private const string MCP_METHOD_TOOLS_CALL = "tools/call";
    private const string MCP_METHOD_RESOURCES_LIST = "resources/list";
    private const string MCP_METHOD_RESOURCES_READ = "resources/read";
    private const string MCP_METHOD_PROMPTS_LIST = "prompts/list";
    private const string MCP_METHOD_PROMPTS_GET = "prompts/get";

    private const string TOOL_GENERATE_JAML_FILTER = "generate_jaml_filter";
    private const string TOOL_SEARCH_SEEDS = "search_seeds";
    private const string TOOL_GET_SEARCH_STATUS = "get_search_status";
    private const string TOOL_ANALYZE_SEED = "analyze_seed";
    private const string TOOL_BALATRO_SEED_ANALYZER = "balatro_seed_analyzer";
    private const string TOOL_VERIFY_SEED = "verify_seed";

    private readonly ILogger<McpProtocolServer> _logger;
    private readonly McpServer _mcpServer;
    private readonly MultiSearchManager _searchManager;

    public McpProtocolServer(
        ILogger<McpProtocolServer> logger,
        McpServer mcpServer,
        MultiSearchManager searchManager
    )
    {
        _logger = logger;
        _mcpServer = mcpServer;
        _searchManager = searchManager;
    }

    public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
    {
        try
        {
            return request.Method switch
            {
                MCP_METHOD_INITIALIZE => HandleInitialize(request),
                MCP_METHOD_TOOLS_LIST => HandleToolsList(request),
                MCP_METHOD_TOOLS_CALL => await HandleToolCall(request),
                MCP_METHOD_RESOURCES_LIST => HandleResourcesList(request),
                MCP_METHOD_RESOURCES_READ => await HandleResourceRead(request),
                MCP_METHOD_PROMPTS_LIST => HandlePromptsList(request),
                MCP_METHOD_PROMPTS_GET => await HandlePromptGet(request),
                _ => JsonRpcResponse.Error(
                    request.Id,
                    -32601,
                    $"Method not found: {request.Method}"
                ),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling MCP request: {request.Method}");
            return JsonRpcResponse.Error(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        var initParams = JsonSerializer.Deserialize<McpInitializeParams>(
            request.Params?.ToString() ?? "{}"
        );

        var response = new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpServerCapabilities
            {
                Tools = new McpToolsCapability(),
                Resources = new McpResourcesCapability(),
                Prompts = new McpPromptsCapability(),
            },
            ServerInfo = new McpServerInfo { Name = "balatro-seed-oracle", Version = "1.0.0" },
        };

        return JsonRpcResponse.Success(request.Id, response);
    }

    /// <summary>
    /// List available MCP tools
    /// </summary>
    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new List<McpTool>
        {
            new McpTool
            {
                Name = TOOL_GENERATE_JAML_FILTER,
                Description =
                    "Generate a JAML (Joker Artifact Markup Language) filter from natural language prompt. Returns ONLY the JAML config (no seed search). Use search_seeds tool separately to find seeds.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        prompt = new
                        {
                            type = "string",
                            description = "Natural language description of desired Balatro seed (e.g., 'Blueprint and Brainstorm in Ante 1', 'Negative Perkeo with Observatory voucher')",
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Optional: Deck name (Red, Blue, Yellow, Green, Black, Ghost, Abandoned, Checkered, Anaglyph, Plasma, Erratic). Defaults to Red.",
                            @enum = new[]
                            {
                                "Red",
                                "Blue",
                                "Yellow",
                                "Green",
                                "Black",
                                "Ghost",
                                "Abandoned",
                                "Checkered",
                                "Anaglyph",
                                "Plasma",
                                "Erratic",
                            },
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Optional: Stake level (White, Yellow, Orange, Red, Green, Blue, Purple, Gold, Black). Defaults to White.",
                            @enum = new[]
                            {
                                "White",
                                "Yellow",
                                "Orange",
                                "Red",
                                "Green",
                                "Blue",
                                "Purple",
                                "Gold",
                                "Black",
                            },
                        },
                    },
                    required = new[] { "prompt" },
                },
            },
            new McpTool
            {
                Name = "search_seeds",
                Description =
                    "Search for Balatro seeds matching a JAML filter. Returns search ID and initial results. Use get_search_status to check progress.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        jaml = new
                        {
                            type = "string",
                            description = "JAML filter configuration (from generate_jaml_filter or user-provided)",
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Deck name (defaults to filter's deck or Red)",
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Stake level (defaults to filter's stake or White)",
                        },
                        seedCount = new
                        {
                            type = "integer",
                            description = "Optional: Number of random seeds to search (default: 1,000,000). Set to 0 for unlimited sequential search.",
                        },
                    },
                    required = new[] { "jaml" },
                },
            },
            new McpTool
            {
                Name = TOOL_GET_SEARCH_STATUS,
                Description = "Get status and results of a running or completed seed search.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        searchId = new
                        {
                            type = "string",
                            description = "Search ID returned from search_seeds",
                        },
                    },
                    required = new[] { "searchId" },
                },
            },
            new McpTool
            {
                Name = "analyze_seed",
                Description =
                    "Analyze a specific Balatro seed to see all items (jokers, vouchers, tags, packs, etc.) across all antes.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        seed = new
                        {
                            type = "string",
                            description = "Balatro seed string (e.g., 'ALEEB', '12345678')",
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Optional: Deck name (defaults to Red)",
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Optional: Stake level (defaults to White)",
                        },
                    },
                    required = new[] { "seed" },
                },
            },
            new McpTool
            {
                Name = TOOL_BALATRO_SEED_ANALYZER,
                Description =
                    "Get a comprehensive analysis of a Balatro seed. Identifies every Joker, Voucher, Tarot, and Boss Blind across all 8 antes. Essential for deep seed exploration.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        seed = new
                        {
                            type = "string",
                            description = "The 8-character Balatro seed (e.g. 'TACO1111' or 'ALEEB')",
                        },
                        deck = new
                        {
                            type = "string",
                            description = "The deck to simulate (defaults to Red)",
                            @enum = new[]
                            {
                                "Red", "Blue", "Yellow", "Green", "Black", "Ghost",
                                "Abandoned", "Checkered", "Anaglyph", "Plasma", "Erratic"
                            }
                        },
                        stake = new
                        {
                            type = "string",
                            description = "The stake level (defaults to White)",
                            @enum = new[]
                            {
                                "White", "Yellow", "Orange", "Red", "Green", "Blue", "Purple", "Gold"
                            }
                        },
                    },
                    required = new[] { "seed" },
                },
            },
            new McpTool
            {
                Name = TOOL_VERIFY_SEED,
                Description =
                    "Verify if a specific Balatro seed matches a JAML filter. Returns whether it matches, the score, and detailed tallies.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        seed = new
                        {
                            type = "string",
                            description = "Balatro seed string to verify (e.g., 'UQP1JJ11', 'ALEEB')",
                        },
                        jaml = new
                        {
                            type = "string",
                            description = "JAML filter configuration to verify against (can be JAML string or path to .jaml file)",
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Optional: Deck name (defaults to filter's deck or Red)",
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Optional: Stake level (defaults to filter's stake or White)",
                        },
                    },
                    required = new[] { "seed", "jaml" },
                },
            },
        };

        return JsonRpcResponse.Success(request.Id, new { tools });
    }

    private async Task<JsonRpcResponse> HandleToolCall(JsonRpcRequest request)
    {
        var callParams = JsonSerializer.Deserialize<McpToolCallParams>(
            request.Params?.ToString() ?? "{}"
        );

        if (callParams == null || string.IsNullOrEmpty(callParams.Name))
        {
            return JsonRpcResponse.Error(request.Id, -32602, "Invalid tool call parameters");
        }

        var toolName = callParams.Name;
        var arguments = callParams.Arguments ?? new Dictionary<string, object>();

        try
        {
            object? result = toolName switch
            {
                TOOL_GENERATE_JAML_FILTER => await HandleGenerateJamlFilter(arguments),
                TOOL_SEARCH_SEEDS => await HandleSearchSeeds(arguments),
                TOOL_GET_SEARCH_STATUS => HandleGetSearchStatus(arguments),
                TOOL_ANALYZE_SEED => HandleAnalyzeSeed(arguments),
                TOOL_BALATRO_SEED_ANALYZER => HandleBalatroSeedAnalyzer(arguments),
                TOOL_VERIFY_SEED => HandleVerifySeed(arguments),
                _ => throw new ArgumentException($"Unknown tool: {toolName}"),
            };

            return JsonRpcResponse.Success(
                request.Id,
                new
                {
                    content = new[]
                    {
                        new { type = "text", text = JsonSerializer.Serialize(result) },
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling tool: {toolName}");
            return JsonRpcResponse.Error(
                request.Id,
                -32603,
                $"Tool execution failed: {ex.Message}"
            );
        }
    }

    private async Task<object> HandleGenerateJamlFilter(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("prompt", out var promptObj) || promptObj is not string prompt)
        {
            throw new ArgumentException("Missing or invalid 'prompt' parameter");
        }

        var (jaml, reasoning, error) = await _mcpServer.GenerateJamlOnlyAsync(prompt);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        if (string.IsNullOrWhiteSpace(jaml))
        {
            throw new InvalidOperationException("Failed to generate JAML");
        }

        return new
        {
            jaml = jaml,
            reasoning = reasoning,
            message = $"Generated JAML filter for: {prompt}",
        };
    }

    private async Task<object> HandleSearchSeeds(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("jaml", out var jamlObj) || jamlObj is not string jaml)
        {
            throw new ArgumentException("Missing or invalid 'jaml' parameter");
        }

        // Validate JAML first
        if (
            !JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out var error)
            || config == null
        )
        {
            throw new ArgumentException($"Invalid JAML: {error}");
        }

        var deck = args.TryGetValue("deck", out var deckObj) ? deckObj?.ToString() : null;
        var deckValue = deck ?? config.Deck ?? "Red";
        var stake = args.TryGetValue("stake", out var stakeObj) ? stakeObj?.ToString() : null;
        var stakeValue = stake ?? config.Stake ?? "White";
        var seedCount =
            args.TryGetValue("seedCount", out var countObj)
            && countObj is JsonElement countElem
            && countElem.ValueKind == JsonValueKind.Number
                ? countElem.GetInt32()
                : 0;

        var seedSource = seedCount > 0 ? $"random:{seedCount}" : "random:1000000";

        var (results, searchId) = await _searchManager.StartSearchAsync(
            jaml,
            deck: deckValue,
            stake: stakeValue,
            seedCount: 0,
            seedSource: seedSource
        );

        var columns = _searchManager.GetColumnNames(searchId);
        var searchUrl = $"/JAML/?search={Uri.EscapeDataString(searchId)}";

        var resultList =
            results
                ?.Select(r => new
                {
                    Seed = r.Seed,
                    Score = r.Score,
                    Tallies = r.Tallies,
                })
                .ToList<object>() ?? new List<object>();

        return new
        {
            searchId,
            searchUrl,
            results = resultList,
            columns,
            status = "running",
            message = $"Search started with ID: {searchId}. Found {results?.Count ?? 0} initial results.",
        };
    }

    private object HandleGetSearchStatus(Dictionary<string, object> args)
    {
        if (
            !args.TryGetValue("searchId", out var searchIdObj) || searchIdObj is not string searchId
        )
        {
            throw new ArgumentException("Missing or invalid 'searchId' parameter");
        }

        var (results, progressPercent) = _searchManager.GetSearchStatus(searchId);
        var isRunning = _searchManager.IsSearchRunning(searchId);
        var columns = _searchManager.GetColumnNames(searchId);
        var searchUrl = $"/JAML/?search={Uri.EscapeDataString(searchId)}";

        // Get metrics from search status
        var status = _searchManager.GetStatus(searchId);
        var currentBatch = status?.SeedsSearched ?? 0;
        var totalBatches = 0L; // Not available from MultiSearchManager
        var seedsSearched = status?.SeedsSearched ?? 0;
        var seedsPerSecond = status?.SeedsPerSecond ?? 0.0;

        var resultList =
            results
                ?.Select(r => new
                {
                    Seed = r.Seed,
                    Score = r.Score,
                    Tallies = r.Tallies,
                })
                .ToList<object>() ?? new List<object>();

        return new
        {
            searchId,
            searchUrl,
            status = isRunning ? "running" : "completed",
            progressPercent,
            results = resultList,
            columns,
            metrics = new
            {
                currentBatch,
                totalBatches,
                seedsSearched,
                seedsPerSecond,
            },
        };
    }

    private object HandleAnalyzeSeed(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("seed", out var seedObj) || seedObj is not string seed)
        {
            throw new ArgumentException("Missing or invalid 'seed' parameter");
        }

        var deck = args.TryGetValue("deck", out var deckObj) ? deckObj?.ToString() : null;
        var deckValue = deck ?? "Red";
        var stake = args.TryGetValue("stake", out var stakeObj) ? stakeObj?.ToString() : null;
        var stakeValue = stake ?? "White";

        if (!Enum.TryParse<MotelyDeck>(deckValue, true, out var deckEnum))
            deckEnum = MotelyDeck.Red;
        if (!Enum.TryParse<MotelyStake>(stakeValue, true, out var stakeEnum))
            stakeEnum = MotelyStake.White;

        var analysis = MotelySeedAnalyzer.Analyze(
            new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum)
        );

        return new
        {
            seed,
            deck = deckValue,
            stake = stakeValue,
            analysis = analysis.ToString(),
        };
    }

    private object HandleBalatroSeedAnalyzer(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("seed", out var seedObj) || seedObj is not string seed)
        {
            throw new ArgumentException("Missing or invalid 'seed' parameter");
        }

        var deck = args.TryGetValue("deck", out var deckObj) ? deckObj?.ToString() : null;
        var deckValue = deck ?? "Red";
        var stake = args.TryGetValue("stake", out var stakeObj) ? stakeObj?.ToString() : null;
        var stakeValue = stake ?? "White";

        if (!Enum.TryParse<MotelyDeck>(deckValue, true, out var deckEnum))
            deckEnum = MotelyDeck.Red;
        if (!Enum.TryParse<MotelyStake>(stakeValue, true, out var stakeEnum))
            stakeEnum = MotelyStake.White;

        var analysis = MotelySeedAnalyzer.Analyze(
            new MotelySeedAnalysisConfig(seed, deckEnum, stakeEnum)
        );

        // Return rich structured data so the AI can actually "see" the shop
        return new
        {
            seed,
            deck = deckValue,
            stake = stakeValue,
            analysis_text = analysis.ToString(),
            erratic_deck_composition = analysis.ErraticDeckComposition,
            antes = analysis.Antes.Select(a => new {
                ante = a.Ante,
                boss = FormatUtils.FormatBoss(a.Boss),
                voucher = FormatUtils.FormatVoucher(a.Voucher),
                tags = new[] { FormatUtils.FormatTag(a.SmallBlindTag), FormatUtils.FormatTag(a.BigBlindTag) },
                shop = a.ShopQueue.Select(item => FormatUtils.FormatItem(item)).ToArray(),
                packs = a.Packs.Select(p => new {
                    type = FormatUtils.FormatPackName(p.Type),
                    items = p.Items.Select(item => FormatUtils.FormatItem(item)).ToArray()
                }).ToArray()
            }).ToArray(),
            error = analysis.Error
        };
    }

    private object HandleVerifySeed(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("seed", out var seedObj) || seedObj is not string seed)
        {
            throw new ArgumentException("Missing or invalid 'seed' parameter");
        }

        if (!args.TryGetValue("jaml", out var jamlObj) || jamlObj is not string jaml)
        {
            throw new ArgumentException("Missing or invalid 'jaml' parameter");
        }

        // Determine if jaml is a file path or JAML content
        string jamlContent;
        string configPath;
        bool isJamlFile =
            jaml.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase)
            && (File.Exists(jaml) || File.Exists(Path.Combine("JamlFilters", jaml)));

        if (isJamlFile)
        {
            // It's a file path
            configPath = File.Exists(jaml) ? jaml : Path.Combine("JamlFilters", jaml);
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"JAML file not found: {jaml}");
            }
            jamlContent = File.ReadAllText(configPath);
        }
        else
        {
            jamlContent = jaml;
            configPath = "inline";
        }

        if (
            !JamlConfigLoader.TryLoadFromJamlString(jamlContent, out var config, out var error)
            || config == null
        )
        {
            throw new ArgumentException($"Invalid JAML: {error}");
        }

        var deck = args.TryGetValue("deck", out var deckObj) ? deckObj?.ToString() : null;
        var deckValue = deck ?? config.Deck ?? "Red";
        var stake = args.TryGetValue("stake", out var stakeObj) ? stakeObj?.ToString() : null;
        var stakeValue = stake ?? config.Stake ?? "White";

        // Collect results via callback
        List<MotelySeedScoreTally> results = new();

        var parameters = new JsonSearchParams
        {
            SpecificSeed = seed,
            Quiet = true, // Suppress console output
            Cutoff = 0, // No cutoff - we want to see the actual score
            Threads = 1,
        };

        var executor = new JsonSearchExecutor(
            config,
            parameters,
            (MotelySeedScoreTally result) =>
            {
                results.Add(result);
            }
        );

        var exitCode = executor.Execute(awaitCompletion: true);

        // Determine if seed matched (exit code 0 and has results)
        bool matched = exitCode == 0 && results.Count > 0;
        MotelySeedScoreTally? result = results.Count > 0 ? results.First() : null;

        // Get column names from config and pair with tally values
        var columnNames = config.GetColumnNames();
        var talliesDict = new Dictionary<string, int>();
        if (result != null && result.Value.TallyColumns != null)
        {
            for (int i = 0; i < result.Value.TallyColumns.Count && i + 2 < columnNames.Count; i++)
            {
                talliesDict[columnNames[i + 2]] = result.Value.TallyColumns[i];
            }
        }

        return new
        {
            seed,
            jaml = configPath == "inline" ? "(inline)" : configPath,
            deck = deckValue,
            stake = stakeValue,
            matched,
            score = result?.Score ?? 0,
            tallies = talliesDict,
            exitCode,
            message = matched
                ? $"✅ Seed '{seed}' MATCHES the filter (score: {result?.Score ?? 0})"
                : $"❌ Seed '{seed}' does NOT match the filter",
        };
    }

    private JsonRpcResponse HandleResourcesList(JsonRpcRequest request)
    {
        var resources = new List<McpResource>
        {
            new McpResource
            {
                Uri = "jaml://templates",
                Name = "JAML Templates",
                Description = "Example JAML filter templates",
                MimeType = "application/yaml",
            },
            new McpResource
            {
                Uri = "jaml://game-mechanics",
                Name = "Game Mechanics",
                Description = "Balatro game mechanics and rules documentation",
                MimeType = "text/markdown",
            },
        };

        // Add filter files as resources
        try
        {
            var filtersDir = Path.Combine(
                MotelyPaths.SearchResultsDir,
                "..",
                "JamlFilters"
            );
            if (Directory.Exists(filtersDir))
            {
                var filterFiles = Directory.GetFiles(
                    filtersDir,
                    "*.jaml",
                    SearchOption.TopDirectoryOnly
                );
                foreach (var filterFile in filterFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filterFile);
                    resources.Add(
                        new McpResource
                        {
                            Uri = $"jaml://filter/{fileName}",
                            Name = fileName,
                            Description = $"JAML filter: {fileName}",
                            MimeType = "text/yaml",
                        }
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list filter files as resources");
        }

        return JsonRpcResponse.Success(request.Id, new { resources });
    }

    private async Task<JsonRpcResponse> HandleResourceRead(JsonRpcRequest request)
    {
        var readParams = JsonSerializer.Deserialize<McpResourceReadParams>(
            request.Params?.ToString() ?? "{}"
        );

        if (readParams == null || string.IsNullOrEmpty(readParams.Uri))
        {
            return JsonRpcResponse.Error(request.Id, -32602, "Invalid resource read parameters");
        }

        try
        {
            string content = "";
            string mimeType = "text/plain";

            // Handle different resource URIs
            if (readParams.Uri == "jaml://templates")
            {
                // Return example JAML templates
                content =
                    @"# Example JAML Filter Templates

## Basic Joker Search
name: ""Example Filter""
deck: Red
stake: White
jokers:
  - name: Blueprint
    ante: 1

## Multiple Jokers
name: ""Multi Joker Filter""
deck: Red
stake: White
jokers:
  - name: Blueprint
    ante: 1
  - name: Brainstorm
    ante: 1
  - name: Perkeo
    ante: 2

## Voucher Search
name: ""Voucher Filter""
deck: Red
stake: White
vouchers:
  - name: Observatory
    ante: 1";
                mimeType = "text/yaml";
            }
            else if (readParams.Uri == "jaml://game-mechanics")
            {
                // Return game mechanics documentation
                content =
                    @"# Balatro Game Mechanics

## Antes
- Ante 1-3: Early game
- Ante 4-6: Mid game  
- Ante 7-8: Late game

## Jokers
Jokers are the primary cards that modify gameplay. Each joker has unique effects.

## Vouchers
Vouchers provide permanent shop upgrades.

## Tarot Cards
Tarot cards provide one-time effects when used.";
                mimeType = "text/markdown";
            }
            else if (readParams.Uri.StartsWith("jaml://filter/"))
            {
                // Read a specific filter file
                var filterName = readParams.Uri.Replace("jaml://filter/", "");

                // Try multiple paths
                var motelyRoot = MotelyPaths.SearchResultsDir;
                var possiblePaths = new[]
                {
                    Path.Combine(motelyRoot, "..", "JamlFilters", $"{filterName}.jaml"),
                    Path.Combine("JamlFilters", $"{filterName}.jaml"),
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "JamlFilters",
                        $"{filterName}.jaml"
                    ),
                };

                string? filterPath = null;
                foreach (var path in possiblePaths)
                {
                    var fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        filterPath = fullPath;
                        break;
                    }
                }

                if (filterPath != null && File.Exists(filterPath))
                {
                    content = await File.ReadAllTextAsync(filterPath);
                    mimeType = "text/yaml";
                }
                else
                {
                    return JsonRpcResponse.Error(
                        request.Id,
                        -32602,
                        $"Filter not found: {filterName}"
                    );
                }
            }
            else
            {
                return JsonRpcResponse.Error(
                    request.Id,
                    -32602,
                    $"Unknown resource URI: {readParams.Uri}"
                );
            }

            return JsonRpcResponse.Success(
                request.Id,
                new
                {
                    contents = new[]
                    {
                        new
                        {
                            uri = readParams.Uri,
                            mimeType = mimeType,
                            text = content,
                        },
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading resource: {Uri}", readParams.Uri);
            return JsonRpcResponse.Error(
                request.Id,
                -32603,
                $"Error reading resource: {ex.Message}"
            );
        }
    }

    private JsonRpcResponse HandlePromptsList(JsonRpcRequest request)
    {
        var prompts = new List<McpPrompt>
        {
            new McpPrompt
            {
                Name = "find_joker_build",
                Description = "Find Balatro seeds with specific joker combinations",
                Arguments = new[]
                {
                    new McpPromptArgument
                    {
                        Name = "jokers",
                        Description =
                            "Comma-separated list of joker names (e.g., 'Blueprint, Brainstorm, Perkeo')",
                        Required = true,
                    },
                    new McpPromptArgument
                    {
                        Name = "antes",
                        Description =
                            "Optional: Antes to search in (e.g., '1-3' or '1,2,3'). Defaults to all antes.",
                        Required = false,
                    },
                },
            },
            new McpPrompt
            {
                Name = "find_economy_build",
                Description =
                    "Find Balatro seeds with economy items (money-generating jokers, vouchers, tarot cards)",
                Arguments = new[]
                {
                    new McpPromptArgument
                    {
                        Name = "focus",
                        Description =
                            "Optional: Focus area - 'early' (antes 1-3), 'mid' (antes 4-6), or 'late' (antes 7-8). Defaults to early.",
                        Required = false,
                    },
                },
            },
        };

        return JsonRpcResponse.Success(request.Id, new { prompts });
    }

    private async Task<JsonRpcResponse> HandlePromptGet(JsonRpcRequest request)
    {
        var getParams = JsonSerializer.Deserialize<McpPromptGetParams>(
            request.Params?.ToString() ?? "{}"
        );

        if (getParams == null || string.IsNullOrEmpty(getParams.Name))
        {
            return JsonRpcResponse.Error(request.Id, -32602, "Invalid prompt get parameters");
        }

        try
        {
            // Build prompt based on prompt name and arguments
            string promptText = "";

            if (getParams.Name == "find_joker_build")
            {
                string? jokers = null;
                string? antes = null;
                getParams.Arguments?.TryGetValue("jokers", out jokers);
                getParams.Arguments?.TryGetValue("antes", out antes);
                var jokersStr = jokers ?? "";
                var antesStr = antes ?? "";

                if (string.IsNullOrEmpty(jokersStr))
                {
                    return JsonRpcResponse.Error(request.Id, -32602, "jokers argument is required");
                }

                promptText = $"Find Balatro seeds with these jokers: {jokersStr}";
                if (!string.IsNullOrEmpty(antesStr))
                {
                    promptText += $" in antes {antesStr}";
                }
            }
            else if (getParams.Name == "find_economy_build")
            {
                string? focus = null;
                getParams.Arguments?.TryGetValue("focus", out focus);
                var focusStr = focus ?? "early";
                var antes =
                    focusStr == "early" ? "1-3"
                    : focusStr == "mid" ? "4-6"
                    : "7-8";

                promptText =
                    $"Find Balatro seeds with economy items (money-generating jokers, vouchers, tarot cards) in antes {antes}";
            }
            else
            {
                return JsonRpcResponse.Error(
                    request.Id,
                    -32602,
                    $"Unknown prompt: {getParams.Name}"
                );
            }

            // Generate JAML using the prompt
            var (jaml, reasoning, error) = await _mcpServer.GenerateJamlOnlyAsync(promptText);

            if (!string.IsNullOrEmpty(error))
            {
                return JsonRpcResponse.Error(
                    request.Id,
                    -32603,
                    $"Failed to generate JAML: {error}"
                );
            }

            return JsonRpcResponse.Success(
                request.Id,
                new
                {
                    description = $"Generated JAML filter for: {promptText}",
                    messages = new[]
                    {
                        new { role = "user", content = new { type = "text", text = promptText } },
                        new
                        {
                            role = "assistant",
                            content = new
                            {
                                type = "text",
                                text = $"Generated JAML filter:\n\n{jaml}\n\nReasoning: {reasoning}",
                            },
                        },
                    },
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating prompt: {Name}", getParams.Name);
            return JsonRpcResponse.Error(
                request.Id,
                -32603,
                $"Error generating prompt: {ex.Message}"
            );
        }
    }
}

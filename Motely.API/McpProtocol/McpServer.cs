using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Motely;
using Motely.Filters;
using Motely.Analysis;

namespace Motely.API.McpProtocol;

/// <summary>
/// Real MCP (Model Context Protocol) Server implementation
/// Follows MCP specification: https://spec.modelcontextprotocol.io/
/// Protocol version: 2024-11-05
/// </summary>
public class McpProtocolServer
{
    private readonly ILogger<McpProtocolServer> _logger;
    private readonly McpServer _jamlGenieService;
    private readonly SearchManager _searchManager;

    public McpProtocolServer(
        ILogger<McpProtocolServer> logger,
        McpServer jamlGenieService,
        SearchManager searchManager)
    {
        _logger = logger;
        _jamlGenieService = jamlGenieService;
        _searchManager = searchManager;
    }

    /// <summary>
    /// Handle MCP JSON-RPC 2.0 request
    /// </summary>
    public async Task<JsonRpcResponse> HandleRequestAsync(JsonRpcRequest request)
    {
        try
        {
            return request.Method switch
            {
                // MCP Protocol Methods
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolCall(request),
                "resources/list" => HandleResourcesList(request),
                "resources/read" => await HandleResourceRead(request),
                "prompts/list" => HandlePromptsList(request),
                "prompts/get" => await HandlePromptGet(request),
                
                // Unknown method
                _ => JsonRpcResponse.Error(request.Id, -32601, $"Method not found: {request.Method}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling MCP request: {request.Method}");
            return JsonRpcResponse.Error(request.Id, -32603, $"Internal error: {ex.Message}");
        }
    }

    /// <summary>
    /// MCP Initialize - Handshake with client
    /// </summary>
    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        var initParams = JsonSerializer.Deserialize<McpInitializeParams>(request.Params?.ToString() ?? "{}");
        
        var response = new McpInitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpServerCapabilities
            {
                Tools = new McpToolsCapability(),
                Resources = new McpResourcesCapability(),
                Prompts = new McpPromptsCapability()
            },
            ServerInfo = new McpServerInfo
            {
                Name = "balatro-seed-oracle",
                Version = "1.0.0"
            }
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
                Name = "generate_jaml_filter",
                Description = "Generate a JAML (Joker Artifact Markup Language) filter from natural language prompt. Returns ONLY the JAML config (no seed search). Use search_seeds tool separately to find seeds.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        prompt = new
                        {
                            type = "string",
                            description = "Natural language description of desired Balatro seed (e.g., 'Blueprint and Brainstorm in Ante 1', 'Negative Perkeo with Observatory voucher')"
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Optional: Deck name (Red, Blue, Yellow, Green, Black, Ghost, Abandoned, Checkered, Anaglyph, Plasma, Erratic). Defaults to Red.",
                            @enum = new[] { "Red", "Blue", "Yellow", "Green", "Black", "Ghost", "Abandoned", "Checkered", "Anaglyph", "Plasma", "Erratic" }
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Optional: Stake level (White, Yellow, Orange, Red, Green, Blue, Purple, Gold, Black). Defaults to White.",
                            @enum = new[] { "White", "Yellow", "Orange", "Red", "Green", "Blue", "Purple", "Gold", "Black" }
                        }
                    },
                    required = new[] { "prompt" }
                }
            },
            new McpTool
            {
                Name = "search_seeds",
                Description = "Search for Balatro seeds matching a JAML filter. Returns search ID and initial results. Use get_search_status to check progress.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        jaml = new
                        {
                            type = "string",
                            description = "JAML filter configuration (from generate_jaml_filter or user-provided)"
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Deck name (defaults to filter's deck or Red)"
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Stake level (defaults to filter's stake or White)"
                        },
                        seedCount = new
                        {
                            type = "integer",
                            description = "Optional: Number of random seeds to search (default: 1,000,000). Set to 0 for unlimited sequential search."
                        }
                    },
                    required = new[] { "jaml" }
                }
            },
            new McpTool
            {
                Name = "get_search_status",
                Description = "Get status and results of a running or completed seed search.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        searchId = new
                        {
                            type = "string",
                            description = "Search ID returned from search_seeds"
                        }
                    },
                    required = new[] { "searchId" }
                }
            },
            new McpTool
            {
                Name = "analyze_seed",
                Description = "Analyze a specific Balatro seed to see all items (jokers, vouchers, tags, packs, etc.) across all antes.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        seed = new
                        {
                            type = "string",
                            description = "Balatro seed string (e.g., 'ALEEB', '12345678')"
                        },
                        deck = new
                        {
                            type = "string",
                            description = "Optional: Deck name (defaults to Red)"
                        },
                        stake = new
                        {
                            type = "string",
                            description = "Optional: Stake level (defaults to White)"
                        }
                    },
                    required = new[] { "seed" }
                }
            }
        };

        return JsonRpcResponse.Success(request.Id, new { tools });
    }

    /// <summary>
    /// Call an MCP tool
    /// </summary>
    private async Task<JsonRpcResponse> HandleToolCall(JsonRpcRequest request)
    {
        var callParams = JsonSerializer.Deserialize<McpToolCallParams>(request.Params?.ToString() ?? "{}");
        
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
                "generate_jaml_filter" => await HandleGenerateJamlFilter(arguments),
                "search_seeds" => await HandleSearchSeeds(arguments),
                "get_search_status" => HandleGetSearchStatus(arguments),
                "analyze_seed" => HandleAnalyzeSeed(arguments),
                _ => throw new ArgumentException($"Unknown tool: {toolName}")
            };

            return JsonRpcResponse.Success(request.Id, new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result) } } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling tool: {toolName}");
            return JsonRpcResponse.Error(request.Id, -32603, $"Tool execution failed: {ex.Message}");
        }
    }

    private async Task<object> HandleGenerateJamlFilter(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("prompt", out var promptObj) || promptObj is not string prompt)
        {
            throw new ArgumentException("Missing or invalid 'prompt' parameter");
        }

        // MCP tool: generate_jaml_filter should ONLY generate config, NOT search
        // Use GenerateJamlOnlyAsync instead of ProcessPromptAsync
        var (jaml, reasoning, error) = await _jamlGenieService.GenerateJamlOnlyAsync(prompt);
        
        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        if (string.IsNullOrWhiteSpace(jaml))
        {
            throw new InvalidOperationException("Failed to generate JAML");
        }

        // Return ONLY config - no searchId, no results (that's what search_seeds is for)
        return new
        {
            jaml = jaml,
            reasoning = reasoning,
            message = $"Generated JAML filter for: {prompt}"
        };
    }

    private async Task<object> HandleSearchSeeds(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("jaml", out var jamlObj) || jamlObj is not string jaml)
        {
            throw new ArgumentException("Missing or invalid 'jaml' parameter");
        }

        // Validate JAML first
        if (!JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out var error) || config == null)
        {
            throw new ArgumentException($"Invalid JAML: {error}");
        }

            var deck = args.TryGetValue("deck", out var deckObj) ? deckObj?.ToString() : null;
            var deckValue = deck ?? config.Deck ?? "Red";
            var stake = args.TryGetValue("stake", out var stakeObj) ? stakeObj?.ToString() : null;
            var stakeValue = stake ?? config.Stake ?? "White";
        var seedCount = args.TryGetValue("seedCount", out var countObj) && countObj is JsonElement countElem && countElem.ValueKind == JsonValueKind.Number
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

        var resultList = results?.Select(r => new { Seed = r.Seed, Score = r.Score, Tallies = r.Tallies }).ToList<object>() ?? new List<object>();
        
        return new
        {
            searchId,
            searchUrl,
            results = resultList,
            columns,
            status = "running",
            message = $"Search started with ID: {searchId}. Found {results?.Count ?? 0} initial results."
        };
    }

    private object HandleGetSearchStatus(Dictionary<string, object> args)
    {
        if (!args.TryGetValue("searchId", out var searchIdObj) || searchIdObj is not string searchId)
        {
            throw new ArgumentException("Missing or invalid 'searchId' parameter");
        }

        var (results, progressPercent) = _searchManager.GetSearchStatus(searchId);
        var isRunning = _searchManager.IsSearchRunning(searchId);
        var columns = _searchManager.GetColumnNames(searchId);
        var searchUrl = $"/JAML/?search={Uri.EscapeDataString(searchId)}";

        _searchManager.TryGetSearchMetrics(
            searchId,
            out var currentBatch,
            out var totalBatches,
            out var seedsSearched,
            out var seedsPerSecond
        );

        var resultList = results?.Select(r => new { Seed = r.Seed, Score = r.Score, Tallies = r.Tallies }).ToList<object>() ?? new List<object>();
        
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
                seedsPerSecond
            }
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
            analysis = analysis.ToString()
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
                MimeType = "application/yaml"
            },
            new McpResource
            {
                Uri = "jaml://game-mechanics",
                Name = "Game Mechanics",
                Description = "Balatro game mechanics and rules documentation",
                MimeType = "text/markdown"
            }
        };

        return JsonRpcResponse.Success(request.Id, new { resources });
    }

    private Task<JsonRpcResponse> HandleResourceRead(JsonRpcRequest request)
    {
        var readParams = JsonSerializer.Deserialize<McpResourceReadParams>(request.Params?.ToString() ?? "{}");
        
        if (readParams == null || string.IsNullOrEmpty(readParams.Uri))
        {
            return Task.FromResult(JsonRpcResponse.Error(request.Id, -32602, "Invalid resource read parameters"));
        }

        // TODO: Implement resource reading
        return Task.FromResult(JsonRpcResponse.Error(request.Id, -32601, "Resource reading not yet implemented"));
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
                        Description = "Comma-separated list of joker names (e.g., 'Blueprint, Brainstorm, Perkeo')",
                        Required = true
                    },
                    new McpPromptArgument
                    {
                        Name = "antes",
                        Description = "Optional: Antes to search in (e.g., '1-3' or '1,2,3'). Defaults to all antes.",
                        Required = false
                    }
                }
            },
            new McpPrompt
            {
                Name = "find_economy_build",
                Description = "Find Balatro seeds with economy items (money-generating jokers, vouchers, tarot cards)",
                Arguments = new[]
                {
                    new McpPromptArgument
                    {
                        Name = "focus",
                        Description = "Optional: Focus area - 'early' (antes 1-3), 'mid' (antes 4-6), or 'late' (antes 7-8). Defaults to early.",
                        Required = false
                    }
                }
            }
        };

        return JsonRpcResponse.Success(request.Id, new { prompts });
    }

    private Task<JsonRpcResponse> HandlePromptGet(JsonRpcRequest request)
    {
        var getParams = JsonSerializer.Deserialize<McpPromptGetParams>(request.Params?.ToString() ?? "{}");
        
        if (getParams == null || string.IsNullOrEmpty(getParams.Name))
        {
            return Task.FromResult(JsonRpcResponse.Error(request.Id, -32602, "Invalid prompt get parameters"));
        }

        // TODO: Implement prompt generation
        return Task.FromResult(JsonRpcResponse.Error(request.Id, -32601, "Prompt generation not yet implemented"));
    }
}


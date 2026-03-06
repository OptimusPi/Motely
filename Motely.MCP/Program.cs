using System.Text.Json;
using System.Text.Json.Serialization;
using Motely;
using Motely.Analysis;
using Motely.Filters;

namespace Motely.MCP;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static async Task Main(string[] args)
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin);
        using var writer = new StreamWriter(stdout) { AutoFlush = true };

        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(line, JsonOptions);
                if (request == null)
                    continue;

                var response = HandleRequest(request);
                await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));
            }
            catch (Exception ex)
            {
                var error = new JsonRpcResponse
                {
                    Id = null,
                    Error = new JsonRpcError
                    {
                        Code = -32700,
                        Message = $"Parse error: {ex.Message}",
                    },
                };
                await writer.WriteLineAsync(JsonSerializer.Serialize(error, JsonOptions));
            }
        }
    }

    private static JsonRpcResponse HandleRequest(JsonRpcRequest request)
    {
        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => HandleToolCall(request),
            "notifications/initialized" => new JsonRpcResponse { Id = request.Id },
            _ => new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32601,
                    Message = $"Method not found: {request.Method}",
                },
            },
        };
    }

    private static JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        var version = MotelyBuildVersion.For(typeof(Program).Assembly);

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { tools = new { } },
                serverInfo = new { name = "motely", version },
            },
        };
    }

    private static JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new object[]
        {
            new
            {
                name = "parse_jaml",
                description = "Parse and validate a JAML filter. Returns validation result.",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["jaml"] = new { type = "string", description = "The JAML filter content" },
                    },
                    required = new[] { "jaml" },
                },
            },
            new
            {
                name = "analyze_seed",
                description = "Analyze a Balatro seed. Returns jokers, vouchers, bosses, tags for each ante.",
                inputSchema = new
                {
                    type = "object",
                    properties = new Dictionary<string, object>
                    {
                        ["seed"] = new
                        {
                            type = "string",
                            description = "8-character seed (e.g., ABC12345)",
                        },
                        ["deck"] = new { type = "string", description = "Deck (default: Red)" },
                        ["stake"] = new { type = "string", description = "Stake (default: White)" },
                    },
                    required = new[] { "seed" },
                },
            },
        };

        return new JsonRpcResponse { Id = request.Id, Result = new { tools } };
    }

    private static JsonRpcResponse HandleToolCall(JsonRpcRequest request)
    {
        try
        {
            var paramsJson = request.Params?.ToString() ?? "{}";
            var callParams = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, JsonOptions);
            if (callParams == null)
                return ErrorResponse(request.Id, -32602, "Invalid params");

            var args = callParams.Arguments ?? new Dictionary<string, JsonElement>();

            return callParams.Name switch
            {
                "parse_jaml" => HandleParseJaml(request.Id, args),
                "analyze_seed" => HandleAnalyzeSeed(request.Id, args),
                _ => ErrorResponse(request.Id, -32602, $"Unknown tool: {callParams.Name}"),
            };
        }
        catch (Exception ex)
        {
            return ErrorResponse(request.Id, -32603, ex.Message);
        }
    }

    private static JsonRpcResponse HandleParseJaml(object? id, Dictionary<string, JsonElement> args)
    {
        if (!args.TryGetValue("jaml", out var jamlElement))
            return ErrorResponse(id, -32602, "Missing required parameter: jaml");

        var jaml = jamlElement.GetString() ?? "";

        try
        {
            // Parse JAML using YamlDotNet deserializer
            var deserializer = new YamlDotNet.Serialization.Deserializer();
            var jamlDto = deserializer.Deserialize<JamlDto>(jaml);

            // Convert DTO to JamlConfig manually
            var config = new JamlConfig
            {
                Name = jamlDto?.Name,
                Description = jamlDto?.Description,
                Author = jamlDto?.Author,
                Deck = (
                    jamlDto?.Deck != null
                    && System.Enum.TryParse<MotelyDeck>(jamlDto.Deck, true, out var deck)
                        ? deck
                        : MotelyDeck.Red
                ),
                Stake = (
                    jamlDto?.Stake != null
                    && System.Enum.TryParse<MotelyStake>(jamlDto.Stake, true, out var stake)
                        ? stake
                        : MotelyStake.White
                ),
                Must = new JamlClauseSet(),
                Should = new JamlClauseSet(),
                MustNot = new JamlClauseSet(),
            };
            var must = config.Must?.Count ?? 0;
            var should = config.Should?.Count ?? 0;
            var mustNot = config.MustNot?.Count ?? 0;
            return ToolResult(
                id,
                $"Valid JAML.\nMust: {must}, Should: {should}, MustNot: {mustNot}"
            );
        }
        catch (Exception ex)
        {
            return ToolResult(id, $"Parse error: {ex.Message}", isError: true);
        }
    }

    private static JsonRpcResponse HandleAnalyzeSeed(
        object? id,
        Dictionary<string, JsonElement> args
    )
    {
        if (!args.TryGetValue("seed", out var seedElement))
            return ErrorResponse(id, -32602, "Missing required parameter: seed");

        var seed = seedElement.GetString()?.ToUpperInvariant() ?? "";
        var deckStr = args.TryGetValue("deck", out var d) ? d.GetString() ?? "Red" : "Red";
        var stakeStr = args.TryGetValue("stake", out var s) ? s.GetString() ?? "White" : "White";

        try
        {
            var deck = ParseDeck(deckStr);
            var stake = ParseStake(stakeStr);
            var config = new MotelySeedAnalysisConfig(seed, deck, stake);
            var analysis = MotelySeedAnalyzer.Analyze(config);
            return ToolResult(id, analysis.ToString());
        }
        catch (Exception ex)
        {
            return ToolResult(id, $"Analysis error: {ex.Message}", isError: true);
        }
    }

    private static MotelyDeck ParseDeck(string deck)
    {
        return deck.ToLowerInvariant().Replace(" ", "").Replace("deck", "") switch
        {
            "red" => MotelyDeck.Red,
            "blue" => MotelyDeck.Blue,
            "yellow" => MotelyDeck.Yellow,
            "green" => MotelyDeck.Green,
            "black" => MotelyDeck.Black,
            "magic" => MotelyDeck.Magic,
            "nebula" => MotelyDeck.Nebula,
            "ghost" => MotelyDeck.Ghost,
            "abandoned" => MotelyDeck.Abandoned,
            "checkered" => MotelyDeck.Checkered,
            "zodiac" => MotelyDeck.Zodiac,
            "painted" => MotelyDeck.Painted,
            "anaglyph" => MotelyDeck.Anaglyph,
            "plasma" => MotelyDeck.Plasma,
            "erratic" => MotelyDeck.Erratic,
            _ => MotelyDeck.Red,
        };
    }

    private static MotelyStake ParseStake(string stake)
    {
        return stake.ToLowerInvariant().Replace(" ", "").Replace("stake", "") switch
        {
            "white" => MotelyStake.White,
            "red" => MotelyStake.Red,
            "green" => MotelyStake.Green,
            "black" => MotelyStake.Black,
            "blue" => MotelyStake.Blue,
            "purple" => MotelyStake.Purple,
            "orange" => MotelyStake.Orange,
            "gold" => MotelyStake.Gold,
            _ => MotelyStake.White,
        };
    }

    private static JsonRpcResponse ToolResult(object? id, string text, bool isError = false)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Result = new { content = new[] { new { type = "text", text } }, isError },
        };
    }

    private static JsonRpcResponse ErrorResponse(object? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message },
        };
    }
}

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public class ToolCallParams
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, JsonElement>? Arguments { get; set; }
}

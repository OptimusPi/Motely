using System.Text.Json.Serialization;

namespace Motely.API.McpProtocol;

/// <summary>
/// JSON-RPC 2.0 Request
/// </summary>
public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string JsonRpc { get; set; } = "2.0";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("id")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? Id { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("method")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Method { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("params")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? Params { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// JSON-RPC 2.0 Response
/// </summary>
public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string JsonRpc { get; set; } = "2.0";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("id")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? Id { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("result")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? Result { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("error")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public JsonRpcError? ErrorResponse { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static JsonRpcResponse Success(object? id, object result)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        return new JsonRpcResponse { Id = id, Result = result };
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static JsonRpcResponse Error(object? id, int code, string message, object? data = null)
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        return new JsonRpcResponse
        {
            Id = id,
            ErrorResponse = new JsonRpcError
            {
                Code = code,
                Message = message,
                Data = data,
            },
        };
    }
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class JsonRpcError
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
    [JsonPropertyName("code")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public int Code { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("message")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Message { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("data")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? Data { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// MCP Initialize Parameters
/// </summary>
public class McpInitializeParams
{
    [JsonPropertyName("protocolVersion")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string ProtocolVersion { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("capabilities")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? Capabilities { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("clientInfo")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object? ClientInfo { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// MCP Initialize Result
/// </summary>
public class McpInitializeResult
{
    [JsonPropertyName("protocolVersion")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string ProtocolVersion { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("capabilities")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public McpServerCapabilities Capabilities { get; set; } = new();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("serverInfo")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public McpServerInfo ServerInfo { get; set; } = new();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpServerCapabilities
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
    [JsonPropertyName("tools")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public McpToolsCapability Tools { get; set; } = new();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("resources")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public McpResourcesCapability Resources { get; set; } = new();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("prompts")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public McpPromptsCapability Prompts { get; set; } = new();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpToolsCapability { }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpResourcesCapability { }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpPromptsCapability { }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpServerInfo
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("version")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Version { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// MCP Tool Definition
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("description")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Description { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("inputSchema")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public object InputSchema { get; set; } = new();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// MCP Tool Call Parameters
/// </summary>
public class McpToolCallParams
{
    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("arguments")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public Dictionary<string, object>? Arguments { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// MCP Resource Definition
/// </summary>
public class McpResource
{
    [JsonPropertyName("uri")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Uri { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("description")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Description { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("mimeType")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string MimeType { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpResourceReadParams
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
    [JsonPropertyName("uri")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Uri { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

/// <summary>
/// MCP Prompt Definition
/// </summary>
public class McpPrompt
{
    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("description")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Description { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("arguments")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public McpPromptArgument[] Arguments { get; set; } = Array.Empty<McpPromptArgument>();
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpPromptArgument
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("description")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Description { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("required")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public bool Required { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public class McpPromptGetParams
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
{
    [JsonPropertyName("name")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public string Name { get; set; } = string.Empty;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    [JsonPropertyName("arguments")]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public Dictionary<string, string>? Arguments { get; set; }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}

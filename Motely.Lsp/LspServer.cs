using System.Reflection;
using System.Text.Json.Nodes;
using Motely.Filters.Jaml;
using Motely.Lsp.Core;

namespace Motely.Lsp;

/// <summary>
/// The JAML language server: a single-threaded message loop over <see cref="JsonRpcChannel"/>
/// that answers the LSP subset editors actually consume — lifecycle, document sync with
/// published diagnostics, hover, and completion. Every answer comes from
/// <see cref="JamlLanguageService"/>, which reads the engine's own grammar.
/// Logging goes to <paramref name="log"/> (stderr in production) — stdout is the protocol.
/// </summary>
public sealed class LspServer(Stream input, Stream output, TextWriter log)
{
    private readonly JsonRpcChannel _channel = new(input, output);
    private readonly Dictionary<string, string> _documents = [];
    private bool _shutdownRequested;

    /// <summary>Serve until the peer sends <c>exit</c> or closes the stream. Returns the
    /// process exit code the protocol asks for: 0 after a clean shutdown, 1 otherwise.</summary>
    public int Run()
    {
        while (true)
        {
            JsonNode? message;
            try
            {
                message = _channel.Read();
            }
            catch (Exception ex)
            {
                log.WriteLine($"[motely-lsp] read failed: {ex.Message}");
                return 1;
            }
            if (message is null)
                return _shutdownRequested ? 0 : 1;

            var method = message["method"]?.GetValue<string>();
            var id = message["id"];
            var @params = message["params"];

            if (method == "exit")
                return _shutdownRequested ? 0 : 1;

            try
            {
                Dispatch(method, id, @params);
            }
            catch (Exception ex)
            {
                log.WriteLine($"[motely-lsp] {method} failed: {ex}");
                if (id is not null)
                    RespondError(id, -32603, ex.Message);
            }
        }
    }

    private void Dispatch(string? method, JsonNode? id, JsonNode? @params)
    {
        switch (method)
        {
            case "initialize":
                Respond(id, InitializeResult());
                break;
            case "initialized":
            case "$/cancelRequest":
            case "$/setTrace":
                break;
            case "shutdown":
                _shutdownRequested = true;
                Respond(id, null);
                break;
            case "textDocument/didOpen":
            {
                var doc = @params?["textDocument"];
                var uri = doc?["uri"]?.GetValue<string>();
                if (uri is not null)
                {
                    _documents[uri] = doc?["text"]?.GetValue<string>() ?? "";
                    PublishDiagnostics(uri);
                }
                break;
            }
            case "textDocument/didChange":
            {
                var uri = @params?["textDocument"]?["uri"]?.GetValue<string>();
                // Full sync: the last content change carries the whole document.
                var changes = @params?["contentChanges"] as JsonArray;
                if (uri is not null && changes is { Count: > 0 })
                {
                    _documents[uri] = changes[^1]?["text"]?.GetValue<string>() ?? "";
                    PublishDiagnostics(uri);
                }
                break;
            }
            case "textDocument/didClose":
            {
                var uri = @params?["textDocument"]?["uri"]?.GetValue<string>();
                if (uri is not null)
                {
                    _documents.Remove(uri);
                    _channel.Write(Notification("textDocument/publishDiagnostics", new JsonObject
                    {
                        ["uri"] = uri,
                        ["diagnostics"] = new JsonArray(),
                    }));
                }
                break;
            }
            case "textDocument/hover":
                Respond(id, HoverResult(@params));
                break;
            case "textDocument/completion":
                Respond(id, CompletionResult(@params));
                break;
            default:
                if (id is not null)
                    RespondError(id, -32601, $"Method not found: {method}");
                break;
        }
    }

    private static JsonObject InitializeResult() => new()
    {
        ["capabilities"] = new JsonObject
        {
            // 1 = Full sync: JAML documents are small; whole-text sync keeps truth simple.
            ["textDocumentSync"] = 1,
            ["hoverProvider"] = true,
            ["completionProvider"] = new JsonObject
            {
                ["triggerCharacters"] = new JsonArray(":", " ", "-"),
            },
        },
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "Motely.Lsp",
            ["version"] = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+')[0] ?? "0.0.0",
        },
    };

    private JsonNode? HoverResult(JsonNode? @params)
    {
        var (text, line, character) = TextDocumentPosition(@params);
        if (text is null)
            return null;
        var hover = JamlLanguageService.Hover(text, line, character);
        if (hover is null)
            return null;
        return new JsonObject
        {
            ["contents"] = new JsonObject
            {
                ["kind"] = "markdown",
                ["value"] = hover.Markdown,
            },
            ["range"] = Range(hover.Span),
        };
    }

    private JsonNode CompletionResult(JsonNode? @params)
    {
        var (text, line, character) = TextDocumentPosition(@params);
        var items = new JsonArray();
        if (text is not null)
            foreach (var item in JamlLanguageService.Complete(text, line, character))
                items.Add(new JsonObject
                {
                    ["label"] = item.Label,
                    // LSP CompletionItemKind: Property=10, EnumMember=20, Class=7.
                    ["kind"] = item.Kind switch
                    {
                        "value" => 20,
                        "discriminator" => 7,
                        _ => 10,
                    },
                    ["detail"] = item.Detail,
                });
        return items;
    }

    private void PublishDiagnostics(string uri)
    {
        var diagnostics = new JsonArray();
        foreach (var d in JamlLanguageService.Diagnose(_documents[uri]))
            diagnostics.Add(new JsonObject
            {
                ["range"] = Range(d.Span),
                ["severity"] = (int)d.Severity,
                ["code"] = d.Code,
                ["source"] = "motely",
                ["message"] = d.Message,
            });
        _channel.Write(Notification("textDocument/publishDiagnostics", new JsonObject
        {
            ["uri"] = uri,
            ["diagnostics"] = diagnostics,
        }));
    }

    private (string? Text, int Line, int Character) TextDocumentPosition(JsonNode? @params)
    {
        var uri = @params?["textDocument"]?["uri"]?.GetValue<string>();
        var line = @params?["position"]?["line"]?.GetValue<int>() ?? 0;
        var character = @params?["position"]?["character"]?.GetValue<int>() ?? 0;
        return (uri is not null && _documents.TryGetValue(uri, out var text) ? text : null,
            line, character);
    }

    private static JsonObject Range(JamlSpan span) => new()
    {
        ["start"] = new JsonObject { ["line"] = span.StartLine, ["character"] = span.StartColumn },
        ["end"] = new JsonObject { ["line"] = span.EndLine, ["character"] = span.EndColumn },
    };

    private void Respond(JsonNode? id, JsonNode? result)
    {
        if (id is null)
            return;
        _channel.Write(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["result"] = result,
        });
    }

    private void RespondError(JsonNode id, int code, string message) =>
        _channel.Write(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        });

    private static JsonObject Notification(string method, JsonObject @params) => new()
    {
        ["jsonrpc"] = "2.0",
        ["method"] = method,
        ["params"] = @params,
    };
}

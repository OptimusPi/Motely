using System.Text.Json;
using Motely.Lsp;
using Motely.Lsp.Core;

// One-shot engine diagnose for tools / CI (does not open the stdio LSP loop).
//   Motely.Lsp --diagnose <file.jaml>
//   Motely.Lsp --diagnose -   # text on stdin
// Exit 0 = clean document; 1 = one or more diagnostics; 2 = usage / IO error.
if (args is ["--diagnose", var pathArg])
{
    try
    {
        string text =
            pathArg == "-"
                ? Console.In.ReadToEnd()
                : File.ReadAllText(pathArg);
        var diags = JamlLanguageService.Diagnose(text);
        var payload = diags
            .Select(d => new
            {
                message = d.Message,
                code = d.Code,
                severity = d.Severity.ToString(),
                startLine = d.Span.StartLine,
                startColumn = d.Span.StartColumn,
                endLine = d.Span.EndLine,
                endColumn = d.Span.EndColumn,
            })
            .ToArray();
        Console.Out.WriteLine(JsonSerializer.Serialize(payload));
        return diags.Count == 0 ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 2;
    }
}

if (args.Length > 0)
{
    Console.Error.WriteLine(
        "Usage: Motely.Lsp                 # stdio language server\n"
            + "       Motely.Lsp --diagnose <file.jaml>\n"
            + "       Motely.Lsp --diagnose -   # stdin"
    );
    return 2;
}

// stdout carries the protocol; every human-readable word goes to stderr.
var server = new LspServer(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    Console.Error
);
return server.Run();

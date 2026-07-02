using System.ComponentModel;
using ModelContextProtocol.Server;
using Motely.Filters.Jaml;
using Motely.Filters.Jummy;
using Motely;

namespace Motely.Mcp;

/// <summary>
/// Lightweight JAML validation tools exposed over MCP. The heavy lifting — seed searching —
/// happens client-side in the MCP App UI using motely-wasm, never on this server.
/// </summary>
[McpServerToolType]
public static class JamlTools
{
    [McpServerTool, Description(
        "Validate a JAML (Jimbo's Ante Markup Language, or JSON) filter against the real Motely "
        + "loader. Returns 'OK' if valid, or the exact loader error. Note: legendaries "
        + "(Perkeo/Triboulet/Canio/Yorick/Chicot) must use 'legendaryJoker:' with "
        + "arcanaPacks/spectralPacks sources — never shopItems.")]
    public static string JamlValidate(
        [Description("JAML or JSON filter text")] string jaml)
    {
        return JamlConfigLoader.TryLoad(jaml, out _, out var error)
            ? "OK — valid JAML."
            : $"INVALID: {error}";
    }

    [McpServerTool, Description(
        "Parse a one-line JUMMY string (e.g. 'Eternal Blueprint in antes 1 or 2' — one line = "
        + "one JAML clause) and report whether it maps to a valid JAML clause, or the parse error.")]
    public static string JummyValidate(
        [Description("A single JUMMY line")] string line)
    {
        return JummyLine.TryToClause(line, out _, out var err)
            ? "OK — parses to a valid clause."
            : $"INVALID JUMMY: {err}";
    }

    [McpServerTool, Description(
        "Define Motely's naming scheme: what JAML, JUMMY, Jimmolate, and JAMLyzer each mean.")]
    public static string Glossary() => MotelyGlossary.Render();
}

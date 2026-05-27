using System.Linq;
using Bootsharp;
using Motely;
using Motely.Filters.Jaml;

namespace Motely.Wasm;

/// <summary>
/// Exposes a parsed JAML configuration as a real type/handle over the JS interop boundary.
/// </summary>
public sealed class WasmJamlConfig
{
    internal JamlConfig Config { get; }

    internal WasmJamlConfig(JamlConfig config)
    {
        Config = config;
    }

    public string Id => Config.Id;
    public string? Name => Config.Name;
    public string? Description => Config.Description;
    public string? Author => Config.Author;
    public MotelyDeck Deck => Config.Deck;
    public MotelyStake Stake => Config.Stake;
    public string[] Seeds => Config.Seeds.ToArray();
    public bool HasAnyClauses => Config.HasAnyClauses;
}

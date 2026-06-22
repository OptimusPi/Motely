using System.Reflection;

namespace Motely.Wasm;

// Renamers keep the JS surface to exactly two nodes: Jimmolate + Motely.
public static class BootsharpRenamers
{
    // Fold the Motely.Wasm namespace (default module path "motely/wasm") into the root
    // "index" module so consumers import { Jimmolate, Motely } straight from the package root.
    [RenameModule]
    public static string RenameModule(Type type, string @default) =>
        type.Namespace == "Motely.Wasm" ? "" : @default;

    [RenameNode]
    public static string? RenameNode(Type type, string @default)
    {
        if (type == typeof(Program)) return null; // hide the C# bootstrap from JS
        if (type.IsByRefLike) return null;        // Span<T> / ref structs never marshal
        return @default;
    }
}

// C# entry point. Bootstrap only; hidden from JS by the renamer above.
public static class Program
{
    public static void Main() { }
}

// JS -> C#. Bind `Jimmolate.probe = (seed, deck, stake) => bool` BEFORE boot().
// Bootsharp snapshots [Import] bindings at boot(); assigning after boot is a no-op.
public static partial class Jimmolate
{
    [Import]
    public static partial bool Probe(string seed, MotelyDeck deck, MotelyStake stake);
}

// C# -> JS. The Motely node.
public static partial class Motely
{
    [Export]
    public static string GetVersion() =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

    [Export]
    public static string NormalizeSeed(string seed) => MotelyGlobals.NormalizeSeed(seed);
}

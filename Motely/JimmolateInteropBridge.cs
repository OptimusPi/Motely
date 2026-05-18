namespace Motely;

/// <summary>
/// Wires a JS-provided predicate into <see cref="Filters.JimmolateFilterDesc"/> for WASM interop.
/// Assigned by Motely.Wasm.Program at boot; null outside of a WASM host.
/// </summary>
public static class JimmolateInteropBridge
{
    public static Func<string, bool>? Predicate;
}

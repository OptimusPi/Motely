using Bootsharp;
using Motely;

// Two types on the settings surface that the 0.9 serializer cannot build a Binary<> for.
// Both are boundary facts, so both are answered here rather than in the engine.

/// <summary>
/// A filter desc crosses as an opaque handle. Its members drive ref-struct contexts, so there is
/// nothing JavaScript can read or implement — but the settings surface names the type, and the
/// generator needs a declared shape rather than a value serializer.
/// </summary>
[SpecializeImport(typeof(IMotelySeedFilterDesc))]
public abstract class MotelySeedFilterDescImport(int id) : SpecializedImport(id) { }

[SpecializeExport(typeof(IMotelySeedFilterDesc))]
public sealed class MotelySeedFilterDescExport(IMotelySeedFilterDesc desc)
    : SpecializedExport(desc) { }

/// <summary>
/// A deferred sequence has no value to serialize, so it crosses as its materialized items. The
/// lazy form stays native-side; anything that reaches this boundary is already enumerable once.
/// </summary>
[SpecializeImport(typeof(IEnumerable<string>))]
public abstract class StringSequenceImport(int id) : SpecializedImport(id)
{
    public abstract string[] Items { get; }

    protected override object Unwrap() => Items;
}

[SpecializeExport(typeof(IEnumerable<string>))]
public sealed class StringSequenceExport : SpecializedExport
{
    public StringSequenceExport(IEnumerable<string> sequence)
        : base(sequence) => Items = [.. sequence];

    public string[] Items { get; }
}

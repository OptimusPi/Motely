using Bootsharp;
using Motely;
using Motely.Filters;
using Motely.Filters.Jaml;

// The engine's real search interface, exported as-is. No DTO, no wrapper: Bootsharp passes an
// interface by reference as an interop instance, so JS gets the same chainable With* surface the
// CLI uses. One grammar, one apply path.
[assembly: Export(typeof(IMotelySearchSettings), typeof(IMotelySearch))]

public static partial class Program
{
    public static void Main() { }

    /// <summary>JAML text in, the engine's live settings out. Every knob is the engine's own.</summary>
    [Export]
    public static IMotelySearchSettings Settings(string jaml) =>
        JamlSearchBuilder.CreateSettings(JamlConfigLoader.FromJaml(jaml));
}

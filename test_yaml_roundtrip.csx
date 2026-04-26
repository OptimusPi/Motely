// Quick test to see what NormalizeNestedLogicSyntax produces
using Motely.Filters;

var jaml = File.ReadAllText(@"JamlFilters\sixtid.jaml");
Console.WriteLine("=== ORIGINAL ===");
Console.WriteLine(jaml);

var canonical = JamlConfigLoader.Canonicalize(jaml);
Console.WriteLine("=== CANONICAL ===");
Console.WriteLine(canonical);

if (JamlConfigLoader.TryLoad(jaml, out var config, out var error))
{
    Console.WriteLine($"=== LOADED OK: {config.Must.Count} must, {config.Should.Count} should ===");
}
else
{
    Console.WriteLine($"=== LOAD FAILED: {error} ===");
}

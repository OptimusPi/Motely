#:project ../Motely/Motely.csproj
#:property TreatWarningsAsErrors=false
// Generates TypeScript types for JAML directly from the Motely C# enums.
// The enums are the single source of truth; this emits the thing tooling actually consumes
// (TS string-literal unions), not a schema.json nobody reads.
//
//   dotnet run tools/jaml-types.cs              -> types/jaml.generated.ts
//   dotnet run tools/jaml-types.cs path/out.ts  -> custom path
using System;
using System.IO;
using System.Linq;

var enums = typeof(Motely.Enums.MotelyVoucher).Assembly.GetTypes()
    .Where(t => t.IsEnum && t.IsPublic && t.Namespace == "Motely.Enums")
    .OrderBy(t => t.Name)
    .ToList();

var sb = new System.Text.StringBuilder();
sb.AppendLine("// AUTO-GENERATED from Motely C# enums. Do not edit by hand.");
sb.AppendLine("// Regenerate: dotnet run tools/jaml-types.cs");
sb.AppendLine();
foreach (var e in enums)
{
    var members = Enum.GetNames(e);
    sb.Append("export const ").Append(e.Name).Append(" = [")
      .Append(string.Join(", ", members.Select(m => "\"" + m + "\"")))
      .AppendLine("] as const;");
    sb.Append("export type ").Append(e.Name).Append(" = typeof ").Append(e.Name).AppendLine("[number];");
    sb.AppendLine();
}

var outPath = args.Length > 0 ? args[0] : "types/jaml.generated.ts";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, sb.ToString());
Console.WriteLine($"Wrote {enums.Count} enum types ({enums.Sum(e => Enum.GetNames(e).Length)} names) -> {outPath}");

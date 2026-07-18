using System.Runtime.CompilerServices;

// Minimal assembly info to avoid duplicates
[assembly: InternalsVisibleTo("Motely.Tests")]
[assembly: InternalsVisibleTo("Motely.CLI")]
[assembly: InternalsVisibleTo("MotelyWorker")]
// The language service reads JamlSyntaxException.Span so an editor squiggle lands on the
// offending character instead of being regexed back out of the message text.
[assembly: InternalsVisibleTo("Motely.Lsp.Core")]

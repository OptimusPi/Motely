using Motely.Analysis;

namespace Motely;

public interface IMotelyRuntime
{
    string? ValidateJaml(string jaml);
    JamlSearchPlanDto DescribeJaml(string jaml);
    SearchStatusDto RunSequentialJamlSearch(string jaml, int threads, int batchCharCount, long startBatch, long endBatch);
    SearchStatusDto RunSeedListJamlSearch(string jaml, int threads, string[] seeds);
    SearchStatusDto RunRandomJamlSearch(string jaml, int threads, int count);
    SearchStatusDto RunPalindromeJamlSearch(string jaml, int threads);
    SearchStatusDto RunKeywordJamlSearch(string jaml, int threads, string[] keywords, string? paddingChars);
    SeedAnalysisDto AnalyzeSeed(string seed, string deck, string stake);
}

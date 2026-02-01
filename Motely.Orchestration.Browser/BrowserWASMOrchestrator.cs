using Motely.Executors;
using Motely.Filters;

namespace Motely.Orchestration.Browser;

public static class BrowserWASMOrchestrator
{
    public static IMotelySearchContext LaunchWithContext(
        MotelyJsonConfig config,
        JsonSearchParams parameters)
    {
        // Browser uses in-memory search; results can be persisted via JS callbacks.
        return MotelySearchOrchestrator.LaunchWithContext(
            config,
            parameters,
            useInMemoryStorage: true
        );
    }
}

using Motely.Filters;

namespace Motely.Repository;

public interface IMotelyRepository
{
    IMotelySeedProvider GetSource(string moniker);
    IResultStorage GetSink(string moniker, MotelyRunConfig runConfig);
}


namespace Motely;

public interface IMotelySeedExplorer : IDisposable
{
    MotelySingleSearchContext GetContext();
}

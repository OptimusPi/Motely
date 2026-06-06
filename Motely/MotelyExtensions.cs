namespace Motely;

public static class MotelyExtensions
{
    public static IMotelySeedFilterDesc Or(
        this IMotelySeedFilterDesc first,
        IMotelySeedFilterDesc second
    )
    {
        return new OrFilterDesc([first, second]);
    }

    public static IMotelySeedFilterDesc And(
        this IMotelySeedFilterDesc first,
        IMotelySeedFilterDesc second
    )
    {
        return new AndFilterDesc([first, second]);
    }
}

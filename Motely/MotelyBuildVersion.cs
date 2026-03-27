using System.Reflection;

namespace Motely.Core;

public static class MotelyBuildVersion
{
    public static string For(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? throw new InvalidOperationException(
            $"AssemblyInformationalVersionAttribute is missing for {assembly.GetName().Name}."
        );
}

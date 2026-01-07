#if !BROWSER
using DuckDB.NET.Data;

namespace Motely.DuckDB;

/// <summary>
/// R2 configuration helper - applies R2 credentials to DuckDB connections.
/// Note: For IConfiguration-based configuration, use R2ConfigurationHelper in Motely.API project.
/// </summary>
public static class R2Configuration
{
    // This class provides direct credential methods.
    // For appsettings.json integration, see Motely.API/R2ConfigurationHelper.cs
}
#endif

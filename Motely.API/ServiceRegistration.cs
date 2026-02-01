using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Motely.API.Hubs;
using Motely.API.Services;
using Motely.Executors;

namespace Motely.API;

/// <summary>
/// Modular service registration for different deployment scenarios.
/// Allows enabling/disabling features independently for testing and deployment.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Register search queue services (for multiplayer seed searches)
    /// </summary>
    public static IServiceCollection AddSearchQueueServices(this IServiceCollection services)
    {
        services.AddSingleton<SearchQueueService>();
        services.AddHostedService<SearchQueueHostedService>();
        services.AddSingleton<SearchService>();
        services.AddSingleton<SearchManager>(); // Facade for MCP
        return services;
    }

    /// <summary>
    /// Register SignalR services (for real-time search updates)
    /// </summary>
    public static IServiceCollection AddSignalRServices(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(10);
            options.KeepAliveInterval = TimeSpan.FromSeconds(5);
            options.HandshakeTimeout = TimeSpan.FromSeconds(2);
        });

        services.AddSingleton<ISearchBroadcaster, SearchBroadcaster>();
        return services;
    }

    /// <summary>
    /// Register MCP services (for AI assistant integration)
    /// NOTE: MCP services are now in Motely.MCP project - this is kept for backward compatibility
    /// </summary>
    public static IServiceCollection AddMcpServices(this IServiceCollection services)
    {
        // MCP services are registered in MotelyApiHost.cs directly from Motely.MCP project
        // This method is kept for backward compatibility but does nothing
        return services;
    }

    /// <summary>
    /// Register all services (always enabled - no feature flags)
    /// </summary>
    public static IServiceCollection AddMotelyServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Register MultiSearchManager as singleton (manages thread pool for queued searches)
        services.AddSingleton(MultiSearchManager.Instance);
        
        // Always enable all services - no feature flags needed
        services.AddSearchQueueServices();
        services.AddSignalRServices();
        services.AddMcpServices();

        return services;
    }
}

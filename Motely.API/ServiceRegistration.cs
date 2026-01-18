using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Motely.API.Hubs;
using Motely.API.Services;

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
    /// Register all services based on feature flags
    /// </summary>
    public static IServiceCollection AddMotelyServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var features = new FeatureFlags(configuration);

        if (features.EnableSearchQueue)
        {
            services.AddSearchQueueServices();
        }

        if (features.EnableSignalR)
        {
            services.AddSignalRServices();
        }

        if (features.EnableMcp)
        {
            services.AddMcpServices();
        }

        return services;
    }
}

/// <summary>
/// Feature flags for enabling/disabling features
/// </summary>
public class FeatureFlags
{
    public bool EnableSearchQueue { get; }
    public bool EnableSignalR { get; }
    public bool EnableMcp { get; }
    public bool EnableSwagger { get; }

    public FeatureFlags(IConfiguration configuration)
    {
        var featuresSection = configuration.GetSection("Features");

        // Default to enabled for backward compatibility
        EnableSearchQueue = featuresSection.GetValue<bool>("EnableSearchQueue", true);
        EnableSignalR = featuresSection.GetValue<bool>("EnableSignalR", true);
        EnableMcp = featuresSection.GetValue<bool>("EnableMcp", true);
        EnableSwagger = featuresSection.GetValue<bool>("EnableSwagger", true);
    }
}

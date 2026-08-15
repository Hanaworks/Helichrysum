namespace Helichrysum.Core;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Helichrysum core services
/// into the dependency injection container.
/// </summary>
public static class HelichrysumServiceRegistration
{
    /// <summary>
    /// Adds Helichrysum core services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddHelichrysumCore(this IServiceCollection services)
    {
        // Core services will be registered here as slices are implemented.
        // Phase 0: placeholder registration — ensures DI infrastructure is wired.
        return services;
    }
}
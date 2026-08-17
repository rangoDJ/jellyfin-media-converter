using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MediaConverter;

/// <summary>
/// Registers this plugin's own services with Jellyfin's dependency injection container.
/// Core services (IMediaEncoder, ILibraryManager, IProviderManager, ILibraryMonitor,
/// IDirectoryService) are already registered by Jellyfin itself and just get constructor-injected
/// where needed. <see cref="Services.ConversionRuleScheduledTask"/> isn't registered here - Jellyfin
/// auto-discovers <c>IScheduledTask</c> implementations from plugin assemblies on its own, but it
/// still resolves the task's own constructor dependencies (like ConversionRuleService below) through
/// this same container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<Services.HardwareEncoderResolver>();
        serviceCollection.AddSingleton<Services.ConversionEngine>();
        serviceCollection.AddSingleton<Services.MediaProbeService>();
        serviceCollection.AddSingleton<Services.PreviewRemuxService>();
        serviceCollection.AddSingleton<Services.PreviewTokenService>();
        serviceCollection.AddSingleton<Services.ConversionRuleService>();
        serviceCollection.AddSingleton<Services.IConversionJobManager, Services.ConversionJobManager>();
        serviceCollection.AddHostedService(provider => (Services.ConversionJobManager)provider.GetRequiredService<Services.IConversionJobManager>());
    }
}

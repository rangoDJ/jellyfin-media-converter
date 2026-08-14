using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MediaConverter;

/// <summary>
/// Registers this plugin's own services with Jellyfin's dependency injection container.
/// Core services (IMediaEncoder, ILibraryManager, IProviderManager, ILibraryMonitor,
/// IDirectoryService) are already registered by Jellyfin itself and just get constructor-injected
/// where needed - only the plugin's own conversion job queue needs registering here.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<Services.HardwareEncoderResolver>();
        serviceCollection.AddSingleton<Services.ConversionEngine>();
        serviceCollection.AddSingleton<Services.IConversionJobManager, Services.ConversionJobManager>();
        serviceCollection.AddHostedService(provider => (Services.ConversionJobManager)provider.GetRequiredService<Services.IConversionJobManager>());
    }
}

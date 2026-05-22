using Jellyfin.Plugin.HDHomeRunGuide.Services;
using Jellyfin.Plugin.HDHomeRunGuide.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HDHomeRunGuide;

/// <summary>
/// Registers server-side plugin services.
/// </summary>
public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<HDHomeRunClient>();
        serviceCollection.AddSingleton<XmlTvGuideService>();
        serviceCollection.AddSingleton<PluginLogService>();
        serviceCollection.AddSingleton<LiveTvConfigurator>();
        serviceCollection.AddSingleton<HDHomeRunGuideService>();
        serviceCollection.AddHostedService<GuideRefreshHostedService>();
        serviceCollection.AddSingleton<IScheduledTask, RefreshGuideTask>();
    }
}

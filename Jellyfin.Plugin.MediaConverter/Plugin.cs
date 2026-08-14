using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.MediaConverter.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MediaConverter;

/// <summary>
/// The main plugin. Registers the dashboard page and holds the shared plugin configuration.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// This GUID is the plugin's permanent identity and must never change across releases.
    /// </summary>
    private static readonly Guid PluginId = Guid.Parse("ced1b4c4-47d1-4cd5-8d22-1491f69676ef");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Media Converter";

    /// <inheritdoc />
    public override string Description => "Convert library media files from the dashboard, using this server's own configured ffmpeg hardware transcoder (Intel/Nvidia/AMD) or CPU.";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = "mediaconverter",
                DisplayName = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Web.browser.html", GetType().Namespace),
                MenuIcon = "video_settings",
                EnableInMainMenu = true
            }
        ];
    }
}

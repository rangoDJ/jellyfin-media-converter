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
            },
            new PluginPageInfo
            {
                // Served as its own page and pulled in via <script src> from browser.html, rather than
                // inlined directly in the HTML - some Jellyfin web client versions don't reliably execute
                // <script> tags embedded in a config page's markup.
                Name = "mediaconverterjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Web.browser.js", GetType().Namespace)
            },
            new PluginPageInfo
            {
                // Not linked from anywhere in the SPA - served here only so it has a stable URL to
                // reference from an external global script injector (see README). Adds a quick-convert
                // button to movie/episode detail pages, sidestepping the config-page script issue above
                // since scripts loaded that way are real <script> tags in the top-level page.
                Name = "mediaconverteritemjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Web.itemdetail.js", GetType().Namespace)
            }
        ];
    }
}

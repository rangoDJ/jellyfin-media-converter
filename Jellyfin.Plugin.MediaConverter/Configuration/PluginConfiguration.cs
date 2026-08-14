using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaConverter.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        DefaultContainer = "mkv";
        DefaultVideoCodec = "hevc";
        DefaultQuality = 23;
        MaxConcurrentJobs = 1;
        TempFileSuffix = ".mediaconverter.tmp";
        VariantSuffixTemplate = "{name}-{codec}{ext}";
    }

    /// <summary>
    /// Gets or sets the default output container (e.g. "mkv", "mp4") preselected in the convert dialog.
    /// </summary>
    public string DefaultContainer { get; set; }

    /// <summary>
    /// Gets or sets the default codec family (e.g. "hevc") preselected in the convert dialog. The
    /// actual hardware/software encoder is resolved at conversion time from the server's own
    /// configured transcoding backend.
    /// </summary>
    public string DefaultVideoCodec { get; set; }

    /// <summary>
    /// Gets or sets the default quality value (QSV global_quality scale, lower is higher quality).
    /// </summary>
    public int DefaultQuality { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of conversions that may run at once. Fixed at 1 by default
    /// since a single shared iGPU cannot usefully encode more than one QSV job at a time.
    /// </summary>
    public int MaxConcurrentJobs { get; set; }

    /// <summary>
    /// Gets or sets the suffix appended to the temporary file ffmpeg writes to before it is
    /// renamed/swapped into its final location.
    /// </summary>
    public string TempFileSuffix { get; set; }

    /// <summary>
    /// Gets or sets the filename template used for "create new variant" mode. Supports the
    /// placeholders {name} (original filename without extension), {codec}, and {ext} (new extension).
    /// </summary>
    public string VariantSuffixTemplate { get; set; }
}

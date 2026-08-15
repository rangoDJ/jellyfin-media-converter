namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Codec/quality stats for a single media file, probed directly via ffprobe.
/// </summary>
public class MediaInfo
{
    /// <summary>
    /// Gets or sets the file path that was probed.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the media duration, in ticks.
    /// </summary>
    public long DurationTicks { get; set; }

    /// <summary>
    /// Gets or sets the container format name(s), e.g. "matroska,webm".
    /// </summary>
    public string? Container { get; set; }

    /// <summary>
    /// Gets or sets the overall bitrate in bits per second, as reported by the container.
    /// </summary>
    public long? OverallBitRate { get; set; }

    /// <summary>
    /// Gets or sets the first video stream's codec name, e.g. "hevc".
    /// </summary>
    public string? VideoCodec { get; set; }

    /// <summary>
    /// Gets or sets the first video stream's width in pixels.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets the first video stream's height in pixels.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets the first video stream's bitrate in bits per second, if reported.
    /// </summary>
    public long? VideoBitRate { get; set; }

    /// <summary>
    /// Gets or sets the first audio stream's codec name, e.g. "aac".
    /// </summary>
    public string? AudioCodec { get; set; }

    /// <summary>
    /// Gets or sets the first audio stream's channel count.
    /// </summary>
    public int? AudioChannels { get; set; }

    /// <summary>
    /// Gets or sets the first audio stream's bitrate in bits per second, if reported.
    /// </summary>
    public long? AudioBitRate { get; set; }
}

using System;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Parameters describing a single requested conversion.
/// </summary>
public class ConversionRequest
{
    /// <summary>
    /// Gets or sets the library item id of the source video to convert.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the output container extension (without the leading dot), e.g. "mkv".
    /// </summary>
    public string Container { get; set; } = "mkv";

    /// <summary>
    /// Gets or sets the target codec family, e.g. "hevc", "h264" or "av1". The actual encoder used
    /// is resolved from the server's configured hardware transcoder.
    /// </summary>
    public string VideoCodec { get; set; } = "hevc";

    /// <summary>
    /// Gets or sets the quality value passed to the encoder (lower is higher quality for the
    /// QSV/NVENC/AMF style quality scales; higher is higher quality for CRF-style software encoding).
    /// </summary>
    public int Quality { get; set; } = 23;

    /// <summary>
    /// Gets or sets a value indicating whether the original file is replaced or a new variant is created.
    /// </summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Variant;

    /// <summary>
    /// Gets or sets an optional raw ffmpeg argument string that, when set, replaces the generated
    /// video-encoding arguments entirely for advanced users.
    /// </summary>
    public string? FfmpegArgsOverride { get; set; }
}

using System;
using Jellyfin.Plugin.MediaConverter.Services;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// Request body for starting a new conversion job.
/// </summary>
public class ConvertRequestDto
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
    /// Gets or sets the target codec family, e.g. "hevc", "h264" or "av1".
    /// </summary>
    public string VideoCodec { get; set; } = "hevc";

    /// <summary>
    /// Gets or sets the quality value passed to the encoder.
    /// </summary>
    public int Quality { get; set; } = 23;

    /// <summary>
    /// Gets or sets a value indicating whether the original file is replaced or a new variant is created.
    /// </summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Variant;

    /// <summary>
    /// Gets or sets an optional raw ffmpeg argument string for advanced users.
    /// </summary>
    public string? FfmpegArgsOverride { get; set; }
}

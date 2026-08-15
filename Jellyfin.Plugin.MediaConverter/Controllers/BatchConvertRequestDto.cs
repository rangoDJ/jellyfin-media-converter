using System;
using System.Collections.Generic;
using Jellyfin.Plugin.MediaConverter.Services;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// Request body for starting conversion jobs for multiple items at once (e.g. a whole season or series),
/// all sharing the same conversion parameters.
/// </summary>
public class BatchConvertRequestDto
{
    /// <summary>
    /// Gets or sets the library item ids of the source videos to convert.
    /// </summary>
    public IList<Guid> ItemIds { get; set; } = new List<Guid>();

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
    /// Gets or sets a value indicating whether the original files are replaced or new variants are created.
    /// </summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Variant;

    /// <summary>
    /// Gets or sets the encoder preset/speed (e.g. "veryfast", "medium", "slow"), when supported.
    /// </summary>
    public string? Preset { get; set; }

    /// <summary>
    /// Gets or sets the target height in pixels to scale the video to. Null or 0 keeps the source resolution.
    /// </summary>
    public int? ScaleHeight { get; set; }

    /// <summary>
    /// Gets or sets the audio encoder to use, e.g. "aac", "ac3", "libopus", "flac", or "copy".
    /// </summary>
    public string AudioCodec { get; set; } = "copy";

    /// <summary>
    /// Gets or sets the audio bitrate in kbps, applied only when <see cref="AudioCodec"/> is not "copy".
    /// </summary>
    public int? AudioBitrateKbps { get; set; }

    /// <summary>
    /// Gets or sets how subtitle streams are handled in the output.
    /// </summary>
    public SubtitleMode SubtitleMode { get; set; } = SubtitleMode.Copy;

    /// <summary>
    /// Gets or sets an optional raw ffmpeg argument string for advanced users.
    /// </summary>
    public string? FfmpegArgsOverride { get; set; }
}

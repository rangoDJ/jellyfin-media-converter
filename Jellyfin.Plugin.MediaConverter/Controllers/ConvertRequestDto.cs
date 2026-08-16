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
    /// Gets or sets the quality value passed to the encoder. Only used when
    /// <see cref="RateControlMode"/> is <see cref="RateControlMode.Quality"/>.
    /// </summary>
    public int Quality { get; set; } = 23;

    /// <summary>
    /// Gets or sets whether the encoder targets a fixed quality value or a specific average bitrate.
    /// </summary>
    public RateControlMode RateControlMode { get; set; } = RateControlMode.Quality;

    /// <summary>
    /// Gets or sets the target average video bitrate in kbps, used only when
    /// <see cref="RateControlMode"/> is <see cref="RateControlMode.Bitrate"/>.
    /// </summary>
    public int? VideoBitrateKbps { get; set; }

    /// <summary>
    /// Gets or sets an optional video bitrate cap in kbps, usable with either rate control mode.
    /// </summary>
    public int? MaxVideoBitrateKbps { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the original file is replaced or a new variant is created.
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

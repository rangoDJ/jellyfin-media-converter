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
    /// Only used when <see cref="RateControlMode"/> is <see cref="RateControlMode.Quality"/>.
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
    /// Gets or sets an optional video bitrate cap in kbps (applied via -maxrate/-bufsize),
    /// usable together with either rate control mode to bound peak bitrate.
    /// </summary>
    public int? MaxVideoBitrateKbps { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the original file is replaced or a new variant is created.
    /// </summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Variant;

    /// <summary>
    /// Gets or sets the encoder preset/speed (e.g. "veryfast", "medium", "slow"), when the resolved
    /// encoder supports one. Ignored for encoders that don't (e.g. VAAPI, VideoToolbox).
    /// </summary>
    public string? Preset { get; set; }

    /// <summary>
    /// Gets or sets the target height in pixels to scale the video to, preserving aspect ratio.
    /// Null or 0 leaves the source resolution untouched.
    /// </summary>
    public int? ScaleHeight { get; set; }

    /// <summary>
    /// Gets or sets the audio encoder to use, e.g. "aac", "ac3", "libopus", "flac". "copy" (the
    /// default) passes the source audio streams through unchanged.
    /// </summary>
    public string AudioCodec { get; set; } = "copy";

    /// <summary>
    /// Gets or sets the audio bitrate in kbps, applied only when <see cref="AudioCodec"/> is not "copy".
    /// Null lets ffmpeg pick the encoder's default.
    /// </summary>
    public int? AudioBitrateKbps { get; set; }

    /// <summary>
    /// Gets or sets how subtitle streams are handled in the output.
    /// </summary>
    public SubtitleMode SubtitleMode { get; set; } = SubtitleMode.Copy;

    /// <summary>
    /// Gets or sets an optional raw ffmpeg argument string that, when set, replaces the generated
    /// video-encoding arguments entirely for advanced users.
    /// </summary>
    public string? FfmpegArgsOverride { get; set; }
}

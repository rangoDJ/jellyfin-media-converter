using System.Collections.Generic;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// The resolved ffmpeg encoder and any extra arguments needed to drive the server's configured
/// hardware transcoder for a given codec family.
/// </summary>
public class EncoderSelection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EncoderSelection"/> class.
    /// </summary>
    /// <param name="encoder">The ffmpeg video encoder name, e.g. "hevc_qsv".</param>
    /// <param name="qualityFlag">The ffmpeg flag used to pass the quality value, e.g. "-global_quality".</param>
    /// <param name="extraInputArgs">Extra arguments inserted before the input file, e.g. hwaccel device init.</param>
    /// <param name="extraOutputArgs">Extra output arguments inserted alongside the video codec.</param>
    /// <param name="requiredVideoFilters">Filter chain fragments (without the leading "-vf") that this
    /// encoder needs applied, e.g. VAAPI's "format=nv12,hwupload". Combined with any user-requested
    /// scale filter into a single "-vf" argument.</param>
    /// <param name="supportsPreset">Whether this encoder accepts a "-preset" argument.</param>
    public EncoderSelection(
        string encoder,
        string qualityFlag,
        IReadOnlyList<string> extraInputArgs,
        IReadOnlyList<string> extraOutputArgs,
        IReadOnlyList<string> requiredVideoFilters,
        bool supportsPreset)
    {
        Encoder = encoder;
        QualityFlag = qualityFlag;
        ExtraInputArgs = extraInputArgs;
        ExtraOutputArgs = extraOutputArgs;
        RequiredVideoFilters = requiredVideoFilters;
        SupportsPreset = supportsPreset;
    }

    /// <summary>
    /// Gets the ffmpeg video encoder name, e.g. "hevc_qsv".
    /// </summary>
    public string Encoder { get; }

    /// <summary>
    /// Gets the ffmpeg flag used to pass the quality value, e.g. "-global_quality".
    /// </summary>
    public string QualityFlag { get; }

    /// <summary>
    /// Gets the extra arguments inserted before the input file, e.g. hwaccel device init.
    /// </summary>
    public IReadOnlyList<string> ExtraInputArgs { get; }

    /// <summary>
    /// Gets the extra output arguments inserted alongside the video codec.
    /// </summary>
    public IReadOnlyList<string> ExtraOutputArgs { get; }

    /// <summary>
    /// Gets the filter chain fragments this encoder requires, e.g. VAAPI's "format=nv12,hwupload".
    /// </summary>
    public IReadOnlyList<string> RequiredVideoFilters { get; }

    /// <summary>
    /// Gets a value indicating whether this encoder accepts a "-preset" argument.
    /// </summary>
    public bool SupportsPreset { get; }
}

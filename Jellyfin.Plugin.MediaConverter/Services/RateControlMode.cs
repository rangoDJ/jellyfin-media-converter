namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Determines how the video encoder's output size/quality tradeoff is controlled.
/// </summary>
public enum RateControlMode
{
    /// <summary>
    /// Use a fixed quality value (CRF for software encoders, CQ/QP-style scales for hardware
    /// encoders) and let the encoder pick whatever bitrate that quality needs.
    /// </summary>
    Quality,

    /// <summary>
    /// Target a specific average video bitrate instead of a fixed quality value.
    /// </summary>
    Bitrate,

    /// <summary>
    /// Target half of each source file's own current video bitrate. Resolved per item (via
    /// ffprobe) rather than a single client-supplied value, so it works correctly across a batch
    /// of items with different source bitrates.
    /// </summary>
    HalfSourceBitrate
}

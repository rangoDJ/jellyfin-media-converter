namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Determines how subtitle streams are handled in the output.
/// </summary>
public enum SubtitleMode
{
    /// <summary>
    /// Copy all subtitle streams through unchanged.
    /// </summary>
    Copy,

    /// <summary>
    /// Drop all subtitle streams from the output.
    /// </summary>
    None
}

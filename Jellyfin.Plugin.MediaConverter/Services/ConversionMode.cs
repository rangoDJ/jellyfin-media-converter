namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Determines whether a conversion overwrites the original file or produces a new file alongside it.
/// </summary>
public enum ConversionMode
{
    /// <summary>
    /// Write the converted media to a new file alongside the original.
    /// </summary>
    Variant,

    /// <summary>
    /// Replace the original media file in place once conversion succeeds.
    /// </summary>
    Replace
}

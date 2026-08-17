using System;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// A saved rule the "Apply Media Converter rules" scheduled task uses to find and queue library
/// items automatically, instead of converting one item/season/series at a time from a search.
/// </summary>
public class ConversionRule
{
    /// <summary>
    /// Gets or sets the rule's unique id.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the rule's display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this rule is applied when the scheduled task runs.
    /// Disabled rules are kept (not deleted) so they can be re-enabled later.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the output container extension (without the leading dot), e.g. "mkv".
    /// </summary>
    public string Container { get; set; } = "mkv";

    /// <summary>
    /// Gets or sets the target codec family, e.g. "hevc". A movie/episode already encoded in this
    /// codec is skipped - this is both the encoding target and the "is this already done" check.
    /// </summary>
    public string VideoCodec { get; set; } = "hevc";

    /// <summary>
    /// Gets or sets the quality value passed to the encoder (lower is higher quality for the
    /// QSV/NVENC/AMF style quality scales; higher is higher quality for CRF-style software encoding).
    /// </summary>
    public int Quality { get; set; } = 23;

    /// <summary>
    /// Gets or sets whether matching items are converted in place or as a new variant alongside
    /// the original, needing a manual keep/delete decision afterward.
    /// </summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Replace;

    /// <summary>
    /// Gets or sets an optional case-insensitive substring the item's file path must contain to
    /// match this rule (e.g. a specific library folder). Null or empty matches every path.
    /// </summary>
    public string? PathContains { get; set; }
}

using Jellyfin.Plugin.MediaConverter.Services;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// The body of a create/update request for a <see cref="Services.ConversionRule"/>. Deliberately
/// excludes <c>Id</c> - the id is server-generated on create, or taken from the route on update.
/// </summary>
public class ConversionRuleRequestDto
{
    /// <summary>
    /// Gets or sets the rule's display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this rule is applied when the scheduled task runs.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the output container extension (without the leading dot), e.g. "mkv".
    /// </summary>
    public string Container { get; set; } = "mkv";

    /// <summary>
    /// Gets or sets the target codec family, e.g. "hevc".
    /// </summary>
    public string VideoCodec { get; set; } = "hevc";

    /// <summary>
    /// Gets or sets the quality value passed to the encoder.
    /// </summary>
    public int Quality { get; set; } = 23;

    /// <summary>
    /// Gets or sets whether matching items are converted in place or as a new variant.
    /// </summary>
    public ConversionMode Mode { get; set; } = ConversionMode.Replace;

    /// <summary>
    /// Gets or sets an optional case-insensitive path substring filter.
    /// </summary>
    public string? PathContains { get; set; }
}

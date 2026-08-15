using System;
using Jellyfin.Plugin.MediaConverter.Services;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// Side-by-side codec/quality stats for a completed "create new variant" job's original and variant files.
/// </summary>
public class VariantCompareDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VariantCompareDto"/> class.
    /// </summary>
    /// <param name="jobId">The job this comparison belongs to.</param>
    /// <param name="original">The original file's stats, or <see langword="null"/> if it could no longer be probed.</param>
    /// <param name="variant">The new variant file's stats, or <see langword="null"/> if it could no longer be probed.</param>
    public VariantCompareDto(Guid jobId, MediaInfo? original, MediaInfo? variant)
    {
        JobId = jobId;
        Original = original;
        Variant = variant;
    }

    /// <summary>
    /// Gets the job this comparison belongs to.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Gets the original file's stats, or <see langword="null"/> if it could no longer be probed.
    /// </summary>
    public MediaInfo? Original { get; }

    /// <summary>
    /// Gets the new variant file's stats, or <see langword="null"/> if it could no longer be probed.
    /// </summary>
    public MediaInfo? Variant { get; }
}

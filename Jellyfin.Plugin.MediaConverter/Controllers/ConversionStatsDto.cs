using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.MediaConverter.Services;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// Aggregate space-saved statistics across all finished conversion jobs.
/// </summary>
public class ConversionStatsDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionStatsDto"/> class, aggregating over
    /// the given jobs.
    /// </summary>
    /// <param name="jobs">All known jobs to aggregate over.</param>
    public ConversionStatsDto(IEnumerable<ConversionJob> jobs)
    {
        // Only count jobs where the space saving is final: a completed "Replace original" job, or
        // a "Create new variant" job whose new variant was actually kept (KeptVariant). A pending
        // decision or a KeptOriginal reversal hasn't actually freed any space.
        var counted = jobs
            .Where(j => j.Status == ConversionJobStatus.Completed
                && (j.Request.Mode == ConversionMode.Replace || j.VariantResolution == VariantResolution.KeptVariant)
                && j.SourceSizeBytes is > 0
                && j.OutputSizeBytes is >= 0)
            .ToList();

        CompletedJobCount = counted.Count;
        TotalSourceBytes = counted.Sum(j => j.SourceSizeBytes!.Value);
        TotalOutputBytes = counted.Sum(j => j.OutputSizeBytes!.Value);
        TotalSavedBytes = TotalSourceBytes - TotalOutputBytes;
        AverageCompressionRatio = TotalSourceBytes > 0
            ? (double)TotalOutputBytes / TotalSourceBytes
            : (double?)null;
    }

    /// <summary>
    /// Gets the number of finished, space-accounted jobs included in these stats.
    /// </summary>
    public int CompletedJobCount { get; }

    /// <summary>
    /// Gets the combined source file size, in bytes, across all counted jobs.
    /// </summary>
    public long TotalSourceBytes { get; }

    /// <summary>
    /// Gets the combined output file size, in bytes, across all counted jobs.
    /// </summary>
    public long TotalOutputBytes { get; }

    /// <summary>
    /// Gets the combined space saved, in bytes (<see cref="TotalSourceBytes"/> minus
    /// <see cref="TotalOutputBytes"/>), across all counted jobs.
    /// </summary>
    public long TotalSavedBytes { get; }

    /// <summary>
    /// Gets the average output-to-source size ratio (e.g. 0.55 means output files are, on average,
    /// 55% of their source's size); <see langword="null"/> if there's nothing to average yet.
    /// </summary>
    public double? AverageCompressionRatio { get; }
}

using System;
using Jellyfin.Plugin.MediaConverter.Services;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// Wire representation of a <see cref="ConversionJob"/>.
/// </summary>
public class JobDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobDto"/> class.
    /// </summary>
    /// <param name="job">The job to project.</param>
    public JobDto(ConversionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        Id = job.Id;
        Status = job.Status;
        ProgressPercent = job.ProgressPercent;
        ErrorMessage = job.ErrorMessage;
        SourcePath = job.SourcePath;
        OutputPath = job.OutputPath;
        Mode = job.Request.Mode;
        VariantResolution = job.VariantResolution;
        EtaSeconds = EstimateEtaSeconds(job);
    }

    /// <summary>
    /// Gets the job's unique id.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the job's current status.
    /// </summary>
    public ConversionJobStatus Status { get; }

    /// <summary>
    /// Gets the job's completion percentage, from 0 to 100.
    /// </summary>
    public double ProgressPercent { get; }

    /// <summary>
    /// Gets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the resolved path of the source media file.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the path the converted file is written to.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Gets whether the original file is replaced or a new variant was created.
    /// </summary>
    public ConversionMode Mode { get; }

    /// <summary>
    /// Gets whether a "create new variant" job's original/variant pair still needs a manual
    /// keep/delete decision.
    /// </summary>
    public VariantResolution VariantResolution { get; }

    /// <summary>
    /// Gets the estimated number of seconds remaining, extrapolated from the current progress
    /// rate; <see langword="null"/> unless the job is currently running with at least some
    /// progress recorded.
    /// </summary>
    public double? EtaSeconds { get; }

    private static double? EstimateEtaSeconds(ConversionJob job)
    {
        if (job.Status != ConversionJobStatus.Running || job.StartedAt is not { } startedAt || job.ProgressPercent <= 0)
        {
            return null;
        }

        var elapsed = DateTime.UtcNow - startedAt;
        var estimatedTotal = elapsed / (job.ProgressPercent / 100.0);
        var remaining = estimatedTotal - elapsed;
        return Math.Max(remaining.TotalSeconds, 0);
    }
}

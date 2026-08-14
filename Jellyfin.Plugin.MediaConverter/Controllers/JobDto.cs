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
}

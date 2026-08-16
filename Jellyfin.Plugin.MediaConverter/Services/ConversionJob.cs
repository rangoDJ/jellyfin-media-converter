using System;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Tracks the state and progress of a single conversion job.
/// </summary>
public class ConversionJob
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionJob"/> class.
    /// </summary>
    /// <param name="request">The request this job was created from.</param>
    /// <param name="sourcePath">The resolved path of the source media file.</param>
    /// <param name="outputPath">The path the converted file will be written to.</param>
    public ConversionJob(ConversionRequest request, string sourcePath, string outputPath)
    {
        Id = Guid.NewGuid();
        Request = request;
        SourcePath = sourcePath;
        OutputPath = outputPath;
        Status = ConversionJobStatus.Queued;
        CreatedAt = DateTime.UtcNow;
        VariantResolution = request.Mode == ConversionMode.Variant ? VariantResolution.PendingReview : VariantResolution.NotApplicable;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionJob"/> class from previously
    /// persisted state, restoring it exactly rather than deriving fields from scratch.
    /// </summary>
    /// <param name="id">The job's original unique id.</param>
    /// <param name="request">The request this job was created from.</param>
    /// <param name="sourcePath">The resolved path of the source media file.</param>
    /// <param name="outputPath">The path the converted file was (or would have been) written to.</param>
    /// <param name="status">The job's persisted status.</param>
    /// <param name="progressPercent">The job's persisted completion percentage.</param>
    /// <param name="errorMessage">The job's persisted error message, if any.</param>
    /// <param name="createdAt">The UTC time the job was originally created.</param>
    /// <param name="variantResolution">The job's persisted variant keep/delete decision state.</param>
    public ConversionJob(
        Guid id,
        ConversionRequest request,
        string sourcePath,
        string outputPath,
        ConversionJobStatus status,
        double progressPercent,
        string? errorMessage,
        DateTime createdAt,
        VariantResolution variantResolution)
    {
        Id = id;
        Request = request;
        SourcePath = sourcePath;
        OutputPath = outputPath;
        Status = status;
        ProgressPercent = progressPercent;
        ErrorMessage = errorMessage;
        CreatedAt = createdAt;
        VariantResolution = variantResolution;
    }

    /// <summary>
    /// Gets the unique id of this job.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the request this job was created from.
    /// </summary>
    public ConversionRequest Request { get; }

    /// <summary>
    /// Gets the resolved path of the source media file.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the path the converted file is written to before being finalized.
    /// </summary>
    public string OutputPath { get; }

    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public ConversionJobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the completion percentage of the running conversion, from 0 to 100.
    /// </summary>
    public double ProgressPercent { get; set; }

    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the UTC time the job was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Gets or sets whether a "create new variant" job's original/variant pair still needs a
    /// manual keep/delete decision. Always <see cref="VariantResolution.NotApplicable"/> for
    /// "Replace original" jobs.
    /// </summary>
    public VariantResolution VariantResolution { get; set; }
}

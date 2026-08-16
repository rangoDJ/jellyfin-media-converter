using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Queues and tracks media conversion jobs.
/// </summary>
public interface IConversionJobManager
{
    /// <summary>
    /// Queues a new conversion job for the given library item.
    /// </summary>
    /// <param name="request">The conversion parameters.</param>
    /// <returns>The newly created job.</returns>
    ConversionJob Enqueue(ConversionRequest request);

    /// <summary>
    /// Gets a previously queued or running job by id.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The job, or <see langword="null"/> if not found.</returns>
    ConversionJob? GetJob(Guid jobId);

    /// <summary>
    /// Gets all jobs known to the manager, most recently created first.
    /// </summary>
    /// <returns>The known jobs.</returns>
    IReadOnlyList<ConversionJob> GetJobs();

    /// <summary>
    /// Requests cancellation of a queued or running job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns><see langword="true"/> if the job was found and cancellation was requested.</returns>
    bool CancelJob(Guid jobId);

    /// <summary>
    /// Resolves a pending variant decision by promoting the new variant: it replaces the original
    /// file at its path (the same swap "Replace original" mode performs), and the original is deleted.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The outcome of the attempt.</returns>
    VariantResolveOutcome ResolveKeepVariant(Guid jobId);

    /// <summary>
    /// Resolves a pending variant decision by keeping the original file and deleting the new variant.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The outcome of the attempt.</returns>
    VariantResolveOutcome ResolveKeepOriginal(Guid jobId);

    /// <summary>
    /// Removes a finished job from history. Queued or running jobs must be cancelled first.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The outcome of the attempt.</returns>
    RemoveJobOutcome RemoveJob(Guid jobId);
}

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
}

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// The lifecycle state of a queued or running conversion job.
/// </summary>
public enum ConversionJobStatus
{
    /// <summary>
    /// The job is waiting for a worker slot.
    /// </summary>
    Queued,

    /// <summary>
    /// The job is actively being converted by ffmpeg.
    /// </summary>
    Running,

    /// <summary>
    /// The job finished successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The job failed. See <see cref="ConversionJob.ErrorMessage"/> for details.
    /// </summary>
    Failed,

    /// <summary>
    /// The job was cancelled before it finished.
    /// </summary>
    Cancelled
}

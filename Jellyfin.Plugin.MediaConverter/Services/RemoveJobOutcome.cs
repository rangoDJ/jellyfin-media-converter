namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// The result of attempting to remove a job from history.
/// </summary>
public enum RemoveJobOutcome
{
    /// <summary>
    /// The job was removed.
    /// </summary>
    Success,

    /// <summary>
    /// No job with the given id was found.
    /// </summary>
    JobNotFound,

    /// <summary>
    /// The job is still queued or running and must be cancelled before it can be removed.
    /// </summary>
    NotEligible
}

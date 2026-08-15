namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// The result of attempting to resolve a pending variant keep/delete decision.
/// </summary>
public enum VariantResolveOutcome
{
    /// <summary>
    /// The decision was applied successfully.
    /// </summary>
    Success,

    /// <summary>
    /// No job with the given id was found.
    /// </summary>
    JobNotFound,

    /// <summary>
    /// The job exists but isn't a completed "create new variant" job awaiting a decision.
    /// </summary>
    NotEligible
}

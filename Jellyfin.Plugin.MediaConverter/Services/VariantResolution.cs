namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Tracks whether a "create new variant" job's original/variant pair still needs a manual decision
/// about which one to keep.
/// </summary>
public enum VariantResolution
{
    /// <summary>
    /// The job did not create a variant pair (e.g. it used "Replace original" mode).
    /// </summary>
    NotApplicable,

    /// <summary>
    /// A variant was created and is awaiting a manual keep/delete decision.
    /// </summary>
    PendingReview,

    /// <summary>
    /// The user chose to keep the new variant; the original file was removed.
    /// </summary>
    KeptVariant,

    /// <summary>
    /// The user chose to keep the original file; the new variant was removed.
    /// </summary>
    KeptOriginal
}

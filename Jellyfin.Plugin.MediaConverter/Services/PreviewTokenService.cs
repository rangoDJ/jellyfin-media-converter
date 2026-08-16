using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Issues and validates short-lived, single-purpose tokens for streaming a specific job's
/// original/variant file for in-browser preview. A plain &lt;video src&gt; load can't carry the
/// request header Jellyfin's own token auth normally requires, and a query-string copy of that
/// token isn't honored by the "RequiresElevation" policy - so these endpoints bypass Jellyfin's
/// auth entirely and validate one of these self-issued tokens instead. The token itself is only
/// ever handed out through a normal, fully-authenticated request.
/// </summary>
public class PreviewTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, TokenInfo> _tokens = new();

    /// <summary>
    /// Issues a new token authorizing a single stream of the given job's original or variant file.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="isVariant">Whether the token is for the variant (vs. the original) file.</param>
    /// <returns>The issued token.</returns>
    public string IssueToken(Guid jobId, bool isVariant)
    {
        var token = Guid.NewGuid().ToString("N");
        _tokens[token] = new TokenInfo(jobId, isVariant, DateTime.UtcNow.Add(TokenLifetime));
        return token;
    }

    /// <summary>
    /// Validates a token against the job/file it should authorize.
    /// </summary>
    /// <param name="token">The token to validate.</param>
    /// <param name="jobId">The job id the caller is requesting a stream for.</param>
    /// <param name="isVariant">Whether the caller is requesting the variant (vs. the original) file.</param>
    /// <returns><see langword="true"/> if the token is valid, unexpired, and matches.</returns>
    public bool Validate(string? token, Guid jobId, bool isVariant)
    {
        if (string.IsNullOrEmpty(token) || !_tokens.TryGetValue(token, out var info))
        {
            return false;
        }

        if (info.ExpiresUtc < DateTime.UtcNow)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }

        return info.JobId == jobId && info.IsVariant == isVariant;
    }

    private sealed record TokenInfo(Guid JobId, bool IsVariant, DateTime ExpiresUtc);
}

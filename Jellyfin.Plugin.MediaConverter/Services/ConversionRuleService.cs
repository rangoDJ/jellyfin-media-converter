using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Scans the library against the configured <see cref="ConversionRule"/>s and queues conversions
/// for matching, not-yet-handled items. Used by both the scheduled task and a manual "run now"
/// trigger, so the matching logic only lives in one place.
/// </summary>
public class ConversionRuleService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IConversionJobManager _jobManager;
    private readonly ILogger<ConversionRuleService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionRuleService"/> class.
    /// </summary>
    /// <param name="libraryManager">Used to enumerate library movies/episodes.</param>
    /// <param name="jobManager">Used to queue matching items and to check what's already been handled.</param>
    /// <param name="logger">Logger for reporting what the rule scan did.</param>
    public ConversionRuleService(ILibraryManager libraryManager, IConversionJobManager jobManager, ILogger<ConversionRuleService> logger)
    {
        _libraryManager = libraryManager;
        _jobManager = jobManager;
        _logger = logger;
    }

    /// <summary>
    /// Scans every movie/episode in the library, queuing a conversion for the first enabled rule
    /// each one matches. An item already covered by a non-failed, non-cancelled job (from a
    /// previous rule run or a manual conversion) is skipped, so re-running this repeatedly (e.g. on
    /// a daily schedule) doesn't keep re-queuing the same files.
    /// </summary>
    /// <param name="progress">Optional progress reporter, 0-100.</param>
    /// <param name="cancellationToken">Token used to cancel a long-running scan.</param>
    /// <returns>The number of jobs queued.</returns>
    public int ApplyRules(IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var rules = (Plugin.Instance?.Configuration.ConversionRules ?? new List<ConversionRule>())
            .Where(r => r.Enabled)
            .ToList();

        if (rules.Count == 0)
        {
            _logger.LogInformation("Media Converter rule scan: no enabled rules, nothing to do");
            return 0;
        }

        var alreadyHandledItemIds = new HashSet<Guid>(
            _jobManager.GetJobs()
                .Where(j => j.Status != ConversionJobStatus.Failed && j.Status != ConversionJobStatus.Cancelled)
                .Select(j => j.Request.ItemId));

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            Recursive = true
        };

        var items = _libraryManager.GetItemList(query).OfType<Video>().ToList();
        var queuedCount = 0;

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];

            if (!alreadyHandledItemIds.Contains(item.Id))
            {
                var matchedRule = rules.FirstOrDefault(rule => Matches(item, rule));
                if (matchedRule is not null && TryQueue(item, matchedRule))
                {
                    queuedCount++;
                }
            }

            progress?.Report(items.Count > 0 ? (i + 1) * 100.0 / items.Count : 100);
        }

        _logger.LogInformation("Media Converter rule scan: queued {Count} job(s) out of {Total} library item(s) checked", queuedCount, items.Count);
        return queuedCount;
    }

    private bool TryQueue(Video item, ConversionRule rule)
    {
        try
        {
            _jobManager.Enqueue(new ConversionRequest
            {
                ItemId = item.Id,
                Container = rule.Container,
                VideoCodec = rule.VideoCodec,
                Quality = rule.Quality,
                Mode = rule.Mode
            });
            _logger.LogInformation("Media Converter rule '{RuleName}' queued {Path}", rule.Name, item.Path);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Media Converter rule '{RuleName}' could not queue {Path}", rule.Name, item.Path);
            return false;
        }
    }

    private static bool Matches(Video item, ConversionRule rule)
    {
        if (!string.IsNullOrEmpty(rule.PathContains)
            && (item.Path is null || item.Path.IndexOf(rule.PathContains, StringComparison.OrdinalIgnoreCase) < 0))
        {
            return false;
        }

        var currentCodec = item.GetMediaStreams().FirstOrDefault(s => s.Type == MediaStreamType.Video)?.Codec;
        return !string.Equals(currentCodec, rule.VideoCodec, StringComparison.OrdinalIgnoreCase);
    }
}

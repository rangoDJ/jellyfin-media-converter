using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Jellyfin scheduled task that applies this plugin's configured conversion rules to the whole
/// library. Has no default trigger - configure a schedule for it from Jellyfin's own Scheduled
/// Tasks dashboard page, the same way as any other task.
/// </summary>
public class ConversionRuleScheduledTask : IScheduledTask
{
    private readonly ConversionRuleService _ruleService;
    private readonly ILogger<ConversionRuleScheduledTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionRuleScheduledTask"/> class.
    /// </summary>
    /// <param name="ruleService">Runs the actual rule matching/queuing.</param>
    /// <param name="logger">Logger for reporting the scan's outcome.</param>
    public ConversionRuleScheduledTask(ConversionRuleService ruleService, ILogger<ConversionRuleScheduledTask> logger)
    {
        _ruleService = ruleService;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Apply Media Converter rules";

    /// <inheritdoc />
    public string Key => "MediaConverterApplyRules";

    /// <inheritdoc />
    public string Description => "Scans the library and queues conversions for items matching an enabled Media Converter rule.";

    /// <inheritdoc />
    public string Category => "Library";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Array.Empty<TaskTriggerInfo>();
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var queuedCount = _ruleService.ApplyRules(progress, cancellationToken);
        _logger.LogInformation("Media Converter scheduled task finished: queued {Count} job(s)", queuedCount);
        return Task.CompletedTask;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Background service that queues, runs, and tracks media conversion jobs, honoring the
/// configured maximum number of concurrent conversions.
/// </summary>
public sealed class ConversionJobManager : IConversionJobManager, IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly ILibraryMonitor _libraryMonitor;
    private readonly IDirectoryService _directoryService;
    private readonly ConversionEngine _engine;
    private readonly HardwareEncoderResolver _encoderResolver;
    private readonly ILogger<ConversionJobManager> _logger;
    private readonly ConcurrentDictionary<Guid, ConversionJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationSources = new();

    // Queued job ids, front-to-back (index 0 runs next). A plain lock-protected list rather than a
    // Channel<T> so a queued job can be moved to the front - Channel<T> has no removal/reordering
    // support. _queueSignal's count always matches _queuedJobIds.Count; a worker only ever takes
    // index 0 after acquiring a permit, so concurrent workers still drain strictly in list order.
    private readonly object _queueLock = new();
    private readonly List<Guid> _queuedJobIds = new();
    private readonly SemaphoreSlim _queueSignal = new(0);

    private readonly List<Task> _workers = new();
    private readonly object _persistenceLock = new();
    private CancellationTokenSource? _stoppingSource;
    private volatile bool _queuePaused;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionJobManager"/> class.
    /// </summary>
    /// <param name="libraryManager">Used to resolve library items from a job's item id.</param>
    /// <param name="providerManager">Used to trigger a metadata refresh once a job finishes.</param>
    /// <param name="libraryMonitor">Used to pause file-system watching while a job writes to disk.</param>
    /// <param name="directoryService">Used when constructing metadata refresh options.</param>
    /// <param name="engine">Runs the actual ffmpeg conversion for each job.</param>
    /// <param name="encoderResolver">Resolves which encoder to use for each job.</param>
    /// <param name="logger">Logger for reporting job failures.</param>
    public ConversionJobManager(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        ILibraryMonitor libraryMonitor,
        IDirectoryService directoryService,
        ConversionEngine engine,
        HardwareEncoderResolver encoderResolver,
        ILogger<ConversionJobManager> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _libraryMonitor = libraryMonitor;
        _directoryService = directoryService;
        _engine = engine;
        _encoderResolver = encoderResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsQueuePaused => _queuePaused;

    /// <inheritdoc />
    public ConversionJob Enqueue(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = _libraryManager.GetItemById(request.ItemId) as Video
            ?? throw new InvalidOperationException("The requested item was not found or is not a video.");

        var outputPath = BuildOutputPath(item, request);
        var job = new ConversionJob(request, item.Path, outputPath);
        _jobs[job.Id] = job;
        _cancellationSources[job.Id] = new CancellationTokenSource();
        SaveJobs();

        lock (_queueLock)
        {
            _queuedJobIds.Add(job.Id);
        }

        _queueSignal.Release();

        _logger.LogInformation("Enqueued job {JobId}: {SourcePath} -> {OutputPath}", job.Id, job.SourcePath, job.OutputPath);
        return job;
    }

    /// <inheritdoc />
    public ConversionJob? GetJob(Guid jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConversionJob> GetJobs()
    {
        return _jobs.Values.OrderByDescending(j => j.CreatedAt).ToList();
    }

    /// <inheritdoc />
    public bool CancelJob(Guid jobId)
    {
        if (!_cancellationSources.TryGetValue(jobId, out var source))
        {
            return false;
        }

        source.Cancel();
        return true;
    }

    /// <inheritdoc />
    public VariantResolveOutcome ResolveKeepVariant(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return VariantResolveOutcome.JobNotFound;
        }

        if (job.VariantResolution != VariantResolution.PendingReview || job.Status != ConversionJobStatus.Completed)
        {
            return VariantResolveOutcome.NotEligible;
        }

        var item = _libraryManager.GetItemById(job.Request.ItemId) as Video
            ?? throw new InvalidOperationException("The requested item was not found or is not a video.");

        FinalizeReplace(item, job);
        job.VariantResolution = VariantResolution.KeptVariant;
        SaveJobs();

        _providerManager.QueueRefresh(item.Id, new MetadataRefreshOptions(_directoryService), RefreshPriority.High);
        _logger.LogInformation("Job {JobId}: kept new variant, original replaced", jobId);
        return VariantResolveOutcome.Success;
    }

    /// <inheritdoc />
    public VariantResolveOutcome ResolveKeepOriginal(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return VariantResolveOutcome.JobNotFound;
        }

        if (job.VariantResolution != VariantResolution.PendingReview || job.Status != ConversionJobStatus.Completed)
        {
            return VariantResolveOutcome.NotEligible;
        }

        TryDeleteFile(job.OutputPath);
        job.VariantResolution = VariantResolution.KeptOriginal;
        SaveJobs();
        _logger.LogInformation("Job {JobId}: kept original, new variant deleted", jobId);
        return VariantResolveOutcome.Success;
    }

    /// <inheritdoc />
    public RemoveJobOutcome RemoveJob(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return RemoveJobOutcome.JobNotFound;
        }

        if (job.Status is ConversionJobStatus.Queued or ConversionJobStatus.Running)
        {
            return RemoveJobOutcome.NotEligible;
        }

        _jobs.TryRemove(jobId, out _);

        if (_cancellationSources.TryRemove(jobId, out var source))
        {
            source.Dispose();
        }

        SaveJobs();
        _logger.LogInformation("Job {JobId}: removed from history", jobId);
        return RemoveJobOutcome.Success;
    }

    /// <inheritdoc />
    public ConversionJob? RetryJob(Guid jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.Status != ConversionJobStatus.Failed)
        {
            return null;
        }

        _logger.LogInformation("Job {JobId}: retrying as a new job", jobId);
        return Enqueue(job.Request);
    }

    /// <inheritdoc />
    public bool MoveJobToFront(Guid jobId)
    {
        lock (_queueLock)
        {
            var index = _queuedJobIds.IndexOf(jobId);
            if (index < 0)
            {
                // Not queued at all (already running/finished, or unknown) - nothing to move.
                return false;
            }

            if (index > 0)
            {
                _queuedJobIds.RemoveAt(index);
                _queuedJobIds.Insert(0, jobId);
                _logger.LogInformation("Job {JobId}: moved to the front of the queue", jobId);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public void SetQueuePaused(bool paused)
    {
        _queuePaused = paused;
        _logger.LogInformation("Conversion queue {State}", paused ? "paused (will stop after the current job finishes)" : "resumed");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        LoadJobs();

        _stoppingSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var workerCount = Math.Max(1, Plugin.Instance?.Configuration.MaxConcurrentJobs ?? 1);

        for (var i = 0; i < workerCount; i++)
        {
            _workers.Add(Task.Run(() => ProcessQueueAsync(_stoppingSource.Token), CancellationToken.None));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_stoppingSource is not null)
        {
            await _stoppingSource.CancelAsync().ConfigureAwait(false);
        }

        try
        {
            await Task.WhenAll(_workers).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stoppingSource?.Dispose();
        _queueSignal.Dispose();

        foreach (var source in _cancellationSources.Values)
        {
            source.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {
                await _queueSignal.WaitAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            Guid jobId;
            lock (_queueLock)
            {
                if (_queuedJobIds.Count == 0)
                {
                    // Shouldn't normally happen (the signal count always matches the list count),
                    // but don't get stuck if it ever does.
                    continue;
                }

                jobId = _queuedJobIds[0];
                _queuedJobIds.RemoveAt(0);
            }

            if (_jobs.TryGetValue(jobId, out var job) && _cancellationSources.TryGetValue(jobId, out var cancellationSource))
            {
                await RunJobAsync(job, cancellationSource.Token).ConfigureAwait(false);
            }

            // Pausing doesn't touch the job that just finished - it only blocks this worker from
            // dequeuing the *next* job until resumed. Anything already queued stays Queued.
            while (_queuePaused)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task RunJobAsync(ConversionJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Job {JobId}: starting conversion of {SourcePath}", job.Id, job.SourcePath);
        job.Status = ConversionJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        SaveJobs();
        _libraryMonitor.ReportFileSystemChangeBeginning(job.SourcePath);

        try
        {
            var item = _libraryManager.GetItemById(job.Request.ItemId) as Video
                ?? throw new InvalidOperationException("The requested item was not found or is not a video.");
            var encoder = _encoderResolver.Resolve(job.Request.VideoCodec);
            _logger.LogInformation("Job {JobId}: resolved encoder {Encoder} for codec {Codec}", job.Id, encoder.Encoder, job.Request.VideoCodec);

            job.SourceSizeBytes = TryGetFileSize(job.SourcePath);

            var totalDurationTicks = item.RunTimeTicks ?? 0;
            if (totalDurationTicks <= 0)
            {
                _logger.LogInformation("Job {JobId}: RunTimeTicks missing, probing duration via ffprobe", job.Id);
                totalDurationTicks = await _engine.ProbeDurationTicksAsync(job.SourcePath, cancellationToken).ConfigureAwait(false);
            }

            await _engine.RunAsync(job, encoder, totalDurationTicks, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Job {JobId}: ffmpeg finished, writing to {OutputPath}", job.Id, job.OutputPath);

            if (totalDurationTicks > 0)
            {
                var outputDurationTicks = await _engine.ProbeDurationTicksAsync(job.OutputPath, cancellationToken).ConfigureAwait(false);

                // Below 90% of the source duration almost always means a truncated/corrupt output
                // (a crashed encode, a full disk mid-write, etc.) rather than a legitimate encode -
                // catching it here means the job is marked Failed instead of a silently broken
                // Completed job replacing/sitting next to the original.
                if (outputDurationTicks <= 0 || outputDurationTicks < totalDurationTicks * 0.9)
                {
                    throw new InvalidOperationException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Output verification failed: the converted file's duration ({0:c}) doesn't match the source's ({1:c}) - it may be truncated or corrupt.",
                        TimeSpan.FromTicks(Math.Max(outputDurationTicks, 0)),
                        TimeSpan.FromTicks(totalDurationTicks)));
                }

                _logger.LogInformation("Job {JobId}: output duration verified ({OutputDuration} vs source {SourceDuration})", job.Id, TimeSpan.FromTicks(outputDurationTicks), TimeSpan.FromTicks(totalDurationTicks));
            }

            job.OutputSizeBytes = TryGetFileSize(job.OutputPath);

            if (job.Request.Mode == ConversionMode.Replace)
            {
                FinalizeReplace(item, job);
                _logger.LogInformation("Job {JobId}: replaced original file at {Path}", job.Id, item.Path);
            }

            job.ProgressPercent = 100;
            job.Status = ConversionJobStatus.Completed;
            _logger.LogInformation("Job {JobId}: completed", job.Id);

            _providerManager.QueueRefresh(item.Id, new MetadataRefreshOptions(_directoryService), RefreshPriority.High);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Job {JobId}: cancelled", job.Id);
            job.Status = ConversionJobStatus.Cancelled;
            TryDeleteFile(job.OutputPath);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Conversion job {JobId} failed", job.Id);
            job.Status = ConversionJobStatus.Failed;
            job.ErrorMessage = ex.Message;
            TryDeleteFile(job.OutputPath);
        }
        finally
        {
            _libraryMonitor.ReportFileSystemChangeComplete(job.SourcePath, true);

            if (_cancellationSources.TryRemove(job.Id, out var source))
            {
                source.Dispose();
            }

            SaveJobs();
        }
    }

    private static void FinalizeReplace(BaseItem item, ConversionJob job)
    {
        var directory = Path.GetDirectoryName(item.Path)
            ?? throw new InvalidOperationException("Source item has no directory.");
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        var container = job.Request.Container.TrimStart('.');
        var finalPath = Path.Combine(directory, nameWithoutExtension + "." + container);

        File.Move(job.OutputPath, finalPath, true);

        if (!string.Equals(finalPath, item.Path, StringComparison.OrdinalIgnoreCase) && File.Exists(item.Path))
        {
            File.Delete(item.Path);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static long? TryGetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string BuildOutputPath(Video item, ConversionRequest request)
    {
        var directory = Path.GetDirectoryName(item.Path)
            ?? throw new InvalidOperationException("Source item has no directory.");
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(item.Path);
        var container = request.Container.TrimStart('.');

        if (request.Mode == ConversionMode.Variant)
        {
            var template = Plugin.Instance?.Configuration.VariantSuffixTemplate ?? "{name}-{codec}{ext}";
            var fileName = template
                .Replace("{name}", nameWithoutExtension, StringComparison.Ordinal)
                .Replace("{codec}", request.VideoCodec, StringComparison.Ordinal)
                .Replace("{ext}", "." + container, StringComparison.Ordinal);
            return Path.Combine(directory, fileName);
        }

        var suffix = Plugin.Instance?.Configuration.TempFileSuffix ?? ".mediaconverter.tmp";
        return Path.Combine(directory, nameWithoutExtension + suffix + "." + container);
    }

    private static string? GetJobsFilePath()
    {
        var dataFolder = Plugin.Instance?.DataFolderPath;
        return string.IsNullOrEmpty(dataFolder) ? null : Path.Combine(dataFolder, "jobs.json");
    }

    /// <summary>
    /// Persists the current job list to disk so history survives a server restart. Called after
    /// every state change (enqueue, status transition, variant decision) rather than continuously,
    /// since progress ticks alone don't need to survive a restart.
    /// </summary>
    private void SaveJobs()
    {
        var path = GetJobsFilePath();
        if (path is null)
        {
            return;
        }

        lock (_persistenceLock)
        {
            try
            {
                var snapshot = _jobs.Values.Select(j => new PersistedJob
                {
                    Id = j.Id,
                    Request = j.Request,
                    SourcePath = j.SourcePath,
                    OutputPath = j.OutputPath,
                    Status = j.Status,
                    ProgressPercent = j.ProgressPercent,
                    ErrorMessage = j.ErrorMessage,
                    CreatedAt = j.CreatedAt,
                    VariantResolution = j.VariantResolution,
                    SourceSizeBytes = j.SourceSizeBytes,
                    OutputSizeBytes = j.OutputSizeBytes
                }).ToList();

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot));
                File.Move(tempPath, path, true);
                _logger.LogDebug("Persisted {Count} job(s) to {Path}", snapshot.Count, path);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Unable to persist conversion job history to {Path}", path);
            }
        }
    }

    /// <summary>
    /// Restores the job list from disk on startup. Any job still marked Queued or Running when the
    /// server last stopped had its ffmpeg process killed along with it, so those are reclassified
    /// as failed rather than silently resumed, and any partial output file is cleaned up.
    /// </summary>
    private void LoadJobs()
    {
        var path = GetJobsFilePath();
        if (path is null)
        {
            _logger.LogInformation("No plugin data folder available; job history will not persist across restarts");
            return;
        }

        if (!File.Exists(path))
        {
            _logger.LogInformation("No persisted job history found at {Path} (first run, or none yet)", path);
            return;
        }

        List<PersistedJob>? snapshot;

        try
        {
            snapshot = JsonSerializer.Deserialize<List<PersistedJob>>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _logger.LogWarning(ex, "Unable to load persisted conversion job history from {Path}", path);
            return;
        }

        if (snapshot is null)
        {
            return;
        }

        _logger.LogInformation("Loading {Count} persisted job(s) from {Path}", snapshot.Count, path);

        foreach (var persisted in snapshot)
        {
            var status = persisted.Status;
            var errorMessage = persisted.ErrorMessage;

            if (status is ConversionJobStatus.Queued or ConversionJobStatus.Running)
            {
                _logger.LogInformation("Job {JobId} was {PreviousStatus} at last shutdown; marking as failed/interrupted", persisted.Id, status);
                status = ConversionJobStatus.Failed;
                errorMessage = "Interrupted by a server restart.";
                TryDeleteFile(persisted.OutputPath);
            }

            var job = new ConversionJob(
                persisted.Id,
                persisted.Request,
                persisted.SourcePath,
                persisted.OutputPath,
                status,
                persisted.ProgressPercent,
                errorMessage,
                persisted.CreatedAt,
                persisted.VariantResolution,
                persisted.SourceSizeBytes,
                persisted.OutputSizeBytes);

            _jobs[job.Id] = job;
        }
    }

    private sealed class PersistedJob
    {
        public Guid Id { get; set; }

        public ConversionRequest Request { get; set; } = new();

        public string SourcePath { get; set; } = string.Empty;

        public string OutputPath { get; set; } = string.Empty;

        public ConversionJobStatus Status { get; set; }

        public double ProgressPercent { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; }

        public VariantResolution VariantResolution { get; set; }

        public long? SourceSizeBytes { get; set; }

        public long? OutputSizeBytes { get; set; }
    }
}

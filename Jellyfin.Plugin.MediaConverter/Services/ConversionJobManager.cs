using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
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
public class ConversionJobManager : IConversionJobManager, IHostedService, IDisposable
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
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly List<Task> _workers = new();
    private CancellationTokenSource? _stoppingSource;

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
    public ConversionJob Enqueue(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = _libraryManager.GetItemById(request.ItemId) as Video
            ?? throw new InvalidOperationException("The requested item was not found or is not a video.");

        var outputPath = BuildOutputPath(item, request);
        var job = new ConversionJob(request, item.Path, outputPath);
        _jobs[job.Id] = job;
        _cancellationSources[job.Id] = new CancellationTokenSource();

        if (!_queue.Writer.TryWrite(job.Id))
        {
            throw new InvalidOperationException("Unable to queue the conversion job.");
        }

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
    public Task StartAsync(CancellationToken cancellationToken)
    {
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
        _queue.Writer.TryComplete();
        _stoppingSource?.Cancel();

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

        foreach (var source in _cancellationSources.Values)
        {
            source.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            if (!_jobs.TryGetValue(jobId, out var job) || !_cancellationSources.TryGetValue(jobId, out var cancellationSource))
            {
                continue;
            }

            await RunJobAsync(job, cancellationSource.Token).ConfigureAwait(false);
        }
    }

    private async Task RunJobAsync(ConversionJob job, CancellationToken cancellationToken)
    {
        job.Status = ConversionJobStatus.Running;
        _libraryMonitor.ReportFileSystemChangeBeginning(job.SourcePath);

        try
        {
            var item = _libraryManager.GetItemById(job.Request.ItemId) as Video
                ?? throw new InvalidOperationException("The requested item was not found or is not a video.");
            var encoder = _encoderResolver.Resolve(job.Request.VideoCodec);

            await _engine.RunAsync(job, encoder, item.RunTimeTicks ?? 0, cancellationToken).ConfigureAwait(false);

            if (job.Request.Mode == ConversionMode.Replace)
            {
                FinalizeReplace(item, job);
            }

            job.ProgressPercent = 100;
            job.Status = ConversionJobStatus.Completed;

            _providerManager.QueueRefresh(item.Id, new MetadataRefreshOptions(_directoryService), RefreshPriority.High);
        }
        catch (OperationCanceledException)
        {
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
}

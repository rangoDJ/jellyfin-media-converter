using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.MediaConverter.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// Dashboard-facing API for browsing the library and running media conversions.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("MediaConverter")]
public class MediaConverterController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IConversionJobManager _jobManager;
    private readonly MediaProbeService _probeService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaConverterController"/> class.
    /// </summary>
    /// <param name="libraryManager">Used to browse the library for convertible videos.</param>
    /// <param name="jobManager">Used to queue and track conversion jobs.</param>
    /// <param name="probeService">Used to read codec/quality stats directly via ffprobe.</param>
    public MediaConverterController(ILibraryManager libraryManager, IConversionJobManager jobManager, MediaProbeService probeService)
    {
        _libraryManager = libraryManager;
        _jobManager = jobManager;
        _probeService = probeService;
    }

    /// <summary>
    /// Searches the library by name, matching movies, episodes, and series. Series results don't
    /// carry a convertible file directly - use <see cref="GetSeriesEpisodes"/> to list their
    /// episodes and pick one to convert.
    /// </summary>
    /// <param name="searchTerm">An optional case-insensitive name filter.</param>
    /// <returns>The matching movies, episodes, and series.</returns>
    [HttpGet("Library")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<LibraryItemDto>> GetLibrary([FromQuery] string? searchTerm)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode, BaseItemKind.Series },
            Recursive = true,
            SearchTerm = searchTerm
        };

        var items = _libraryManager.GetItemList(query)
            .Select(ToDto)
            .Where(dto => dto is not null);

        return Ok(items);
    }

    /// <summary>
    /// Lists the episodes belonging to a series, ordered by season and episode number, so the user
    /// can pick which one to convert.
    /// </summary>
    /// <param name="seriesId">The series' library id.</param>
    /// <returns>The series' episodes.</returns>
    [HttpGet("Series/{seriesId}/Episodes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<LibraryItemDto>> GetSeriesEpisodes([FromRoute] Guid seriesId)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Episode },
            Recursive = true,
            AncestorIds = new[] { seriesId }
        };

        var episodes = _libraryManager.GetItemList(query)
            .OfType<Episode>()
            .OrderBy(e => e.ParentIndexNumber ?? 0)
            .ThenBy(e => e.IndexNumber ?? 0)
            .Select(ToDto)
            .Where(dto => dto is not null);

        return Ok(episodes);
    }

    /// <summary>
    /// Checks whether the folder containing <paramref name="filePath"/> is writable by the server
    /// process, caching the result per directory since a batch conversion commonly touches many
    /// files in the same folder.
    /// </summary>
    /// <param name="filePath">The library item's file path.</param>
    /// <param name="directoryCache">A cache of already-checked directories, shared across a single request.</param>
    /// <returns>An error message if the folder isn't writable; otherwise <see langword="null"/>.</returns>
    private static string? CheckWritable(string filePath, Dictionary<string, string?> directoryCache)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            return "Could not determine the containing folder for this item.";
        }

        if (!directoryCache.TryGetValue(directory, out var error))
        {
            error = IsDirectoryWritable(directory)
                ? null
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "The folder \"{0}\" is not writable by the Jellyfin server process. Check file system permissions before converting.",
                    directory);
            directoryCache[directory] = error;
        }

        return error;
    }

    private static bool IsDirectoryWritable(string directoryPath)
    {
        try
        {
            var probePath = Path.Combine(directoryPath, ".mediaconverter-writetest-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static LibraryItemDto? ToDto(BaseItem item)
    {
        return item switch
        {
            Series series => new LibraryItemDto(series.Id, series.Name, "Series", series.Path, null, null, null, null),
            Episode episode => new LibraryItemDto(episode.Id, episode.Name, "Episode", episode.Path, episode.RunTimeTicks, episode.SeriesName, episode.ParentIndexNumber, episode.IndexNumber),
            Video video => new LibraryItemDto(video.Id, video.Name, "Movie", video.Path, video.RunTimeTicks, null, null, null),
            _ => null
        };
    }

    /// <summary>
    /// Queues a new conversion job.
    /// </summary>
    /// <param name="request">The conversion parameters.</param>
    /// <returns>The newly created job.</returns>
    [HttpPost("Convert")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<JobDto> Convert([FromBody] ConvertRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_libraryManager.GetItemById(request.ItemId) is not Video item)
        {
            return NotFound();
        }

        var writabilityError = CheckWritable(item.Path, new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
        if (writabilityError is not null)
        {
            return BadRequest(writabilityError);
        }

        var conversionRequest = new ConversionRequest
        {
            ItemId = request.ItemId,
            Container = request.Container,
            VideoCodec = request.VideoCodec,
            Quality = request.Quality,
            Mode = request.Mode,
            Preset = request.Preset,
            ScaleHeight = request.ScaleHeight,
            AudioCodec = request.AudioCodec,
            AudioBitrateKbps = request.AudioBitrateKbps,
            SubtitleMode = request.SubtitleMode,
            FfmpegArgsOverride = request.FfmpegArgsOverride
        };

        var job = _jobManager.Enqueue(conversionRequest);
        return Ok(new JobDto(job));
    }

    /// <summary>
    /// Queues conversion jobs for multiple items at once (e.g. every episode in a season or series),
    /// all sharing the same conversion parameters.
    /// </summary>
    /// <param name="request">The shared conversion parameters and the item ids to convert.</param>
    /// <returns>The newly created jobs, in the same order as the requested item ids.</returns>
    [HttpPost("Convert/Batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<IEnumerable<JobDto>> ConvertBatch([FromBody] BatchConvertRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ItemIds.Count == 0)
        {
            return BadRequest("At least one item id is required.");
        }

        var directoryCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var itemId in request.ItemIds)
        {
            if (_libraryManager.GetItemById(itemId) is not Video item)
            {
                return NotFound(string.Format(CultureInfo.InvariantCulture, "Item {0} was not found or is not a video.", itemId));
            }

            var writabilityError = CheckWritable(item.Path, directoryCache);
            if (writabilityError is not null)
            {
                return BadRequest(writabilityError);
            }
        }

        var jobs = request.ItemIds.Select(itemId => _jobManager.Enqueue(new ConversionRequest
        {
            ItemId = itemId,
            Container = request.Container,
            VideoCodec = request.VideoCodec,
            Quality = request.Quality,
            Mode = request.Mode,
            Preset = request.Preset,
            ScaleHeight = request.ScaleHeight,
            AudioCodec = request.AudioCodec,
            AudioBitrateKbps = request.AudioBitrateKbps,
            SubtitleMode = request.SubtitleMode,
            FfmpegArgsOverride = request.FfmpegArgsOverride
        })).Select(job => new JobDto(job)).ToList();

        return Ok(jobs);
    }

    /// <summary>
    /// Probes a library item's current media file directly via ffprobe for codec/quality stats.
    /// </summary>
    /// <param name="itemId">The library item id.</param>
    /// <param name="cancellationToken">Token used to cancel the probe.</param>
    /// <returns>The probed stats, 404 if the item wasn't found, or 502 if the probe failed.</returns>
    [HttpGet("Items/{itemId}/MediaInfo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<MediaInfo>> GetMediaInfo([FromRoute] Guid itemId, CancellationToken cancellationToken)
    {
        if (_libraryManager.GetItemById(itemId) is not Video item)
        {
            return NotFound();
        }

        var info = await _probeService.ProbeAsync(item.Path, cancellationToken).ConfigureAwait(false);
        return info is null ? StatusCode(StatusCodes.Status502BadGateway, "Unable to probe the media file.") : Ok(info);
    }

    /// <summary>
    /// Probes the original and new variant files of a completed "create new variant" job, for
    /// side-by-side comparison before deciding which one to keep.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <param name="cancellationToken">Token used to cancel the probes.</param>
    /// <returns>The comparison, or 404/409 if the job isn't a completed, pending-review variant job.</returns>
    [HttpGet("Jobs/{jobId}/Compare")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VariantCompareDto>> CompareVariant([FromRoute] Guid jobId, CancellationToken cancellationToken)
    {
        var job = _jobManager.GetJob(jobId);
        if (job is null)
        {
            return NotFound();
        }

        if (job.VariantResolution != VariantResolution.PendingReview || job.Status != ConversionJobStatus.Completed)
        {
            return Conflict("This job has no pending original/variant decision to compare.");
        }

        var original = await _probeService.ProbeAsync(job.SourcePath, cancellationToken).ConfigureAwait(false);
        var variant = await _probeService.ProbeAsync(job.OutputPath, cancellationToken).ConfigureAwait(false);
        return Ok(new VariantCompareDto(jobId, original, variant));
    }

    /// <summary>
    /// Resolves a pending variant decision by promoting the new variant: it replaces the original
    /// file at its path, and the original is deleted.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>204 on success, 404 if the job wasn't found, or 409 if it isn't eligible.</returns>
    [HttpPost("Jobs/{jobId}/KeepVariant")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult KeepVariant([FromRoute] Guid jobId)
    {
        return ToActionResult(_jobManager.ResolveKeepVariant(jobId));
    }

    /// <summary>
    /// Resolves a pending variant decision by keeping the original file and deleting the new variant.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>204 on success, 404 if the job wasn't found, or 409 if it isn't eligible.</returns>
    [HttpPost("Jobs/{jobId}/KeepOriginal")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult KeepOriginal([FromRoute] Guid jobId)
    {
        return ToActionResult(_jobManager.ResolveKeepOriginal(jobId));
    }

    private ActionResult ToActionResult(VariantResolveOutcome outcome)
    {
        return outcome switch
        {
            VariantResolveOutcome.Success => NoContent(),
            VariantResolveOutcome.JobNotFound => NotFound(),
            _ => Conflict("This job has no pending original/variant decision.")
        };
    }

    /// <summary>
    /// Lists all known conversion jobs, most recently created first.
    /// </summary>
    /// <returns>The known jobs.</returns>
    [HttpGet("Jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<JobDto>> GetJobs()
    {
        return Ok(_jobManager.GetJobs().Select(j => new JobDto(j)));
    }

    /// <summary>
    /// Gets the current status of a single conversion job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>The job status, or 404 if not found.</returns>
    [HttpGet("Jobs/{jobId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<JobDto> GetJob([FromRoute] Guid jobId)
    {
        var job = _jobManager.GetJob(jobId);
        return job is null ? NotFound() : Ok(new JobDto(job));
    }

    /// <summary>
    /// Cancels a queued or running conversion job.
    /// </summary>
    /// <param name="jobId">The job id.</param>
    /// <returns>204 if cancellation was requested, 404 if the job was not found.</returns>
    [HttpPost("Jobs/{jobId}/Cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult CancelJob([FromRoute] Guid jobId)
    {
        return _jobManager.CancelJob(jobId) ? NoContent() : NotFound();
    }
}

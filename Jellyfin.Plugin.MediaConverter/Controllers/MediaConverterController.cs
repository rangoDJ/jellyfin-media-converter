using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaConverterController"/> class.
    /// </summary>
    /// <param name="libraryManager">Used to browse the library for convertible videos.</param>
    /// <param name="jobManager">Used to queue and track conversion jobs.</param>
    public MediaConverterController(ILibraryManager libraryManager, IConversionJobManager jobManager)
    {
        _libraryManager = libraryManager;
        _jobManager = jobManager;
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
    public ActionResult<JobDto> Convert([FromBody] ConvertRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

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

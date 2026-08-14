using System;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// A lightweight projection of a library item (movie, episode, or series) for the dashboard browser.
/// </summary>
public class LibraryItemDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryItemDto"/> class.
    /// </summary>
    /// <param name="id">The item's library id.</param>
    /// <param name="name">The item's display name.</param>
    /// <param name="type">The item's kind: "Movie", "Episode", or "Series".</param>
    /// <param name="path">The item's file (or folder, for series) path on disk.</param>
    /// <param name="runTimeTicks">The item's runtime, in ticks, if known.</param>
    /// <param name="seriesName">The parent series name, for episodes; <see langword="null"/> otherwise.</param>
    /// <param name="seasonNumber">The season number, for episodes; <see langword="null"/> otherwise.</param>
    /// <param name="episodeNumber">The episode number, for episodes; <see langword="null"/> otherwise.</param>
    public LibraryItemDto(Guid id, string name, string type, string path, long? runTimeTicks, string? seriesName, int? seasonNumber, int? episodeNumber)
    {
        Id = id;
        Name = name;
        Type = type;
        Path = path;
        RunTimeTicks = runTimeTicks;
        SeriesName = seriesName;
        SeasonNumber = seasonNumber;
        EpisodeNumber = episodeNumber;
    }

    /// <summary>
    /// Gets the item's library id.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the item's display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the item's kind: "Movie", "Episode", or "Series".
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the item's file (or folder, for series) path on disk.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the item's runtime, in ticks, if known.
    /// </summary>
    public long? RunTimeTicks { get; }

    /// <summary>
    /// Gets the parent series name, for episodes; <see langword="null"/> otherwise.
    /// </summary>
    public string? SeriesName { get; }

    /// <summary>
    /// Gets the season number, for episodes; <see langword="null"/> otherwise.
    /// </summary>
    public int? SeasonNumber { get; }

    /// <summary>
    /// Gets the episode number, for episodes; <see langword="null"/> otherwise.
    /// </summary>
    public int? EpisodeNumber { get; }
}

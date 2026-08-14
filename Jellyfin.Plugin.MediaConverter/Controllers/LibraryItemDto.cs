using System;

namespace Jellyfin.Plugin.MediaConverter.Controllers;

/// <summary>
/// A lightweight projection of a library video item for the dashboard browser.
/// </summary>
public class LibraryItemDto
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryItemDto"/> class.
    /// </summary>
    /// <param name="id">The item's library id.</param>
    /// <param name="name">The item's display name.</param>
    /// <param name="path">The item's file path on disk.</param>
    /// <param name="runTimeTicks">The item's runtime, in ticks, if known.</param>
    public LibraryItemDto(Guid id, string name, string path, long? runTimeTicks)
    {
        Id = id;
        Name = name;
        Path = path;
        RunTimeTicks = runTimeTicks;
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
    /// Gets the item's file path on disk.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the item's runtime, in ticks, if known.
    /// </summary>
    public long? RunTimeTicks { get; }
}

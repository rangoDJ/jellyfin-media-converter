using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Makes a source file playable in an HTML5 &lt;video&gt; element when its container isn't
/// natively browser-friendly (e.g. Matroska), by remuxing it into an MP4 container via a
/// stream copy - no video/audio re-encoding, just repackaging the exact same bitstreams so the
/// browser can open the file. Results are cached on disk so repeat requests (and HTTP range
/// requests for seeking) don't require remuxing again.
/// </summary>
[SuppressMessage(
    "Security",
    "CA3003:Review code for file path injection vulnerabilities",
    Justification = "sourcePath/cacheKey are not built from raw user input - they originate from a job's " +
        "already-resolved SourcePath/OutputPath and its own Guid id, never a request body/query string value " +
        "used directly as a path.")]
public class PreviewRemuxService
{
    private static readonly string[] BrowserFriendlyExtensions = { ".mp4", ".m4v", ".webm" };

    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<PreviewRemuxService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewRemuxService"/> class.
    /// </summary>
    /// <param name="mediaEncoder">Provides the path to the server's own ffmpeg binary.</param>
    /// <param name="logger">Logger for reporting remux failures.</param>
    public PreviewRemuxService(IMediaEncoder mediaEncoder, ILogger<PreviewRemuxService> logger)
    {
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <summary>
    /// Returns a path to a browser-playable version of <paramref name="sourcePath"/>: the source
    /// itself if its container is already browser-friendly, or a cached stream-copy remux to MP4
    /// otherwise.
    /// </summary>
    /// <param name="sourcePath">The source file's path.</param>
    /// <param name="cacheKey">A stable, unique key identifying this source (e.g. "{jobId}-original").</param>
    /// <param name="cancellationToken">Token used to cancel the remux.</param>
    /// <returns>The path to serve for playback.</returns>
    public async Task<string> GetPlayablePathAsync(string sourcePath, string cacheKey, CancellationToken cancellationToken)
    {
        if (Array.IndexOf(BrowserFriendlyExtensions, Path.GetExtension(sourcePath).ToLowerInvariant()) >= 0)
        {
            return sourcePath;
        }

        var cacheDir = Path.Combine(Path.GetTempPath(), "jellyfin-media-converter-preview");
        Directory.CreateDirectory(cacheDir);
        var cachedPath = Path.Combine(cacheDir, cacheKey + ".mp4");

        if (File.Exists(cachedPath))
        {
            return cachedPath;
        }

        var tempPath = cachedPath + ".tmp";

        // Many audio codecs found in Matroska rips (DTS, TrueHD, etc.) have no valid sample entry
        // in the ISO base media format, so ffmpeg's MP4 muxer rejects a stream-copy remux that
        // includes them ("codec not currently supported in container"). Retry video-only in that
        // case rather than giving up entirely - silent playback beats no playback.
        var remuxed = await TryRemuxAsync(sourcePath, tempPath, includeAudio: true, cancellationToken).ConfigureAwait(false)
            || await TryRemuxAsync(sourcePath, tempPath, includeAudio: false, cancellationToken).ConfigureAwait(false);

        if (!remuxed)
        {
            _logger.LogWarning("Unable to remux {SourcePath} into a browser-playable MP4; falling back to direct playback", sourcePath);
            TryDeleteFile(tempPath);
            return sourcePath;
        }

        File.Move(tempPath, cachedPath, true);
        return cachedPath;
    }

    private async Task<bool> TryRemuxAsync(string sourcePath, string tempPath, bool includeAudio, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _mediaEncoder.EncoderPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0:v:0");

        if (includeAudio)
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:a:0?");
        }
        else
        {
            startInfo.ArgumentList.Add("-an");
        }

        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("copy");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("mp4");
        startInfo.ArgumentList.Add(tempPath);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode == 0 && File.Exists(tempPath))
        {
            return true;
        }

        if (!includeAudio)
        {
            _logger.LogWarning("Video-only remux of {SourcePath} also failed", sourcePath);
        }

        TryDeleteFile(tempPath);
        return false;
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
}

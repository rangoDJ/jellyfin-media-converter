using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Probes a media file's codec/quality stats directly via ffprobe, independent of Jellyfin's own
/// (sometimes stale or incomplete) library metadata.
/// </summary>
public class MediaProbeService
{
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<MediaProbeService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaProbeService"/> class.
    /// </summary>
    /// <param name="mediaEncoder">Provides the path to the server's own ffprobe binary.</param>
    /// <param name="logger">Logger for reporting probe failures.</param>
    public MediaProbeService(IMediaEncoder mediaEncoder, ILogger<MediaProbeService> logger)
    {
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <summary>
    /// Probes a media file and returns its codec/quality stats.
    /// </summary>
    /// <param name="path">The media file's path.</param>
    /// <param name="cancellationToken">Token used to cancel the probe.</param>
    /// <returns>The probed stats, or <see langword="null"/> if the file could not be probed.</returns>
    public async Task<MediaInfo?> ProbeAsync(string path, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _mediaEncoder.ProbePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-print_format");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("-show_format");
        startInfo.ArgumentList.Add("-show_streams");
        startInfo.ArgumentList.Add(path);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("ffprobe exited with code {ExitCode} while probing {Path}", process.ExitCode, path);
            return null;
        }

        try
        {
            return ParseProbeOutput(path, output);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unable to parse ffprobe output for {Path}", path);
            return null;
        }
    }

    private static MediaInfo ParseProbeOutput(string path, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var info = new MediaInfo { Path = path };

        if (root.TryGetProperty("format", out var format))
        {
            if (format.TryGetProperty("size", out var size) && long.TryParse(GetRawString(size), NumberStyles.Integer, CultureInfo.InvariantCulture, out var sizeBytes))
            {
                info.FileSizeBytes = sizeBytes;
            }

            if (format.TryGetProperty("duration", out var duration) && double.TryParse(GetRawString(duration), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                info.DurationTicks = (long)(seconds * TimeSpan.TicksPerSecond);
            }

            if (format.TryGetProperty("bit_rate", out var bitRate) && long.TryParse(GetRawString(bitRate), NumberStyles.Integer, CultureInfo.InvariantCulture, out var overallBitRate))
            {
                info.OverallBitRate = overallBitRate;
            }

            if (format.TryGetProperty("format_name", out var formatName))
            {
                info.Container = formatName.GetString();
            }
        }

        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var codecTypeElement) ? codecTypeElement.GetString() : null;

                if (codecType == "video" && info.VideoCodec is null)
                {
                    info.VideoCodec = stream.TryGetProperty("codec_name", out var vCodec) ? vCodec.GetString() : null;
                    info.Width = TryGetInt(stream, "width");
                    info.Height = TryGetInt(stream, "height");
                    info.VideoBitRate = TryGetLong(stream, "bit_rate");
                }
                else if (codecType == "audio" && info.AudioCodec is null)
                {
                    info.AudioCodec = stream.TryGetProperty("codec_name", out var aCodec) ? aCodec.GetString() : null;
                    info.AudioChannels = TryGetInt(stream, "channels");
                    info.AudioBitRate = TryGetLong(stream, "bit_rate");
                }
            }
        }

        return info;
    }

    private static string? GetRawString(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && int.TryParse(GetRawString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? TryGetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && long.TryParse(GetRawString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Runs a single ffmpeg conversion for a <see cref="ConversionJob"/>, reporting live progress back
/// onto the job as the process runs.
/// </summary>
public class ConversionEngine
{
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILogger<ConversionEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversionEngine"/> class.
    /// </summary>
    /// <param name="mediaEncoder">Provides the path to the server's own ffmpeg binary.</param>
    /// <param name="logger">Logger for reporting ffmpeg failures.</param>
    public ConversionEngine(IMediaEncoder mediaEncoder, ILogger<ConversionEngine> logger)
    {
        _mediaEncoder = mediaEncoder;
        _logger = logger;
    }

    /// <summary>
    /// Probes a media file's duration directly via ffprobe. Used as a fallback when the library
    /// item's own RunTimeTicks metadata is missing (common for freshly-added or partially-scanned
    /// items), since progress reporting depends on knowing the total duration up front.
    /// </summary>
    /// <param name="path">The media file's path.</param>
    /// <param name="cancellationToken">Token used to cancel the probe.</param>
    /// <returns>The duration in ticks, or 0 if it could not be determined.</returns>
    public async Task<long> ProbeDurationTicksAsync(string path, CancellationToken cancellationToken)
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
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format=duration");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        startInfo.ArgumentList.Add(path);

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode == 0 && double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return (long)(seconds * TimeSpan.TicksPerSecond);
        }

        _logger.LogWarning("Unable to probe duration for {Path}; progress reporting will be unavailable for this job", path);
        return 0;
    }

    /// <summary>
    /// Runs ffmpeg for the given job, updating <see cref="ConversionJob.ProgressPercent"/> as it
    /// progresses. Throws if ffmpeg exits with a non-zero code.
    /// </summary>
    /// <param name="job">The job to convert.</param>
    /// <param name="encoder">The resolved encoder to use.</param>
    /// <param name="totalDurationTicks">The source media's total duration, in ticks, used to compute progress.</param>
    /// <param name="cancellationToken">Token used to cancel the running ffmpeg process.</param>
    /// <returns>A task that completes when the conversion finishes.</returns>
    public async Task RunAsync(ConversionJob job, EncoderSelection encoder, long totalDurationTicks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(encoder);

        var startInfo = new ProcessStartInfo
        {
            FileName = _mediaEncoder.EncoderPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        BuildArguments(startInfo, job, encoder);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, args) => OnProgressLine(job, totalDurationTicks, args.Data);

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            _logger.LogError("ffmpeg exited with code {ExitCode} while converting job {JobId}", process.ExitCode, job.Id);
            throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, "ffmpeg exited with code {0}: {1}", process.ExitCode, stderr.ToString()));
        }
    }

    private static void BuildArguments(ProcessStartInfo startInfo, ConversionJob job, EncoderSelection encoder)
    {
        startInfo.ArgumentList.Add("-y");

        foreach (var arg in encoder.ExtraInputArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(job.SourcePath);
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("0");

        if (!string.IsNullOrWhiteSpace(job.Request.FfmpegArgsOverride))
        {
            foreach (var arg in job.Request.FfmpegArgsOverride.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                startInfo.ArgumentList.Add(arg);
            }
        }
        else
        {
            startInfo.ArgumentList.Add("-c:v");
            startInfo.ArgumentList.Add(encoder.Encoder);

            if (job.Request.RateControlMode == RateControlMode.Bitrate && job.Request.VideoBitrateKbps is > 0)
            {
                startInfo.ArgumentList.Add("-b:v");
                startInfo.ArgumentList.Add(job.Request.VideoBitrateKbps.Value.ToString(CultureInfo.InvariantCulture) + "k");
            }
            else
            {
                startInfo.ArgumentList.Add(encoder.QualityFlag);
                startInfo.ArgumentList.Add(job.Request.Quality.ToString(CultureInfo.InvariantCulture));
            }

            if (job.Request.MaxVideoBitrateKbps is > 0)
            {
                var maxRateArg = job.Request.MaxVideoBitrateKbps.Value.ToString(CultureInfo.InvariantCulture) + "k";
                startInfo.ArgumentList.Add("-maxrate");
                startInfo.ArgumentList.Add(maxRateArg);
                startInfo.ArgumentList.Add("-bufsize");
                startInfo.ArgumentList.Add((job.Request.MaxVideoBitrateKbps.Value * 2).ToString(CultureInfo.InvariantCulture) + "k");
            }

            if (!string.IsNullOrWhiteSpace(job.Request.Preset) && encoder.SupportsPreset)
            {
                startInfo.ArgumentList.Add("-preset");
                startInfo.ArgumentList.Add(job.Request.Preset);
            }

            var filters = new List<string>();
            if (job.Request.ScaleHeight is > 0)
            {
                filters.Add("scale=-2:" + job.Request.ScaleHeight.Value.ToString(CultureInfo.InvariantCulture));
            }

            filters.AddRange(encoder.RequiredVideoFilters);

            if (filters.Count > 0)
            {
                startInfo.ArgumentList.Add("-vf");
                startInfo.ArgumentList.Add(string.Join(',', filters));
            }

            foreach (var arg in encoder.ExtraOutputArgs)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var audioCodec = string.IsNullOrWhiteSpace(job.Request.AudioCodec) ? "copy" : job.Request.AudioCodec;
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add(audioCodec);

            if (!string.Equals(audioCodec, "copy", StringComparison.OrdinalIgnoreCase) && job.Request.AudioBitrateKbps is > 0)
            {
                startInfo.ArgumentList.Add("-b:a");
                startInfo.ArgumentList.Add(job.Request.AudioBitrateKbps.Value.ToString(CultureInfo.InvariantCulture) + "k");
            }

            if (job.Request.SubtitleMode == SubtitleMode.None)
            {
                startInfo.ArgumentList.Add("-sn");
            }
            else
            {
                startInfo.ArgumentList.Add("-c:s");
                startInfo.ArgumentList.Add("copy");
            }
        }

        startInfo.ArgumentList.Add("-progress");
        startInfo.ArgumentList.Add("pipe:1");
        startInfo.ArgumentList.Add("-nostats");
        startInfo.ArgumentList.Add(job.OutputPath);
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void OnProgressLine(ConversionJob job, long totalDurationTicks, string? line)
    {
        if (string.IsNullOrEmpty(line) || totalDurationTicks <= 0)
        {
            return;
        }

        var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return;
        }

        var key = line[..separatorIndex];
        var value = line[(separatorIndex + 1)..];

        if (key == "out_time_us" && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var outTimeUs))
        {
            var elapsedTicks = outTimeUs * 10L;
            job.ProgressPercent = Math.Clamp(elapsedTicks * 100d / totalDurationTicks, 0, 100);
        }
    }
}

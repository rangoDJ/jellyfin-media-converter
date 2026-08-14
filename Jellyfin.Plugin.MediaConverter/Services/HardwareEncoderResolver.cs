using System;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.MediaConverter.Services;

/// <summary>
/// Resolves which ffmpeg video encoder and hwaccel arguments to use for a requested codec family,
/// based on the hardware transcoder already configured in Jellyfin's own playback settings.
/// </summary>
public class HardwareEncoderResolver
{
    private static readonly string[] QsvInitArgs = { "-init_hw_device", "qsv=hw", "-filter_hw_device", "hw" };
    private static readonly string[] VaapiFormatArgs = { "-vf", "format=nv12,hwupload" };

    private readonly IServerConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="HardwareEncoderResolver"/> class.
    /// </summary>
    /// <param name="configurationManager">Provides access to the server's encoding configuration.</param>
    public HardwareEncoderResolver(IServerConfigurationManager configurationManager)
    {
        _configurationManager = configurationManager;
    }

    /// <summary>
    /// Resolves the encoder to use for the given codec family.
    /// </summary>
    /// <param name="codecFamily">The requested codec family, e.g. "hevc", "h264" or "av1".</param>
    /// <returns>The resolved encoder and its extra arguments.</returns>
    public EncoderSelection Resolve(string codecFamily)
    {
        var options = _configurationManager.GetConfiguration<EncodingOptions>("encoding");
        var codec = (codecFamily ?? "hevc").Trim().ToLowerInvariant();

        return (options.HardwareAccelerationType ?? string.Empty).ToLowerInvariant() switch
        {
            "qsv" => ResolveQsv(codec),
            "nvenc" => ResolveNvenc(codec),
            "amf" => ResolveAmf(codec),
            "vaapi" => ResolveVaapi(codec, options.VaapiDevice),
            "videotoolbox" => ResolveVideoToolbox(codec),
            _ => ResolveSoftware(codec)
        };
    }

    private static EncoderSelection ResolveQsv(string codec)
    {
        var encoder = codec switch
        {
            "h264" => "h264_qsv",
            "av1" => "av1_qsv",
            _ => "hevc_qsv"
        };

        return new EncoderSelection(
            encoder,
            "-global_quality",
            QsvInitArgs,
            Array.Empty<string>());
    }

    private static EncoderSelection ResolveNvenc(string codec)
    {
        var encoder = codec switch
        {
            "h264" => "h264_nvenc",
            "av1" => "av1_nvenc",
            _ => "hevc_nvenc"
        };

        return new EncoderSelection(encoder, "-cq", Array.Empty<string>(), Array.Empty<string>());
    }

    private static EncoderSelection ResolveAmf(string codec)
    {
        var encoder = codec switch
        {
            "h264" => "h264_amf",
            "av1" => "av1_amf",
            _ => "hevc_amf"
        };

        return new EncoderSelection(encoder, "-qp_i", Array.Empty<string>(), Array.Empty<string>());
    }

    private static EncoderSelection ResolveVaapi(string codec, string? vaapiDevice)
    {
        var encoder = codec switch
        {
            "h264" => "h264_vaapi",
            "av1" => "av1_vaapi",
            _ => "hevc_vaapi"
        };

        var device = string.IsNullOrWhiteSpace(vaapiDevice) ? "/dev/dri/renderD128" : vaapiDevice;

        return new EncoderSelection(
            encoder,
            "-qp",
            new[] { "-init_hw_device", "vaapi=hw:" + device, "-filter_hw_device", "hw" },
            VaapiFormatArgs);
    }

    private static EncoderSelection ResolveVideoToolbox(string codec)
    {
        var encoder = codec == "h264" ? "h264_videotoolbox" : "hevc_videotoolbox";

        return new EncoderSelection(encoder, "-q:v", Array.Empty<string>(), Array.Empty<string>());
    }

    private static EncoderSelection ResolveSoftware(string codec)
    {
        var encoder = codec switch
        {
            "h264" => "libx264",
            "av1" => "libsvtav1",
            _ => "libx265"
        };

        return new EncoderSelection(encoder, "-crf", Array.Empty<string>(), Array.Empty<string>());
    }
}

using Jellyfin.Plugin.MediaConverter.Services;
using Xunit;

namespace Jellyfin.Plugin.MediaConverter.Tests;

public class ConversionJobTests
{
    [Fact]
    public void NewJob_VariantMode_DefaultsToPendingReview()
    {
        var request = new ConversionRequest { Mode = ConversionMode.Variant };

        var job = new ConversionJob(request, "/source.mkv", "/output.mkv");

        Assert.Equal(VariantResolution.PendingReview, job.VariantResolution);
        Assert.Equal(ConversionJobStatus.Queued, job.Status);
    }

    [Fact]
    public void NewJob_ReplaceMode_DefaultsToNotApplicable()
    {
        var request = new ConversionRequest { Mode = ConversionMode.Replace };

        var job = new ConversionJob(request, "/source.mkv", "/output.mkv");

        Assert.Equal(VariantResolution.NotApplicable, job.VariantResolution);
    }

    [Fact]
    public void NewJob_GetsAUniqueId()
    {
        var request = new ConversionRequest();

        var first = new ConversionJob(request, "/source.mkv", "/output1.mkv");
        var second = new ConversionJob(request, "/source.mkv", "/output2.mkv");

        Assert.NotEqual(first.Id, second.Id);
    }
}

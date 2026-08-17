using System.Collections.Generic;
using Jellyfin.Plugin.MediaConverter.Controllers;
using Jellyfin.Plugin.MediaConverter.Services;
using Xunit;

namespace Jellyfin.Plugin.MediaConverter.Tests;

public class ConversionStatsDtoTests
{
    private static ConversionJob CreateJob(
        ConversionMode mode,
        ConversionJobStatus status,
        VariantResolution? variantResolution = null,
        long? sourceSizeBytes = null,
        long? outputSizeBytes = null)
    {
        var request = new ConversionRequest { Mode = mode };
        var job = new ConversionJob(request, "/source.mkv", "/output.mkv")
        {
            Status = status,
            SourceSizeBytes = sourceSizeBytes,
            OutputSizeBytes = outputSizeBytes
        };

        if (variantResolution.HasValue)
        {
            job.VariantResolution = variantResolution.Value;
        }

        return job;
    }

    [Fact]
    public void EmptyJobList_ProducesZeroStats()
    {
        var stats = new ConversionStatsDto(new List<ConversionJob>());

        Assert.Equal(0, stats.CompletedJobCount);
        Assert.Equal(0, stats.TotalSourceBytes);
        Assert.Equal(0, stats.TotalOutputBytes);
        Assert.Equal(0, stats.TotalSavedBytes);
        Assert.Null(stats.AverageCompressionRatio);
    }

    [Fact]
    public void CompletedReplaceJob_IsCounted()
    {
        var job = CreateJob(ConversionMode.Replace, ConversionJobStatus.Completed, sourceSizeBytes: 1000, outputSizeBytes: 400);

        var stats = new ConversionStatsDto(new[] { job });

        Assert.Equal(1, stats.CompletedJobCount);
        Assert.Equal(1000, stats.TotalSourceBytes);
        Assert.Equal(400, stats.TotalOutputBytes);
        Assert.Equal(600, stats.TotalSavedBytes);
        Assert.Equal(0.4, stats.AverageCompressionRatio);
    }

    [Fact]
    public void CompletedVariantJob_KeptVariant_IsCounted()
    {
        var job = CreateJob(
            ConversionMode.Variant,
            ConversionJobStatus.Completed,
            VariantResolution.KeptVariant,
            sourceSizeBytes: 2000,
            outputSizeBytes: 1000);

        var stats = new ConversionStatsDto(new[] { job });

        Assert.Equal(1, stats.CompletedJobCount);
        Assert.Equal(1000, stats.TotalSavedBytes);
    }

    [Theory]
    [InlineData(null)] // Still PendingReview (default for a Variant-mode job).
    [InlineData(VariantResolution.KeptOriginal)] // Reverted - nothing was actually saved.
    public void CompletedVariantJob_NotYetKept_IsExcluded(VariantResolution? resolution)
    {
        var job = CreateJob(
            ConversionMode.Variant,
            ConversionJobStatus.Completed,
            resolution,
            sourceSizeBytes: 2000,
            outputSizeBytes: 1000);

        var stats = new ConversionStatsDto(new[] { job });

        Assert.Equal(0, stats.CompletedJobCount);
        Assert.Equal(0, stats.TotalSavedBytes);
    }

    [Theory]
    [InlineData(ConversionJobStatus.Queued)]
    [InlineData(ConversionJobStatus.Running)]
    [InlineData(ConversionJobStatus.Failed)]
    [InlineData(ConversionJobStatus.Cancelled)]
    public void NonCompletedJob_IsExcludedRegardlessOfMode(ConversionJobStatus status)
    {
        var job = CreateJob(ConversionMode.Replace, status, sourceSizeBytes: 1000, outputSizeBytes: 400);

        var stats = new ConversionStatsDto(new[] { job });

        Assert.Equal(0, stats.CompletedJobCount);
    }

    [Fact]
    public void JobsMissingSizeData_AreExcludedFromTotals()
    {
        var job = CreateJob(ConversionMode.Replace, ConversionJobStatus.Completed, sourceSizeBytes: null, outputSizeBytes: null);

        var stats = new ConversionStatsDto(new[] { job });

        Assert.Equal(0, stats.CompletedJobCount);
    }

    [Fact]
    public void MultipleJobs_AggregateAcrossAll()
    {
        var jobs = new[]
        {
            CreateJob(ConversionMode.Replace, ConversionJobStatus.Completed, sourceSizeBytes: 1000, outputSizeBytes: 600),
            CreateJob(ConversionMode.Variant, ConversionJobStatus.Completed, VariantResolution.KeptVariant, sourceSizeBytes: 4000, outputSizeBytes: 1000),
            CreateJob(ConversionMode.Variant, ConversionJobStatus.Completed, VariantResolution.PendingReview, sourceSizeBytes: 9999, outputSizeBytes: 9999),
            CreateJob(ConversionMode.Replace, ConversionJobStatus.Failed, sourceSizeBytes: 9999, outputSizeBytes: 9999)
        };

        var stats = new ConversionStatsDto(jobs);

        Assert.Equal(2, stats.CompletedJobCount);
        Assert.Equal(5000, stats.TotalSourceBytes);
        Assert.Equal(1600, stats.TotalOutputBytes);
        Assert.Equal(3400, stats.TotalSavedBytes);
    }
}

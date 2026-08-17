using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class TimeTrustEvaluatorTests : IDisposable
{
    private readonly string _tempDir;

    public TimeTrustEvaluatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_trust_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void BulkCopiedFiles_TightCluster_Detected()
    {
        // All files share nearly the same ctime (one-time copy).
        string dir = Path.Combine(_tempDir, "copied");
        Directory.CreateDirectory(dir);

        var copyStamp = DateTimeOffset.UtcNow.AddDays(-30);
        for (int i = 0; i < 10; i++)
        {
            string file = Path.Combine(dir, $"f{i}.txt");
            File.WriteAllText(file, $"content {i}");
            File.SetCreationTimeUtc(file, copyStamp.UtcDateTime);
        }

        var result = TimeTrustEvaluator.Evaluate(dir);

        Assert.True(result.IsCopyArtifact, $"Expected copy-artifact, got: {result.Evidence}");
        Assert.True(result.ClusterRatio >= 0.9);
    }

    [Fact]
    public void NaturallyAccumulatedFiles_SpreadTimestamps_Trusted()
    {
        // Files created days apart — natural accumulation, not a copy.
        string dir = Path.Combine(_tempDir, "natural");
        Directory.CreateDirectory(dir);

        var day = TimeSpan.FromDays(1);
        for (int i = 0; i < 5; i++)
        {
            string file = Path.Combine(dir, $"f{i}.txt");
            File.WriteAllText(file, $"content {i}");
            File.SetCreationTimeUtc(file, (DateTimeOffset.UtcNow - day * (10 - i)).UtcDateTime);
        }

        var result = TimeTrustEvaluator.Evaluate(dir);

        Assert.False(result.IsCopyArtifact, $"Expected trusted, got: {result.Evidence}");
        Assert.True(result.ClusterRatio < 0.7);
    }

    [Fact]
    public void MixedDir_PartialCluster_BelowThreshold()
    {
        // 3 files tightly clustered, 3 spread apart → ratio ~0.5 < 0.7 → trusted.
        string dir = Path.Combine(_tempDir, "mixed");
        Directory.CreateDirectory(dir);

        var copyStamp = DateTimeOffset.UtcNow.AddDays(-10);
        for (int i = 0; i < 3; i++)
        {
            string file = Path.Combine(dir, $"copied{i}.txt");
            File.WriteAllText(file, "c");
            File.SetCreationTimeUtc(file, copyStamp.UtcDateTime);
        }

        for (int i = 0; i < 3; i++)
        {
            string file = Path.Combine(dir, $"natural{i}.txt");
            File.WriteAllText(file, "n");
            File.SetCreationTimeUtc(file, (DateTimeOffset.UtcNow - TimeSpan.FromDays(i * 7 + 1)).UtcDateTime);
        }

        var result = TimeTrustEvaluator.Evaluate(dir);

        Assert.False(result.IsCopyArtifact);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}
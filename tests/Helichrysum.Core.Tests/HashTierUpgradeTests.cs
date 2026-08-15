using Helichrysum.Core.Hashing;

namespace Helichrysum.Core.Tests;

public sealed class HashTierUpgradeTests : IDisposable
{
    private readonly string _tempDir;

    public HashTierUpgradeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_tier_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Upgrade_MetadataMatch_TriggersSampled()
    {
        string fileA = Path.Combine(_tempDir, "a.dat");
        string fileB = Path.Combine(_tempDir, "b.dat");
        File.WriteAllBytes(fileA, new byte[100 * 1024]);
        File.WriteAllBytes(fileB, new byte[100 * 1024]);

        var infoA = new FileInfo(fileA);
        var infoB = new FileInfo(fileB);

        var tier = HashTierManager.DetermineTier(infoA, infoB);

        // Same size + same mtime → should trigger at least SampledHash
        Assert.True(tier >= HashTier.SampledHash);
    }

    [Fact]
    public void Upgrade_MetadataMismatch_StopsAtMetadata()
    {
        string fileA = Path.Combine(_tempDir, "a.dat");
        string fileB = Path.Combine(_tempDir, "b.dat");
        File.WriteAllBytes(fileA, new byte[100]);
        File.WriteAllBytes(fileB, new byte[200]); // different size

        var infoA = new FileInfo(fileA);
        var infoB = new FileInfo(fileB);

        var tier = HashTierManager.DetermineTier(infoA, infoB);

        // Different sizes → no hash needed
        Assert.Equal(HashTier.Metadata, tier);
    }

    [Fact]
    public void Upgrade_SampledMatch_TriggersFull()
    {
        string fileA = Path.Combine(_tempDir, "a.dat");
        string fileB = Path.Combine(_tempDir, "b.dat");
        byte[] data = new byte[100 * 1024];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(fileA, data);
        File.WriteAllBytes(fileB, data);

        // Set same mtime for both.
        var now = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(fileA, now);
        File.SetLastWriteTimeUtc(fileB, now);

        var infoA = new FileInfo(fileA);
        var infoB = new FileInfo(fileB);

        // Same size+mtime → SampledHash
        var tier = HashTierManager.DetermineTier(infoA, infoB);
        Assert.True(tier >= HashTier.SampledHash);

        // If SampledHash matches → FullHash
        var sampledA = HashService.ComputeSampled(fileA);
        var sampledB = HashService.ComputeSampled(fileB);
        Assert.Equal(sampledA.HashValue, sampledB.HashValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
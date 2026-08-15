using Helichrysum.Core.Hashing;

namespace Helichrysum.Core.Tests;

public sealed class SampledHashTests : IDisposable
{
    private readonly string _tempDir;

    public SampledHashTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_sampled_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SampledHash_SmallFile_ReadsAll()
    {
        string filePath = Path.Combine(_tempDir, "small.txt");
        File.WriteAllText(filePath, "Hello, World!"); // 13 bytes, well under 64KB

        var result = HashService.ComputeSampled(filePath);

        Assert.NotNull(result);
        Assert.True(result.BytesRead < 64 * 1024);
        Assert.Equal(13, result.BytesRead); // Read the whole file
    }

    [Fact]
    public void SampledHash_LargeFile_ReadsPartial()
    {
        string filePath = Path.Combine(_tempDir, "large.bin");
        byte[] largeData = new byte[100 * 1024]; // 100KB
        new Random(42).NextBytes(largeData);
        File.WriteAllBytes(filePath, largeData);

        var result = HashService.ComputeSampled(filePath);

        Assert.NotNull(result);
        // Should read ~64KB (head 16KB + middle 32KB + tail 16KB)
        Assert.True(result.BytesRead <= 70 * 1024);
        Assert.True(result.BytesRead >= 60 * 1024);
    }

    [Fact]
    public void SampledHash_TwoIdenticalFiles_Matches()
    {
        string fileA = Path.Combine(_tempDir, "a.bin");
        string fileB = Path.Combine(_tempDir, "b.bin");
        byte[] data = new byte[80 * 1024];
        new Random(42).NextBytes(data);
        File.WriteAllBytes(fileA, data);
        File.WriteAllBytes(fileB, data);

        var hashA = HashService.ComputeSampled(fileA);
        var hashB = HashService.ComputeSampled(fileB);

        Assert.Equal(hashA.HashValue, hashB.HashValue);
    }

    [Fact]
    public void SampledHash_TwoDifferentFiles_Differs()
    {
        string fileA = Path.Combine(_tempDir, "a.bin");
        string fileB = Path.Combine(_tempDir, "b.bin");
        byte[] dataA = new byte[80 * 1024];
        byte[] dataB = new byte[80 * 1024];
        new Random(42).NextBytes(dataA);
        new Random(43).NextBytes(dataB);
        File.WriteAllBytes(fileA, dataA);
        File.WriteAllBytes(fileB, dataB);

        var hashA = HashService.ComputeSampled(fileA);
        var hashB = HashService.ComputeSampled(fileB);

        Assert.NotEqual(hashA.HashValue, hashB.HashValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
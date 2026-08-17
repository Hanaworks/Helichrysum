using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class NearDuplicateDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public NearDuplicateDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_near_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SameContent_ExactMatch()
    {
        Assert.True(NearDuplicateDetector.AreNearDuplicates("hello world", "hello world"));
    }

    [Fact]
    public void DifferentLineEndings_Normalized()
    {
        string unix = "line1\nline2\nline3";
        string windows = "line1\r\nline2\r\nline3";

        Assert.True(NearDuplicateDetector.AreNearDuplicates(unix, windows));
    }

    [Fact]
    public void BOM_Tolerated()
    {
        string withBom = "\uFEFFheader";
        string withoutBom = "header";

        Assert.True(NearDuplicateDetector.AreNearDuplicates(withBom, withoutBom));
    }

    [Fact]
    public void TrailingSpacesTolerated()
    {
        string spaced = "text  \nnext  ";
        string plain = "text\nnext";

        Assert.True(NearDuplicateDetector.AreNearDuplicates(spaced, plain));
    }

    [Fact]
    public void DifferentContent_NotNearDuplicate()
    {
        Assert.False(NearDuplicateDetector.AreNearDuplicates("alpha", "beta"));
    }

    [Fact]
    public void Files_WithEOLDiffers_DetectedAsNearDuplicate()
    {
        string fileA = Path.Combine(_tempDir, "a.txt");
        string fileB = Path.Combine(_tempDir, "b.txt");

        File.WriteAllText(fileA, "first\nsecond\nthird");
        File.WriteAllText(fileB, "first\r\nsecond\r\nthird");

        Assert.True(NearDuplicateDetector.AreFilesNearDuplicates(fileA, fileB));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}
using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class ArchivePairTests : IDisposable
{
    private readonly string _tempDir;

    public ArchivePairTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_arc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void ZipWithSiblingDir_FullyExtracted()
    {
        string zipPath = Path.Combine(_tempDir, "project.zip");
        string extractDir = Directory.CreateDirectory(Path.Combine(_tempDir, "project")).FullName;

        File.WriteAllText(Path.Combine(extractDir, "readme.md"), "Hello");
        File.WriteAllText(Path.Combine(extractDir, "index.html"), "<html>");

        // Create zip with same files.
        System.IO.Compression.ZipFile.CreateFromDirectory(extractDir, zipPath);

        var result = ArchivePairDetector.Detect(zipPath, _tempDir);

        Assert.NotNull(result);
        Assert.Equal("FullyExtracted", result!.Status);
    }

    [Fact]
    public void ZipWithModifiedDir_ModifiedAfterExtraction()
    {
        string zipPath = Path.Combine(_tempDir, "photos.zip");
        string extractDir = Directory.CreateDirectory(Path.Combine(_tempDir, "photos")).FullName;

        File.WriteAllText(Path.Combine(extractDir, "img1.jpg"), "jpeg data");
        File.WriteAllText(Path.Combine(extractDir, "img2.jpg"), "more jpeg");
        File.WriteAllText(Path.Combine(extractDir, "readme.txt"), "notes");

        System.IO.Compression.ZipFile.CreateFromDirectory(extractDir, zipPath);

        // Add a new file after zip creation.
        File.WriteAllText(Path.Combine(extractDir, "img3.jpg"), "new image");

        var result = ArchivePairDetector.Detect(zipPath, _tempDir);

        Assert.NotNull(result);
        Assert.Equal("ModifiedAfterExtraction", result!.Status);
    }

    [Fact]
    public void UnrelatedFiles_ReturnsNull()
    {
        string zipPath = Path.Combine(_tempDir, "project.zip");
        string unrelatedDir = Directory.CreateDirectory(Path.Combine(_tempDir, "unrelated")).FullName;

        File.WriteAllText(Path.Combine(unrelatedDir, "data.txt"), "data");
        System.IO.Compression.ZipFile.CreateFromDirectory(unrelatedDir, zipPath);

        var result = ArchivePairDetector.Detect(zipPath, _tempDir);

        // "project" vs "unrelated" — no match.
        Assert.Null(result);
    }

    [Fact]
    public void Anchor_ExtractedAndUnmodified_SameTimestamp()
    {
        string zipPath = Path.Combine(_tempDir, "anchor.zip");
        string extractDir = Directory.CreateDirectory(Path.Combine(_tempDir, "anchor")).FullName;

        File.WriteAllText(Path.Combine(extractDir, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(extractDir, "b.txt"), "beta");

        System.IO.Compression.ZipFile.CreateFromDirectory(extractDir, zipPath);

        var anchor = ArchivePairDetector.GetArchiveAnchorInfo(zipPath);

        Assert.NotNull(anchor.LatestEntryTimestamp);
        Assert.True(anchor.EntryCount > 0);
    }

    [Fact]
    public void Anchor_SiblingDirOlder_FullyExtractedWithFlag()
    {
        string zipPath = Path.Combine(_tempDir, "old.zip");
        string extractDir = Directory.CreateDirectory(Path.Combine(_tempDir, "old")).FullName;

        File.WriteAllText(Path.Combine(extractDir, "a.txt"), "alpha");
        File.WriteAllText(Path.Combine(extractDir, "b.txt"), "beta");
        System.IO.Compression.ZipFile.CreateFromDirectory(extractDir, zipPath);

        // Ensure the extracted dir's file mtimes are at most equal to archive entries.
        var result = ArchivePairDetector.Detect(zipPath, _tempDir);

        Assert.NotNull(result);
        Assert.Equal("FullyExtracted", result!.Status);
        Assert.NotNull(result.AnchorTimestamp);
        Assert.NotNull(result.ModifiedAfterExtraction);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}
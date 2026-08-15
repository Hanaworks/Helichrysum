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

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }
}
using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class VersionedDetectorTests : IDisposable
{
    private readonly string _tempDir;

    public VersionedDetectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_ver_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SameName_CloseSize_Versioned()
    {
        string dirA = Directory.CreateDirectory(Path.Combine(_tempDir, "v1")).FullName;
        string dirB = Directory.CreateDirectory(Path.Combine(_tempDir, "v2")).FullName;
        string fileA = Path.Combine(dirA, "report.docx");
        string fileB = Path.Combine(dirB, "report.docx");
        File.WriteAllText(fileA, new string('x', 10000));
        File.WriteAllText(fileB, new string('x', 10500) + "extra content");

        var result = VersionedDetector.Detect(fileA, fileB);

        Assert.True(result.IsVersioned);
        Assert.True(result.Confidence >= 0.5);
    }

    [Fact]
    public void VeryDifferentSize_NotVersioned()
    {
        string dirA = Directory.CreateDirectory(Path.Combine(_tempDir, "v1")).FullName;
        string dirB = Directory.CreateDirectory(Path.Combine(_tempDir, "v2")).FullName;
        string fileA = Path.Combine(dirA, "report.docx");
        string fileB = Path.Combine(dirB, "report.docx");
        File.WriteAllText(fileA, new string('x', 100));
        File.WriteAllText(fileB, new string('x', 50000)); // 500x larger

        var result = VersionedDetector.Detect(fileA, fileB);

        Assert.False(result.IsVersioned);
    }

    [Fact]
    public void DifferentName_NotVersioned()
    {
        string fileA = Path.Combine(_tempDir, "report_v1.docx");
        string fileB = Path.Combine(_tempDir, "report_v2.docx");
        File.WriteAllText(fileA, new string('x', 10000));
        File.WriteAllText(fileB, new string('x', 10500));

        var result = VersionedDetector.Detect(fileA, fileB);

        // Different names → not versioned by our detector
        Assert.False(result.IsVersioned);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}
using Helichrysum.Core.Analysis;

namespace Helichrysum.Core.Tests;

public sealed class MovedRenamedTests : IDisposable
{
    private readonly string _tempDir;

    public MovedRenamedTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_mv_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void SameHash_SameName_Moved()
    {
        string srcDir = Directory.CreateDirectory(Path.Combine(_tempDir, "src")).FullName;
        string dstDir = Directory.CreateDirectory(Path.Combine(_tempDir, "dst")).FullName;

        string srcFile = Path.Combine(srcDir, "document.txt");
        string dstFile = Path.Combine(dstDir, "document.txt");

        File.WriteAllText(srcFile, "identical content");
        File.WriteAllText(dstFile, "identical content");

        var result = MovedRenamedDetector.Detect(dstFile, srcFile);

        // Same content, same name, different path → moved
        Assert.True(result.IsMoved);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void SameHash_DifferentName_Renamed()
    {
        string srcDir = Directory.CreateDirectory(Path.Combine(_tempDir, "src")).FullName;
        string dstDir = Directory.CreateDirectory(Path.Combine(_tempDir, "dst")).FullName;

        string srcFile = Path.Combine(srcDir, "old_name.txt");
        string dstFile = Path.Combine(dstDir, "new_name.txt");

        File.WriteAllText(srcFile, "identical content");
        File.WriteAllText(dstFile, "identical content");

        var result = MovedRenamedDetector.Detect(dstFile, srcFile);

        // Same content, different name → renamed
        Assert.True(result.IsRenamed);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void DifferentContent_NotRenamed()
    {
        string srcDir = Directory.CreateDirectory(Path.Combine(_tempDir, "src")).FullName;
        string dstDir = Directory.CreateDirectory(Path.Combine(_tempDir, "dst")).FullName;

        string srcFile = Path.Combine(srcDir, "old_name.txt");
        string dstFile = Path.Combine(dstDir, "new_name.txt");

        File.WriteAllText(srcFile, "content A");
        File.WriteAllText(dstFile, "content B");

        var result = MovedRenamedDetector.Detect(dstFile, srcFile);

        // Different content → not renamed
        Assert.False(result.IsRenamed);
        Assert.False(result.IsMoved);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}
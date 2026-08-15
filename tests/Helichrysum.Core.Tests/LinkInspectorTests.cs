using Helichrysum.Filesystem;

namespace Helichrysum.Core.Tests;

public sealed class LinkInspectorTests : IDisposable
{
    private readonly string _tempDir;

    public LinkInspectorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_link_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void LinkInspector_RegularFile_NotLink()
    {
        string filePath = Path.Combine(_tempDir, "regular.txt");
        File.WriteAllText(filePath, "test");

        var inspector = new PlatformLinkInspector();
        var info = inspector.Inspect(filePath);

        Assert.False(info.IsLink);
        Assert.Equal(LinkKind.None, info.Kind);
    }

    [Fact]
    public void LinkInspector_Symlink_DetectsTarget()
    {
        string targetPath = Path.Combine(_tempDir, "target.txt");
        string linkPath = Path.Combine(_tempDir, "link.txt");
        File.WriteAllText(targetPath, "linked content");
        File.CreateSymbolicLink(linkPath, targetPath);

        var inspector = new PlatformLinkInspector();
        var info = inspector.Inspect(linkPath);

        Assert.True(info.IsLink);
        Assert.Equal(LinkKind.Symlink, info.Kind);
        Assert.NotNull(info.Target);
    }

    [Fact]
    public void LinkInspector_BrokenSymlink_MarkedBroken()
    {
        string linkPath = Path.Combine(_tempDir, "broken.link");
        File.CreateSymbolicLink(linkPath, "/nonexistent/target/file");

        var inspector = new PlatformLinkInspector();
        var info = inspector.Inspect(linkPath);

        Assert.True(info.IsLink);
        Assert.Equal(LinkKind.Symlink, info.Kind);
        Assert.False(File.Exists(info.ResolvedTarget));
    }

    [Fact]
    public void LinkInspector_Directory_NotLink()
    {
        string dirPath = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(dirPath);

        var inspector = new PlatformLinkInspector();
        var info = inspector.Inspect(dirPath);

        Assert.False(info.IsLink);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
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

    [Fact]
    public void LinkInspector_Hardlink_SameInodeGroup()
    {
        // Create a real hardlink pair and verify both share the same inode group.
        string fileA = Path.Combine(_tempDir, "hardlink_original.txt");
        string fileB = Path.Combine(_tempDir, "hardlink_alias.txt");
        File.WriteAllText(fileA, "hardlinked content");

        if (!TryCreateHardLink(fileB, fileA))
        {
            return; // Platform doesn't support hardlink creation — skip gracefully.
        }

        var inspector = new PlatformLinkInspector();
        var infoA = inspector.Inspect(fileA);
        var infoB = inspector.Inspect(fileB);

        // Both should be flagged as hardlinks sharing the same inode group.
        Assert.True(infoA.IsLink, "original should be detected as hardlink");
        Assert.True(infoB.IsLink, "alias should be detected as hardlink");
        Assert.Equal(infoA.InodeGroup, infoB.InodeGroup);
        Assert.NotNull(infoA.InodeGroup);
    }

    private static bool TryCreateHardLink(string linkPath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            // .NET 10 removed File.CreateHardLink; use P/Invoke directly.
            return CreateHardLinkWindows(linkPath, targetPath, System.IntPtr.Zero) != 0;
        }

        // Linux/macOS: P/Invoke to link().
        try
        {
            return NativeLink(linkPath, targetPath) == 0;
        }
        catch
        {
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "CreateHardLinkW", SetLastError = true)]
    private static extern int CreateHardLinkWindows(string lpFileName, string lpExistingFileName, System.IntPtr lpSecurityAttributes);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "link")]
    private static extern int NativeLink(string newPath, string existingPath);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
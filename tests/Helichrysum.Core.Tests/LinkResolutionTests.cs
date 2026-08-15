using Helichrysum.Core.Links;
using Helichrysum.Core.Scope;
using Helichrysum.Filesystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Helichrysum.Core.Tests;

public sealed class LinkResolutionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ScopeConfiguration _scope;
    private readonly HashSet<string> _visited;
    private readonly LinkResolver _resolver;

    public LinkResolutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_link_resolve_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _scope = new ScopeConfiguration();
        _scope.AddRoot(_tempDir);

        _visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _resolver = new LinkResolver(
            _scope,
            new PlatformLinkInspector(),
            _visited,
            NullLogger<LinkResolver>.Instance);
    }

    [Fact]
    public void RegularFile_NotLink()
    {
        string filePath = Path.Combine(_tempDir, "regular.txt");
        File.WriteAllText(filePath, "content");

        var result = _resolver.Resolve(filePath);

        Assert.False(result.IsLink);
        Assert.Equal("InScope", result.ScopeRelation);
    }

    [Fact]
    public void Symlink_ScopeIn_Resolved()
    {
        string targetPath = Path.Combine(_tempDir, "target.txt");
        string linkPath = Path.Combine(_tempDir, "link.txt");
        File.WriteAllText(targetPath, "content");
        File.CreateSymbolicLink(linkPath, targetPath);

        var result = _resolver.Resolve(linkPath);

        Assert.True(result.IsLink);
        Assert.Equal("InScope", result.ScopeRelation);
        Assert.NotNull(result.ResolvedLinkTarget);
    }

    [Fact]
    public void Symlink_ScopeOut_MarkedOutOfScope()
    {
        string linkPath = Path.Combine(_tempDir, "out.link");
        string outsidePath = "/tmp/helichrysum_outside_test.txt";
        try
        {
            File.WriteAllText(outsidePath, "outside");
            File.CreateSymbolicLink(linkPath, outsidePath);

            var result = _resolver.Resolve(linkPath);

            Assert.True(result.IsLink);
            Assert.Equal("OutOfScope", result.ScopeRelation);
        }
        finally
        {
            if (File.Exists(outsidePath)) File.Delete(outsidePath);
        }
    }

    [Fact]
    public void BrokenSymlink_MarkedBroken()
    {
        string linkPath = Path.Combine(_tempDir, "broken.link");
        File.CreateSymbolicLink(linkPath, "/nonexistent_target_file_xyz");

        var result = _resolver.Resolve(linkPath);

        Assert.True(result.IsLink);
        Assert.Equal("Broken", result.ScopeRelation);
    }

    [Fact]
    public void CircularSymlink_Detected()
    {
        // Create a subdirectory with a symlink pointing back to its parent.
        string subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        // Create a symlink in the subdir that points to the parent.
        string linkPath = Path.Combine(subDir, "parent.link");
        File.CreateSymbolicLink(linkPath, _tempDir);

        // The parent directory is already in the visited set (it was added during traversal).
        _visited.Add(_tempDir);

        var result = _resolver.Resolve(linkPath);

        Assert.Equal("Circular", result.ScopeRelation);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
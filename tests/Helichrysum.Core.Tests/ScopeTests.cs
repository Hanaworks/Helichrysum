using Helichrysum.Core.Scope;

namespace Helichrysum.Core.Tests;

public sealed class ScopeTests : IDisposable
{
    private readonly string _tempDir;

    public ScopeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_scope_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Scope_Contains_AcceptsPathWithinRoot()
    {
        string root = Path.Combine(_tempDir, "backup1");
        Directory.CreateDirectory(root);

        var scope = new ScopeConfiguration();
        scope.AddRoot(root);

        string candidate = Path.Combine(root, "report.docx");
        Assert.True(scope.Contains(candidate));
    }

    [Fact]
    public void Scope_Contains_RejectsPathOutsideRoot()
    {
        string root = Path.Combine(_tempDir, "backup1");
        string otherDir = Path.Combine(_tempDir, "elsewhere");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(otherDir);

        var scope = new ScopeConfiguration();
        scope.AddRoot(root);

        string candidate = Path.Combine(otherDir, "file.txt");
        Assert.False(scope.Contains(candidate));
    }

    [Fact]
    public void Scope_Contains_PrefixBoundary_NotConfused()
    {
        string root = Path.Combine(_tempDir, "backup1");
        string lookalike = Path.Combine(_tempDir, "backup10");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(lookalike);

        var scope = new ScopeConfiguration();
        scope.AddRoot(root);

        // A sibling "backup10" must NOT match prefix "backup1".
        string candidate = Path.Combine(lookalike, "file.txt");
        Assert.False(scope.Contains(candidate));
    }

    [Fact]
    public void Scope_ExcludePattern_ExcludesMatchingPath()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot(_tempDir);
        scope.AddExclude("*.tmp");

        Assert.True(scope.IsExcluded("cache.tmp"));
        Assert.False(scope.IsExcluded("notes.txt"));
    }

    [Fact]
    public void Scope_CanonicalPath_ResolvesCorrectly()
    {
        var scope = new ScopeConfiguration();
        string canonical = scope.CanonicalizePath(_tempDir);

        Assert.False(string.IsNullOrEmpty(canonical));
        Assert.True(Path.IsPathRooted(canonical));
    }

    [Fact]
    public void Scope_MultipleRoots_AllPathsAccepted()
    {
        string rootA = Path.Combine(_tempDir, "backup1");
        string rootB = Path.Combine(_tempDir, "backup2");
        Directory.CreateDirectory(rootA);
        Directory.CreateDirectory(rootB);

        var scope = new ScopeConfiguration();
        scope.AddRoot(rootA);
        scope.AddRoot(rootB);

        Assert.True(scope.Contains(Path.Combine(rootA, "file.txt")));
        Assert.True(scope.Contains(Path.Combine(rootB, "doc.docx")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) TestFileHelper.DeleteDirectoryWithRetry(_tempDir);
    }
}
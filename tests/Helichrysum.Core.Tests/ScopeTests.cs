using Helichrysum.Core.Scope;

namespace Helichrysum.Core.Tests;

public sealed class ScopeTests
{
    [Fact]
    public void Scope_Contains_AcceptsPathWithinRoot()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot("/data/backup1");

        bool result = scope.Contains("/data/backup1/report.docx");

        Assert.True(result);
    }

    [Fact]
    public void Scope_Contains_RejectsPathOutsideRoot()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot("/data/backup1");

        bool result = scope.Contains("/other/path/file.txt");

        Assert.False(result);
    }

    [Fact]
    public void Scope_ExcludePattern_ExcludesMatchingPath()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot("/data");
        scope.AddExclude("*.tmp");

        bool result = scope.IsExcluded("cache.tmp");

        Assert.True(result);
    }

    [Fact]
    public void Scope_CanonicalPath_ResolvesCorrectly()
    {
        var scope = new ScopeConfiguration();
        string relative = ".";
        string canonical = scope.CanonicalizePath(relative);

        Assert.False(string.IsNullOrEmpty(canonical));
        Assert.True(Path.IsPathRooted(canonical));
    }

    [Fact]
    public void Scope_MultipleRoots_AllPathsAccepted()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot("/data/backup1");
        scope.AddRoot("/data/backup2");

        Assert.True(scope.Contains("/data/backup1/file.txt"));
        Assert.True(scope.Contains("/data/backup2/doc.docx"));
        Assert.False(scope.Contains("/data/backup3/other.txt"));
    }
}
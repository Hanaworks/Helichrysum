using Helichrysum.Core.Scope;
using Helichrysum.Core.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Helichrysum.Core.Tests;

public sealed class ScannerLinkTests : IDisposable
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "tests", "fixtures");

    private readonly string _tempDir;

    public ScannerLinkTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_scanner_link_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Scanner_Links_ScopeRelationCorrect()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot(Path.Combine(FixtureRoot, "links"));

        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var results = new List<Manifest.FilesystemObject>();

        await foreach (var obj in scanner.ScanAsync(new ScanOptions(), null, CancellationToken.None))
        {
            results.Add(obj);
        }

        // Should find: directory entry, target.txt (regular file), in_scope.link (symlink), broken.link (broken)
        var inScopeLink = results.FirstOrDefault(r => r.Path.EndsWith("in_scope.link"));
        Assert.NotNull(inScopeLink);
        Assert.Equal("Symlink", inScopeLink.Kind);
        Assert.Equal("InScope", inScopeLink.ScopeRelation);

        var brokenLink = results.FirstOrDefault(r => r.Path.EndsWith("broken.link"));
        Assert.NotNull(brokenLink);
        Assert.Equal("Broken", brokenLink.ScopeRelation);
    }

    [Fact]
    public async Task Scanner_Symlink_NotFollowedIntoDirectory()
    {
        // Create a directory structure: scope_dir/ + subdir/ + symlink back to scope_dir
        string scopeDir = Path.Combine(_tempDir, "scope_root");
        string subDir = Path.Combine(scopeDir, "subdir");
        string realFile = Path.Combine(scopeDir, "real.txt");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(realFile, "content");

        // Create a symlink in subdir pointing back to scope_root (circular if followed).
        string symlinkPath = Path.Combine(subDir, "back.link");
        File.CreateSymbolicLink(symlinkPath, scopeDir);

        var scope = new ScopeConfiguration();
        scope.AddRoot(scopeDir);

        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var results = new List<Manifest.FilesystemObject>();

        await foreach (var obj in scanner.ScanAsync(new ScanOptions(), null, CancellationToken.None))
        {
            results.Add(obj);
        }

        // The symlink points back to an already-visited directory, so it's detected as circular.
        var link = results.FirstOrDefault(r => r.Path.EndsWith("back.link"));
        Assert.NotNull(link);
        Assert.Equal("Symlink", link.Kind);
        Assert.Equal("Circular", link.ScopeRelation);

        // The real file should still be found (regular scan).
        Assert.Contains(results, r => r.Path.EndsWith("real.txt") && r.Kind == "RegularFile");
    }

    [Fact]
    public async Task Scanner_DoesNotCrossMountPoint()
    {
        // This test verifies that the scanner doesn't follow symlinks pointing outside scope.
        string outsideDir = Path.Combine(_tempDir, "outside");
        string scopeDir = Path.Combine(_tempDir, "scope");
        Directory.CreateDirectory(outsideDir);
        Directory.CreateDirectory(scopeDir);

        File.WriteAllText(Path.Combine(outsideDir, "outside_file.txt"), "outside content");
        File.WriteAllText(Path.Combine(scopeDir, "in_scope.txt"), "in scope");

        // Create a symlink inside scope pointing outside.
        File.CreateSymbolicLink(Path.Combine(scopeDir, "to_outside.link"), outsideDir);

        var scope = new ScopeConfiguration();
        scope.AddRoot(scopeDir);

        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var results = new List<Manifest.FilesystemObject>();

        await foreach (var obj in scanner.ScanAsync(new ScanOptions(), null, CancellationToken.None))
        {
            results.Add(obj);
        }

        // The symlink should be marked as OutOfScope.
        var link = results.FirstOrDefault(r => r.Path.EndsWith("to_outside.link"));
        Assert.NotNull(link);
        Assert.Equal("OutOfScope", link.ScopeRelation);

        // Files outside scope should NOT be in the results.
        Assert.DoesNotContain(results, r => r.Path.EndsWith("outside_file.txt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }
}
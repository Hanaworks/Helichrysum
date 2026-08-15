using Helichrysum.Core.Scope;
using Helichrysum.Core.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

namespace Helichrysum.Core.Tests;

public sealed class ScanningTests : IDisposable
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "tests", "fixtures");

    private readonly string _tempSymlinkDir;
    private readonly string _tempNoAccessDir;

    public ScanningTests()
    {
        _tempSymlinkDir = Path.Combine(Path.GetTempPath(), "helichrysum_test_symlink_" + Guid.NewGuid().ToString("N"));
        _tempNoAccessDir = Path.Combine(Path.GetTempPath(), "helichrysum_test_noaccess_" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task Scanner_CountsFiles_Correctly()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot(Path.Combine(FixtureRoot, "backup1"));

        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var progress = new TestProgress();
        var results = new List<Manifest.FilesystemObject>();

        await foreach (var obj in scanner.ScanAsync(new ScanOptions(), progress, CancellationToken.None))
        {
            results.Add(obj);
        }

        Assert.Equal(2, results.Count(r => r.Kind == "RegularFile"));
        Assert.All(results, obj => Assert.NotNull(obj.Path));
    }

    [Fact]
    public async Task Scanner_RespectsExcludePattern()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot(Path.Combine(FixtureRoot, "backup1"));
        scope.AddExclude("*.txt");

        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var results = new List<Manifest.FilesystemObject>();

        await foreach (var obj in scanner.ScanAsync(new ScanOptions(), new TestProgress(), CancellationToken.None))
        {
            results.Add(obj);
        }

        Assert.DoesNotContain(results, r => r.Kind == "RegularFile" && r.Path.EndsWith(".txt"));
    }

    [Fact]
    public async Task Scanner_ReportsProgress()
    {
        var scope = new ScopeConfiguration();
        scope.AddRoot(Path.Combine(FixtureRoot, "backup1"));

        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var progress = new TestProgress();

        await foreach (var _ in scanner.ScanAsync(new ScanOptions(), progress, CancellationToken.None))
        {
        }

        Assert.True(progress.TotalFiles > 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempSymlinkDir))
        {
            Directory.Delete(_tempSymlinkDir, true);
        }

        if (Directory.Exists(_tempNoAccessDir))
        {
            Directory.Delete(_tempNoAccessDir, true);
        }
    }

    private sealed class TestProgress : IProgress<ScanProgress>
    {
        public int TotalFiles { get; private set; }

        public void Report(ScanProgress value)
        {
            TotalFiles = value.FilesScanned;
        }
    }
}
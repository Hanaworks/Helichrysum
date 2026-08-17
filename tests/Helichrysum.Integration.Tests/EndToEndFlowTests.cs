namespace Helichrysum.Integration.Tests;

using Helichrysum.Core.Analysis;
using Helichrysum.Core.Hashing;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Planning;
using Helichrysum.Core.Reporting;
using Helichrysum.Core.Scope;
using Helichrysum.Core.Scanning;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// End-to-end integration test: Scan → Analyze → Plan → Exec → Verify.
/// Exercises the full pipeline against a fixture directory.
/// </summary>
public sealed class EndToEndFlowTests : IDisposable
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "tests", "fixtures");

    private readonly string _manifestPath;
    private readonly string _tempDir;

    public EndToEndFlowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"helichrysum_e2e_{Guid.NewGuid():N}");
        _manifestPath = Path.Combine(_tempDir, "e2e.sqlite");

        // Copy the shared fixture into an isolated temp dir so tests that
        // execute file mutations don't pollute the shared fixtures.
        Directory.CreateDirectory(Path.Combine(_tempDir, "backup1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "backup2"));

        string src1 = Path.Combine(FixtureRoot, "backup1");
        string src2 = Path.Combine(FixtureRoot, "backup2");
        foreach (string file in Directory.GetFiles(src1))
            File.Copy(file, Path.Combine(_tempDir, "backup1", Path.GetFileName(file)));
        foreach (string file in Directory.GetFiles(src2))
            File.Copy(file, Path.Combine(_tempDir, "backup2", Path.GetFileName(file)));
    }

    [Fact]
    public async Task FullPipeline_ScanAnalyzePlanExecVerify()
    {
        // 1. SCAN: files in backup1/backup2 (4 files total, readme.txt duplicated).
        var scope = new ScopeConfiguration();
        scope.AddRoot(Path.Combine(_tempDir, "backup1"));
        scope.AddRoot(Path.Combine(_tempDir, "backup2"));

        using var repository = ManifestRepository.Open(_manifestPath);
        var scanner = new Scanner(scope, NullLogger<Scanner>.Instance);
        var scanned = new List<Helichrysum.Core.Manifest.FilesystemObject>();

        await foreach (var obj in scanner.ScanAsync(new ScanOptions(), null, CancellationToken.None))
        {
            scanned.Add(obj);
        }

        repository.BatchInsertObjects(scanned.Where(o => o.Kind == "RegularFile"));

        Assert.Equal(4, scanned.Count(o => o.Kind == "RegularFile"));

        // 2. ANALYZE: hash all files, detect duplicates.
        var files = repository.GetAllFiles();
        foreach (var file in files)
        {
            string hash = HashService.ComputeSha256(file.CanonicalPath);
            repository.InsertHash(new HashRecord
            {
                ObjectId = file.Id, Tier = "FullHash", HashValue = hash,
                BytesRead = file.Size ?? 0, ComputedAt = DateTimeOffset.UtcNow,
            });
        }

        var detector = new ExactDuplicateDetector(repository);
        var relations = detector.Detect();

        Assert.Single(relations); // readme.txt appears in both backup dirs.

        // 3. PLAN: generate processing plan from duplicate groups.
        var duplicateGroups = repository.GetDuplicateGroups();
        var plan = PlanGenerator.Generate(duplicateGroups);

        Assert.Single(plan.Actions); // one file to trash.
        Assert.Empty(plan.Conflicts);

        // 4. EXEC: execute the plan with a path resolver.
        var pathMap = files.ToDictionary(f => f.Id, f => f.CanonicalPath);
        var stagingDir = Path.Combine(_tempDir, "staging");
        var trashDir = Path.Combine(_tempDir, "trash");
        var executor = new Helichrysum.Core.Execution.Executor(trashDir, stagingDir);

        int success = executor.ExecutePlan(plan, id =>
        {
            if (pathMap.TryGetValue(id, out string? path))
            {
                string hash = HashService.ComputeSha256(path);
                return (path, hash);
            }
            return null;
        });

        Assert.Equal(1, success);

        // 5. VERIFY: re-hash remaining files, all should match.
        int verified = 0;
        foreach (var file in repository.GetAllFiles())
        {
            if (!File.Exists(file.CanonicalPath)) continue;
            string current = HashService.ComputeSha256(file.CanonicalPath);
            string? stored = repository.GetHashByObjectId(file.Id);
            if (stored != null && current == stored) verified++;
        }

        Assert.True(verified >= 1, "At least one remaining file should verify");
    }

    [Fact]
    public void Report_Json_GeneratedAfterPipeline()
    {
        // Minimal setup: insert two identical files manually.
        using var repository = ManifestRepository.Open(_manifestPath);

        string fileA = Path.Combine(_tempDir, "a.txt");
        string fileB = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(fileA, "duplicate content");
        File.WriteAllText(fileB, "duplicate content");

        var objA = new FilesystemObject { Id = 0, ScopeId = 1, Path = fileA, CanonicalPath = fileA, Kind = "RegularFile", Size = new FileInfo(fileA).Length, DeviceId = 0, ScopeRelation = "InScope" };
        var objB = new FilesystemObject { Id = 0, ScopeId = 1, Path = fileB, CanonicalPath = fileB, Kind = "RegularFile", Size = new FileInfo(fileB).Length, DeviceId = 0, ScopeRelation = "InScope" };

        long idA = repository.InsertObject(objA);
        long idB = repository.InsertObject(objB);

        string hash = HashService.ComputeSha256(fileA);
        repository.InsertHash(new HashRecord { ObjectId = idA, Tier = "FullHash", HashValue = hash, BytesRead = 0, ComputedAt = DateTimeOffset.UtcNow });
        repository.InsertHash(new HashRecord { ObjectId = idB, Tier = "FullHash", HashValue = hash, BytesRead = 0, ComputedAt = DateTimeOffset.UtcNow });

        repository.SetManifestMeta("created_at", DateTimeOffset.UtcNow.ToString("O"));

        var builder = new ReportBuilder(repository);
        string json = builder.BuildJson();

        Assert.Contains("snapshotAge", json);
        Assert.Contains("duplicateGroupCount", json);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }
}
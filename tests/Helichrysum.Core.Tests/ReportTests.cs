using Helichrysum.Core.Reporting;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Hashing;

namespace Helichrysum.Core.Tests;

public sealed class ReportTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ManifestRepository _repository;

    public ReportTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"helichrysum_test_rpt_{Guid.NewGuid():N}.sqlite");
        _repository = ManifestRepository.Open(_dbPath);
    }

    private long InsertFile(string path, long size, string content)
    {
        var obj = new FilesystemObject
        {
            Id = 0, ScopeId = 1, Path = path, CanonicalPath = path,
            Kind = "RegularFile", Size = size,
            ModifiedTime = DateTimeOffset.UtcNow, CreatedTime = DateTimeOffset.UtcNow,
            InodeGroup = null, DeviceId = 1, ScopeRelation = "InScope",
            LinkTarget = null, ResolvedLinkTarget = null,
        };

        long objectId = _repository.InsertObject(obj);

        string tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        string sha256 = HashService.ComputeSha256(tempPath);
        File.Delete(tempPath);

        _repository.InsertHash(new HashRecord
        {
            ObjectId = objectId, Tier = "FullHash", HashValue = sha256,
            BytesRead = content.Length, ComputedAt = DateTimeOffset.UtcNow,
        });

        return objectId;
    }

    [Fact]
    public void Report_Json_ContainsDuplicateGroups()
    {
        InsertFile("/test/a.txt", 100, "Same content");
        InsertFile("/test/b.txt", 100, "Same content");

        // Run duplicate detection to populate the relations.
        var detector = new Analysis.ExactDuplicateDetector(_repository);
        detector.Detect();

        var builder = new ReportBuilder(_repository);
        string json = builder.BuildJson();

        Assert.Contains("DuplicateGroupCount", json);
        Assert.Contains("HashValue", json);
    }

    [Fact]
    public void Report_Html_Generated_And_Valid()
    {
        InsertFile("/test/a.txt", 100, "Same content");
        InsertFile("/test/b.txt", 100, "Same content");

        var detector = new Analysis.ExactDuplicateDetector(_repository);
        detector.Detect();

        var builder = new ReportBuilder(_repository);
        string html = builder.BuildHtml();

        Assert.Contains("<html", html);
        Assert.Contains("</html>", html);
        Assert.True(html.Length < 5 * 1024 * 1024, "HTML report exceeds 5MB");
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
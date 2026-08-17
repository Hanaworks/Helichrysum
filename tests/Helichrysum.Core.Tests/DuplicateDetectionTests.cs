using Helichrysum.Core.Analysis;
using Helichrysum.Core.Manifest;
using Helichrysum.Core.Hashing;

namespace Helichrysum.Core.Tests;

public sealed class DuplicateDetectionTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ManifestRepository _repository;

    public DuplicateDetectionTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"helichrysum_test_dup_{Guid.NewGuid():N}.sqlite");
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

        // Compute and store hash.
        string tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        uint crc32 = HashService.ComputeCrc32(tempPath);
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
    public void Detect_IdenticalFiles_Grouped()
    {
        long id1 = InsertFile("/test/a.txt", 100, "Hello, world!");
        long id2 = InsertFile("/test/b.txt", 100, "Hello, world!");

        var detector = new ExactDuplicateDetector(_repository);
        var relations = detector.Detect();

        Assert.NotEmpty(relations);
        Assert.Contains(relations, r => r.Members.Contains(id1) && r.Members.Contains(id2));
    }

    [Fact]
    public void Detect_DifferentFiles_NotGrouped()
    {
        long id1 = InsertFile("/test/a.txt", 100, "Hello, world!");
        long id2 = InsertFile("/test/b.txt", 200, "Different content");

        var detector = new ExactDuplicateDetector(_repository);
        var relations = detector.Detect();

        // Different sizes → different groups.
        foreach (var relation in relations)
        {
            Assert.False(relation.Members.Contains(id1) && relation.Members.Contains(id2));
        }
    }

    [Fact]
    public void Detect_Group_HasCorrectEvidence()
    {
        long id1 = InsertFile("/test/a.txt", 100, "Same content");
        long id2 = InsertFile("/test/b.txt", 100, "Same content");

        var detector = new ExactDuplicateDetector(_repository);
        var relations = detector.Detect();

        var group = relations.FirstOrDefault(r => r.Members.Contains(id1) && r.Members.Contains(id2));
        Assert.NotNull(group);
        Assert.Equal(1.0, group.Confidence);
        Assert.Contains("HashMatch", group.Evidence);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (File.Exists(_dbPath))
        {
            TestFileHelper.DeleteFileWithRetry(_dbPath);
        }
    }
}
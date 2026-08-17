using Helichrysum.Core.Manifest;

namespace Helichrysum.Core.Tests;

public sealed class ManifestRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ManifestRepository _repository;

    public ManifestRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"helichrysum_test_{Guid.NewGuid():N}.sqlite");
        _repository = ManifestRepository.Open(_dbPath);
    }

    private long CreateTestObject(long scopeId = 1, string path = "/test/file.txt", long size = 1024)
    {
        var obj = new FilesystemObject
        {
            Id = 0, ScopeId = scopeId, Path = path, CanonicalPath = path,
            Kind = "RegularFile", Size = size,
            ModifiedTime = DateTimeOffset.UtcNow, CreatedTime = DateTimeOffset.UtcNow,
            InodeGroup = null, DeviceId = 1, ScopeRelation = "InScope",
            LinkTarget = null, ResolvedLinkTarget = null,
        };

        return _repository.InsertObject(obj);
    }

    [Fact]
    public void Open_CreatesDatabase_WithSchemaVersion()
    {
        int version = _repository.GetSchemaVersion();
        Assert.True(version >= 1);
    }

    [Fact]
    public void InsertObject_Retrievable()
    {
        long id = CreateTestObject();
        Assert.True(id > 0);
    }

    [Fact]
    public void BatchInsertObjects_AllInserted()
    {
        var objects = new List<FilesystemObject>();
        for (int i = 0; i < 5; i++)
        {
            objects.Add(new FilesystemObject
            {
                Id = 0, ScopeId = 1, Path = $"/test/file{i}.txt", CanonicalPath = $"/test/file{i}.txt",
                Kind = "RegularFile", Size = 100 + i,
                ModifiedTime = DateTimeOffset.UtcNow, CreatedTime = DateTimeOffset.UtcNow,
                InodeGroup = null, DeviceId = 1, ScopeRelation = "InScope",
                LinkTarget = null, ResolvedLinkTarget = null,
            });
        }

        _repository.BatchInsertObjects(objects);
        var bySize = _repository.QueryObjectsBySize(102);
        Assert.Single(bySize);
    }

    [Fact]
    public void InsertHash_And_Retrieve()
    {
        long objectId = CreateTestObject();

        var hash = new HashRecord
        {
            ObjectId = objectId,
            Tier = "FullHash",
            HashValue = "abc123def456",
            BytesRead = 1024,
            ComputedAt = DateTimeOffset.UtcNow,
        };

        _repository.InsertHash(hash);
        var objects = _repository.QueryObjectsByHash("abc123def456");
        Assert.NotEmpty(objects);
        Assert.Contains(objects, o => o.Id == objectId);
    }

    [Fact]
    public void InsertRelation_WithMembers()
    {
        long id1 = CreateTestObject(scopeId: 1, path: "/test/a.txt");
        long id2 = CreateTestObject(scopeId: 2, path: "/test/b.txt");

        var relation = new Relation
        {
            Id = 0, Kind = "ExactDuplicate", Confidence = 1.0,
            Evidence = "[{\"Type\":\"HashMatch\",\"Details\":\"SHA256\"}]",
        };

        long relationId = _repository.InsertRelation(relation, new List<long> { id1, id2 });
        Assert.True(relationId > 0);
    }

    [Fact]
    public void GetDuplicateGroups_Returns_Groups()
    {
        long id1 = CreateTestObject(scopeId: 1, path: "/test/a.txt", size: 100);
        long id2 = CreateTestObject(scopeId: 2, path: "/test/b.txt", size: 100);

        _repository.InsertHash(new HashRecord
        {
            ObjectId = id1, Tier = "FullHash", HashValue = "SAMEHASH", BytesRead = 100, ComputedAt = DateTimeOffset.UtcNow,
        });

        _repository.InsertHash(new HashRecord
        {
            ObjectId = id2, Tier = "FullHash", HashValue = "SAMEHASH", BytesRead = 100, ComputedAt = DateTimeOffset.UtcNow,
        });

        var groups = _repository.GetDuplicateGroups();
        Assert.NotEmpty(groups);
        Assert.Contains(groups, g => g.Members.Count >= 2);
    }

    [Fact]
    public void SaveAndGetScanState_Persists()
    {
        _repository.SaveScanState(1, "/test/last_path.txt", "running");
        var state = _repository.GetScanState(1);

        Assert.NotNull(state);
        Assert.Equal("running", state.Status);
        Assert.Equal("/test/last_path.txt", state.LastPath);
    }

    [Fact]
    public void ManifestMeta_IsStored()
    {
        _repository.SetManifestMeta("tool_version", "0.1.0");
        string? version = _repository.GetManifestMeta("tool_version");
        Assert.Equal("0.1.0", version);
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
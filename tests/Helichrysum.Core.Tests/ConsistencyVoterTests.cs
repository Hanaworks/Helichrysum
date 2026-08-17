using Helichrysum.Core.Analysis;
using Helichrysum.Core.Manifest;

namespace Helichrysum.Core.Tests;

public sealed class ConsistencyVoterTests
{
    private static FilesystemObject CreateObject(long id, string path) => new()
    {
        Id = id,
        ScopeId = 1,
        Path = path,
        CanonicalPath = path,
        Kind = "RegularFile",
        Size = 100,
        DeviceId = 0,
        ScopeRelation = "InScope",
        LinkTarget = null,
        ResolvedLinkTarget = null,
    };

    [Fact]
    public void MajorityMatches_OutlierSuspected()
    {
        var members = new[]
        {
            CreateObject(1, "/a.txt"),
            CreateObject(2, "/b.txt"),
            CreateObject(3, "/c.txt"),
        };

        // Majority (2 copies) share hash HASH_A; one outlier has HASH_B.
        string? HashProvider(FilesystemObject obj) => obj.Id switch
        {
            3 => "HASH_B",
            _ => "HASH_A",
        };

        var result = ConsistencyVoter.Vote(members, HashProvider);

        Assert.Equal("HASH_A", result.MajorityHash);
        Assert.Equal(2, result.MajorityCount);
        Assert.Contains(3L, result.OutlierIds);
        Assert.True(result.HasSuspectedOutliers);
    }

    [Fact]
    public void AllSameHash_NoOutliers()
    {
        var members = new[]
        {
            CreateObject(1, "/a.txt"),
            CreateObject(2, "/b.txt"),
            CreateObject(3, "/c.txt"),
        };

        string? HashProvider(FilesystemObject obj) => "SAME";

        var result = ConsistencyVoter.Vote(members, HashProvider);

        Assert.Equal(3, result.MajorityCount);
        Assert.Empty(result.OutlierIds);
        Assert.False(result.HasSuspectedOutliers);
    }

    [Fact]
    public void OnlyTwoCopies_DifferentHashes_NoMajority()
    {
        var members = new[]
        {
            CreateObject(1, "/a.txt"),
            CreateObject(2, "/b.txt"),
        };

        string? HashProvider(FilesystemObject obj) => obj.Id == 1 ? "HASH_X" : "HASH_Y";

        var result = ConsistencyVoter.Vote(members, HashProvider);

        // Both hashes have count 1 — no majority ≥2, so nothing is "suspected" automatically.
        Assert.False(result.HasSuspectedOutliers);
    }

    [Fact]
    public void SingleCopy_NoVote()
    {
        var members = new[]
        {
            CreateObject(1, "/a.txt"),
        };

        string? HashProvider(FilesystemObject obj) => "ONLY";

        var result = ConsistencyVoter.Vote(members, HashProvider);

        Assert.Equal(1, result.MajorityCount);
        Assert.Empty(result.OutlierIds);
        Assert.False(result.HasSuspectedOutliers);
    }
}
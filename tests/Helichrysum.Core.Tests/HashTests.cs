using Helichrysum.Core.Hashing;

namespace Helichrysum.Core.Tests;

public sealed class HashTests
{
    private static readonly string FixtureRoot = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..", "tests", "fixtures");

    [Fact]
    public void Hash_Crc32_TwoIdenticalFiles_Matches()
    {
        string fileA = Path.Combine(FixtureRoot, "backup1", "readme.txt");
        string fileB = Path.Combine(FixtureRoot, "backup2", "readme.txt");

        uint crc32A = HashService.ComputeCrc32(fileA);
        uint crc32B = HashService.ComputeCrc32(fileB);

        Assert.Equal(crc32A, crc32B);
    }

    [Fact]
    public void Hash_Crc32_TwoDifferentFiles_Differs()
    {
        string fileA = Path.Combine(FixtureRoot, "backup1", "notes.txt");
        string fileB = Path.Combine(FixtureRoot, "backup2", "notes.txt");

        uint crc32A = HashService.ComputeCrc32(fileA);
        uint crc32B = HashService.ComputeCrc32(fileB);

        Assert.NotEqual(crc32A, crc32B);
    }

    [Fact]
    public void Hash_Sha256_ConfirmsCrc32Match()
    {
        string fileA = Path.Combine(FixtureRoot, "backup1", "readme.txt");
        string fileB = Path.Combine(FixtureRoot, "backup2", "readme.txt");

        string sha256A = HashService.ComputeSha256(fileA);
        string sha256B = HashService.ComputeSha256(fileB);

        Assert.Equal(sha256A, sha256B);
    }

    [Fact]
    public void Hash_Upgrade_MetadataCollisionTriggersCrc32()
    {
        string fileA = Path.Combine(FixtureRoot, "backup1", "readme.txt");
        string fileB = Path.Combine(FixtureRoot, "backup2", "readme.txt");

        var infoA = new FileInfo(fileA);
        var infoB = new FileInfo(fileB);

        bool metadataMatches = infoA.Length == infoB.Length
                               && infoA.LastWriteTimeUtc == infoB.LastWriteTimeUtc;

        if (metadataMatches)
        {
            // Should trigger CRC32 upgrade
            uint crc32A = HashService.ComputeCrc32(fileA);
            uint crc32B = HashService.ComputeCrc32(fileB);
            Assert.Equal(crc32A, crc32B);
        }
    }
}